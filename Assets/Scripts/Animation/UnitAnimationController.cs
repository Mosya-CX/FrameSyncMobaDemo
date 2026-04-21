using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Mathematics.FixedPoint;

[RequireComponent(typeof(CombatUnitBase))]
public sealed class UnitAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseAnimationProfile baseProfile;
    [SerializeField] private Animator animator;

    [Header("Runtime Limits")]
    [SerializeField] private int maxStateLayers = 8;

    private CombatUnitBase owner;

    private PlayableGraph graph;
    private AnimationLayerMixerPlayable layerMixer;

    private const int Layer_Base = 0;
    private const int Layer_Override = 1;
    private const int Layer_StateStart = 2;

    #region Base Layer

    private AnimationMixerPlayable baseMixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable movePlayable;
    private AnimationClipPlayable dashPlayable;

    private AnimationClipPlayable[] attackPlayables = Array.Empty<AnimationClipPlayable>();
    private int currentAttackClipIndex = -1;
    private BaseAnimState lastBaseState = BaseAnimState.None;

    private AnimationClipPlayable siffnessPlayable;
    private AnimationClipPlayable deadPlayable;

    private BaseAnimState currentBaseState = BaseAnimState.None;
    private float idleMoveBlend01;

    #endregion

    #region Override Layer
    private readonly Dictionary<BaseAnimOverrideType, BaseAnimOverrideEntry> runtimeBaseOverrides = new();

    public void SetBaseAnimOverride(BaseAnimOverrideType type, AnimationClip clip, int priority = 0, string tag = null)
    {
        var entry = new BaseAnimOverrideEntry
        {
            IsValid = clip != null,
            Type = type,
            Clip = clip,
            Clips = null,
            Priority = priority,
            Tag = tag
        };

        if (!entry.IsValid)
        {
            runtimeBaseOverrides.Remove(type);
        }
        else
        {
            if (runtimeBaseOverrides.TryGetValue(type, out var oldEntry))
            {
                if (oldEntry.IsValid && oldEntry.Priority > priority)
                    return;
            }

            runtimeBaseOverrides[type] = entry;
        }

        RebuildBaseLayerRuntime();
    }

    public void SetBaseAnimOverride(BaseAnimOverrideType type, AnimationClip[] clips, int priority = 0, string tag = null)
    {
        bool valid = clips != null && clips.Length > 0;

        var entry = new BaseAnimOverrideEntry
        {
            IsValid = valid,
            Type = type,
            Clip = null,
            Clips = clips,
            Priority = priority,
            Tag = tag
        };

        if (!valid)
        {
            runtimeBaseOverrides.Remove(type);
        }
        else
        {
            if (runtimeBaseOverrides.TryGetValue(type, out var oldEntry))
            {
                if (oldEntry.IsValid && oldEntry.Priority > priority)
                    return;
            }

            runtimeBaseOverrides[type] = entry;
        }

        RebuildBaseLayerRuntime();
    }

    public void ClearBaseAnimOverride(BaseAnimOverrideType type, string tag = null)
    {
        if (!runtimeBaseOverrides.TryGetValue(type, out var entry))
            return;

        if (!string.IsNullOrEmpty(tag) && entry.Tag != tag)
            return;

        runtimeBaseOverrides.Remove(type);
        RebuildBaseLayerRuntime();
    }

    public void ClearAllBaseAnimOverrides()
    {
        if (runtimeBaseOverrides.Count == 0)
            return;

        runtimeBaseOverrides.Clear();
        RebuildBaseLayerRuntime();
    }

    private AnimationClip ResolveBaseClip(BaseAnimOverrideType type, AnimationClip fallback)
    {
        if (runtimeBaseOverrides.TryGetValue(type, out var entry) && entry.IsValid && entry.Clip != null)
            return entry.Clip;

        return fallback;
    }

    private AnimationClip[] ResolveAttackClips()
    {
        if (runtimeBaseOverrides.TryGetValue(BaseAnimOverrideType.AttackSequence, out var entry) &&
            entry.IsValid &&
            entry.Clips != null &&
            entry.Clips.Length > 0)
        {
            return entry.Clips;
        }

        return baseProfile != null ? baseProfile.AttackClips : null;
    }

    [Serializable]
    public struct OverrideRecord
    {
        public bool IsValid;
        public OverlayAnimRequest Request;
        public uint StartTick;
        public fp RemainingDuration;
    }

    private OverrideRecord overrideRecord;
    private OverrideRuntimeSlot overrideSlot;

    #endregion

    #region State Layers

    [Serializable]
    public struct StateAnimRecord
    {
        public int RuntimeId;
        public bool IsValid;
        public OverlayAnimRequest Request;
        public uint StartTick;
        public fp RemainingDuration;
    }

    private readonly List<StateAnimRecord> stateRecords = new();
    private readonly List<StateRuntimeSlot> stateSlots = new();

    private int nextRuntimeStateId = 1;

    #endregion

    private bool initialized;

    private void Awake()
    {
        owner = GetComponent<CombatUnitBase>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        BuildGraph();
        initialized = graph.IsValid();
    }

    private void OnEnable()
    {
        if (graph.IsValid())
            graph.Play();
    }

    private void OnDisable()
    {
        if (graph.IsValid())
            graph.Stop();
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }

    public void Tick(fp deltaTime)
    {
        if (!initialized || owner == null || baseProfile == null)
            return;

        UpdateBaseLayer((float)deltaTime);
        UpdateOverrideLifetime(deltaTime);
        UpdateStateLifetime(deltaTime);

        overrideSlot?.Tick((float)deltaTime);

        for (int i = 0; i < stateSlots.Count; i++)
            stateSlots[i].Tick((float)deltaTime);

        RebuildStateLayerWeightsAndOrder();
    }

    #region Public API

    public void PlayFullBodyOverride(OverlayAnimRequest request, uint startTick)
    {
        if (!initialized)
            return;

        ApplyPresetDefaults(ref request, true);

        if (request.ClipRef == null || request.ClipRef.Clip == null)
            return;

        fp duration = ComputeDuration(request);

        overrideRecord = new OverrideRecord
        {
            IsValid = true,
            Request = request,
            StartTick = startTick,
            RemainingDuration = duration
        };

        overrideSlot.PlayImmediate(request);
    }

    public void StopFullBodyOverride()
    {
        overrideRecord = default;
        overrideSlot?.ClearImmediate();
    }

    public int PlayStateLayer(OverlayAnimRequest request, uint startTick, bool replaceSameTag = true)
    {
        if (!initialized)
            return -1;

        ApplyPresetDefaults(ref request, false);

        if (request.ClipRef == null || request.ClipRef.Clip == null)
            return -1;

        if (replaceSameTag && !string.IsNullOrEmpty(request.Tag))
            RemoveStateLayersByTag(request.Tag);

        int runtimeId = nextRuntimeStateId++;
        fp duration = ComputeDuration(request);

        var record = new StateAnimRecord
        {
            RuntimeId = runtimeId,
            IsValid = true,
            Request = request,
            StartTick = startTick,
            RemainingDuration = duration,
        };

        stateRecords.Add(record);

        var slot = AllocateOrReuseStateSlot();
        slot.Bind(runtimeId, request);
        stateSlots.Add(slot);

        RebuildStateLayerWeightsAndOrder();
        return runtimeId;
    }

    public void StopStateLayer(int runtimeId)
    {
        for (int i = stateRecords.Count - 1; i >= 0; i--)
        {
            if (stateRecords[i].RuntimeId == runtimeId)
                stateRecords.RemoveAt(i);
        }

        for (int i = stateSlots.Count - 1; i >= 0; i--)
        {
            if (stateSlots[i].RuntimeId == runtimeId)
            {
                stateSlots[i].ClearImmediate();
                stateSlots.RemoveAt(i);
            }
        }

        RebuildStateLayerWeightsAndOrder();
    }

    public void RemoveStateLayersByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        for (int i = stateRecords.Count - 1; i >= 0; i--)
        {
            if (stateRecords[i].Request.Tag == tag)
                stateRecords.RemoveAt(i);
        }

        for (int i = stateSlots.Count - 1; i >= 0; i--)
        {
            if (stateSlots[i].Tag == tag)
            {
                stateSlots[i].ClearImmediate();
                stateSlots.RemoveAt(i);
            }
        }

        RebuildStateLayerWeightsAndOrder();
    }

    public void ClearAllStateLayers()
    {
        stateRecords.Clear();

        for (int i = 0; i < stateSlots.Count; i++)
            stateSlots[i].ClearImmediate();

        stateSlots.Clear();
        RebuildStateLayerWeightsAndOrder();
    }

    public void ClearAllAnimationOverrides()
    {
        StopFullBodyOverride();
        ClearAllStateLayers();
    }

    public void RemoveRecordsAfter(uint rollbackTick)
    {
        if (overrideRecord.IsValid && overrideRecord.StartTick >= rollbackTick)
            StopFullBodyOverride();

        for (int i = stateRecords.Count - 1; i >= 0; i--)
        {
            if (stateRecords[i].StartTick >= rollbackTick)
                StopStateLayer(stateRecords[i].RuntimeId);
        }
    }

    public void RebuildOverlayToTick(uint currentTick, float tickInterval)
    {
        // 重建全身覆盖层
        overrideSlot?.ClearImmediate();
        if (overrideRecord.IsValid)
        {
            float elapsed = (currentTick - overrideRecord.StartTick) * tickInterval;
            overrideSlot.PlayRebuilt(overrideRecord.Request, elapsed);
        }

        // 重建状态层
        for (int i = 0; i < stateSlots.Count; i++)
            stateSlots[i].ClearImmediate();

        stateSlots.Clear();

        for (int i = 0; i < stateRecords.Count; i++)
        {
            var record = stateRecords[i];
            float elapsed = (currentTick - record.StartTick) * tickInterval;

            var slot = AllocateOrReuseStateSlot();
            slot.Bind(record.RuntimeId, record.Request);
            slot.PlayRebuilt(record.Request, elapsed);
            stateSlots.Add(slot);
        }

        RebuildStateLayerWeightsAndOrder();
    }

    #endregion

    #region Base Layer

    private void UpdateBaseLayer(float deltaTime)
    {
        ResolveAndApplyBaseState();
        UpdateIdleMoveBlend(deltaTime);
        UpdateBasePlaybackSpeed();
    }

    private void ResolveAndApplyBaseState()
    {
        var next = ResolveBaseState();
        if (next == currentBaseState)
            return;

        lastBaseState = currentBaseState;
        currentBaseState = next;

        if (currentBaseState == BaseAnimState.Attack && lastBaseState != BaseAnimState.Attack)
            AdvanceAttackClipIndex();

        ApplyBaseStateWeights();

        if (currentBaseState == BaseAnimState.Dead)
            ClearAllAnimationOverrides();
    }

    private BaseAnimState ResolveBaseState()
    {
        if (owner == null)
            return BaseAnimState.None;

        if (owner.IsDead)
            return BaseAnimState.Dead;

        if (owner.CrowdControlHandler.IsInControlStiffness())
            return BaseAnimState.Siffness;

        if (owner.ShouldPlayAttackAnimation())
            return BaseAnimState.Attack;

        if (owner.DashMotor != null && owner.DashMotor.IsDashing)
            return BaseAnimState.Dash;

        if (owner.LocomotionState == UnitLocomotionState.Move)
            return BaseAnimState.Move;

        return BaseAnimState.Idle;
    }

    private void ApplyBaseStateWeights()
    {
        int totalInputCount = 5 + (attackPlayables != null ? attackPlayables.Length : 0);

        for (int i = 0; i < totalInputCount; i++)
            baseMixer.SetInputWeight(i, 0f);

        int idleIndex = 0;
        int moveIndex = 1;
        int dashIndex = 2;
        int attackStartIndex = 3;
        int siffnessIndex = attackStartIndex + (attackPlayables != null ? attackPlayables.Length : 0);
        int deadIndex = siffnessIndex + 1;

        switch (currentBaseState)
        {
            case BaseAnimState.Idle:
            case BaseAnimState.Move:
                ApplyIdleMoveWeights();
                break;

            case BaseAnimState.Dash:
                baseMixer.SetInputWeight(dashIndex, 1f);
                break;

            case BaseAnimState.Attack:
                if (attackPlayables != null && attackPlayables.Length > 0)
                {
                    int clampedIndex = Mathf.Clamp(currentAttackClipIndex, 0, attackPlayables.Length - 1);
                    baseMixer.SetInputWeight(attackStartIndex + clampedIndex, 1f);
                }
                else
                {
                    // 没有攻击动画时退回 Idle，避免空播
                    baseMixer.SetInputWeight(idleIndex, 1f);
                }
                break;

            case BaseAnimState.Siffness:
                baseMixer.SetInputWeight(siffnessIndex, 1f);
                break;

            case BaseAnimState.Dead:
                baseMixer.SetInputWeight(deadIndex, 1f);
                break;
        }
    }

    private void UpdateIdleMoveBlend(float deltaTime)
    {
        if (currentBaseState != BaseAnimState.Idle && currentBaseState != BaseAnimState.Move)
            return;

        float target = 0f;
        if (baseProfile.AuthoredMoveSpeed > 0.001f)
        {
            float currentMoveSpeed = (float)owner.Stats.MoveDistancePerSecond;
            target = Mathf.Clamp01(currentMoveSpeed / baseProfile.AuthoredMoveSpeed);
        }

        idleMoveBlend01 = Mathf.Lerp(idleMoveBlend01, target, deltaTime * baseProfile.IdleMoveBlendLerp);
        ApplyIdleMoveWeights();
    }

    private void ApplyIdleMoveWeights()
    {
        if (currentBaseState != BaseAnimState.Idle && currentBaseState != BaseAnimState.Move)
            return;

        int idleIndex = 0;
        int moveIndex = 1;

        baseMixer.SetInputWeight(idleIndex, 1f - idleMoveBlend01);
        baseMixer.SetInputWeight(moveIndex, idleMoveBlend01);
    }

    private void UpdateBasePlaybackSpeed()
    {
        if (baseProfile == null || owner == null)
            return;

        if (movePlayable.IsValid() && baseProfile.AuthoredMoveSpeed > 0.001f)
        {
            float currentMoveSpeed = Mathf.Max(0f, (float)owner.Stats.MoveDistancePerSecond);
            float moveSpeed = currentMoveSpeed / baseProfile.AuthoredMoveSpeed;
            movePlayable.SetSpeed(Mathf.Max(0.05f, moveSpeed));
        }

        if (attackPlayables != null && attackPlayables.Length > 0 && baseProfile.AuthoredAttackSpeed > 0.001f)
        {
            float attackSpeed = Mathf.Max(0.01f, (float)owner.Stats.Get(UnitStatType.AttackSpeed));
            float animSpeed = attackSpeed / baseProfile.AuthoredAttackSpeed;
            float finalSpeed = Mathf.Max(0.05f, animSpeed);

            for (int i = 0; i < attackPlayables.Length; i++)
            {
                if (attackPlayables[i].IsValid())
                    attackPlayables[i].SetSpeed(finalSpeed);
            }
        }

        if (idlePlayable.IsValid()) idlePlayable.SetSpeed(1f);
        if (dashPlayable.IsValid()) dashPlayable.SetSpeed(1f);
        if (siffnessPlayable.IsValid()) siffnessPlayable.SetSpeed(1f);
        if (deadPlayable.IsValid()) deadPlayable.SetSpeed(1f);
    }

    private void AdvanceAttackClipIndex()
    {
        if (attackPlayables == null || attackPlayables.Length == 0)
        {
            currentAttackClipIndex = 0;
            return;
        }

        currentAttackClipIndex++;
        if (currentAttackClipIndex >= attackPlayables.Length)
            currentAttackClipIndex = 0;
    }
    #endregion

    #region Override / State Lifetime

    private void UpdateOverrideLifetime(fp deltaTime)
    {
        if (!overrideRecord.IsValid)
            return;

        if (overrideRecord.Request.Loop)
            return;

        overrideRecord.RemainingDuration -= deltaTime;
        if (overrideRecord.RemainingDuration <= fp.zero)
            StopFullBodyOverride();
    }

    private void UpdateStateLifetime(fp deltaTime)
    {
        List<int> expired = null;

        for (int i = 0; i < stateRecords.Count; i++)
        {
            var record = stateRecords[i];
            if (!record.IsValid)
                continue;

            if (record.Request.Loop)
                continue;

            record.RemainingDuration -= deltaTime;
            stateRecords[i] = record;

            if (record.RemainingDuration <= fp.zero)
            {
                expired ??= new List<int>();
                expired.Add(record.RuntimeId);
            }
        }

        if (expired == null)
            return;

        for (int i = 0; i < expired.Count; i++)
            StopStateLayer(expired[i]);
    }

    private fp ComputeDuration(OverlayAnimRequest request)
    {
        if (request.Loop || request.ClipRef == null || request.ClipRef.Clip == null)
            return fp.zero;

        float speed = request.Speed <= 0f ? 1f : request.Speed;
        return (fp)(request.ClipRef.Clip.length / Mathf.Max(0.01f, speed));
    }

    #endregion

    #region Init

    private void BuildGraph()
    {
        if (animator == null)
        {
            Debug.LogError($"[{nameof(UnitAnimationController)}] 缺少 Animator: {name}");
            return;
        }

        int totalLayerCount = Layer_StateStart + Mathf.Max(1, maxStateLayers);

        graph = PlayableGraph.Create($"{name}_AnimationGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        layerMixer = AnimationLayerMixerPlayable.Create(graph, totalLayerCount);

        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        output.SetSourcePlayable(layerMixer);

        BuildBaseLayer();
        BuildOverrideLayer();
        graph.Play();
    }

    private void BuildBaseLayer()
    {
        var idleClip = ResolveBaseClip(BaseAnimOverrideType.Idle, baseProfile != null ? baseProfile.IdleClip : null);
        var moveClip = ResolveBaseClip(BaseAnimOverrideType.Move, baseProfile != null ? baseProfile.MoveClip : null);
        var dashClip = ResolveBaseClip(BaseAnimOverrideType.Dash, baseProfile != null ? baseProfile.DashClip : null);
        var siffnessClip = ResolveBaseClip(BaseAnimOverrideType.Siffness, baseProfile != null ? baseProfile.SiffnessClip : null);
        var deadClip = ResolveBaseClip(BaseAnimOverrideType.Dead, baseProfile != null ? baseProfile.DeadClip : null);
        var resolvedAttackClips = ResolveAttackClips();

        int attackCount = resolvedAttackClips != null ? resolvedAttackClips.Length : 0;
        int totalInputCount = 5 + attackCount;

        baseMixer = AnimationMixerPlayable.Create(graph, totalInputCount);

        int idleIndex = 0;
        int moveIndex = 1;
        int dashIndex = 2;
        int attackStartIndex = 3;
        int siffnessIndex = attackStartIndex + attackCount;
        int deadIndex = siffnessIndex + 1;

        idlePlayable = CreateClipPlayable(idleClip, true);
        movePlayable = CreateClipPlayable(moveClip, true);
        dashPlayable = CreateClipPlayable(dashClip, true);

        attackPlayables = new AnimationClipPlayable[attackCount];
        for (int i = 0; i < attackCount; i++)
            attackPlayables[i] = CreateClipPlayable(resolvedAttackClips[i], true);

        siffnessPlayable = CreateClipPlayable(siffnessClip, true);
        deadPlayable = CreateClipPlayable(deadClip, false);

        graph.Connect(idlePlayable, 0, baseMixer, idleIndex);
        graph.Connect(movePlayable, 0, baseMixer, moveIndex);
        graph.Connect(dashPlayable, 0, baseMixer, dashIndex);

        for (int i = 0; i < attackPlayables.Length; i++)
            graph.Connect(attackPlayables[i], 0, baseMixer, attackStartIndex + i);

        graph.Connect(siffnessPlayable, 0, baseMixer, siffnessIndex);
        graph.Connect(deadPlayable, 0, baseMixer, deadIndex);

        for (int i = 0; i < totalInputCount; i++)
            baseMixer.SetInputWeight(i, 0f);

        layerMixer.ConnectInput(Layer_Base, baseMixer, 0);
        layerMixer.SetInputWeight(Layer_Base, 1f);
    }

    private void RebuildBaseLayerRuntime()
    {
        if (!graph.IsValid())
            return;

        // 先断开旧 Base Layer
        if (layerMixer.IsValid())
            layerMixer.DisconnectInput(Layer_Base);

        if (baseMixer.IsValid())
            baseMixer.Destroy();

        if (idlePlayable.IsValid()) idlePlayable.Destroy();
        if (movePlayable.IsValid()) movePlayable.Destroy();
        if (dashPlayable.IsValid()) dashPlayable.Destroy();
        if (siffnessPlayable.IsValid()) siffnessPlayable.Destroy();
        if (deadPlayable.IsValid()) deadPlayable.Destroy();

        if (attackPlayables != null)
        {
            for (int i = 0; i < attackPlayables.Length; i++)
            {
                if (attackPlayables[i].IsValid())
                    attackPlayables[i].Destroy();
            }
        }

        attackPlayables = Array.Empty<AnimationClipPlayable>();

        BuildBaseLayer();
        ApplyBaseStateWeights();
        UpdateBasePlaybackSpeed();
    }

    private void BuildOverrideLayer()
    {
        overrideSlot = new OverrideRuntimeSlot(graph, layerMixer, Layer_Override);
    }

    private StateRuntimeSlot AllocateOrReuseStateSlot()
    {
        for (int i = Layer_StateStart; i < Layer_StateStart + maxStateLayers; i++)
        {
            bool used = false;
            for (int j = 0; j < stateSlots.Count; j++)
            {
                if (stateSlots[j].LayerIndex == i)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
                return new StateRuntimeSlot(graph, layerMixer, i);
        }

        // 达到上限时，挤掉优先级最低的
        if (stateSlots.Count > 0)
        {
            stateSlots.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            var reused = stateSlots[0];
            reused.ClearImmediate();
            stateSlots.RemoveAt(0);
            return reused;
        }

        return new StateRuntimeSlot(graph, layerMixer, Layer_StateStart);
    }

    private AnimationClipPlayable CreateClipPlayable(AnimationClip clip, bool loop)
    {
        if (clip == null)
            return default;

        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(1f);
        playable.SetTime(0);
        return playable;
    }

    #endregion

    #region Weights / Sort

    private void RebuildStateLayerWeightsAndOrder()
    {
        stateSlots.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        for (int i = 0; i < stateSlots.Count; i++)
        {
            stateSlots[i].ApplyToLayerMixer();
        }
    }

    #endregion

    #region Preset

    private void ApplyPresetDefaults(ref OverlayAnimRequest request, bool isOverride)
    {
        switch (request.Preset)
        {
            case OverlayAnimPreset.FullBodyOverride_Default:
                request.Mask = null;
                request.Additive = false;
                request.Weight = request.Weight <= 0 ? 1f : request.Weight;
                request.FadeIn = request.FadeIn <= 0 ? baseProfile.OverlayDefaultFadeIn : request.FadeIn;
                request.FadeOut = request.FadeOut <= 0 ? baseProfile.OverlayDefaultFadeOut : request.FadeOut;
                request.Speed = request.Speed <= 0 ? 1f : request.Speed;
                request.AutoStop = true;
                request.Loop = false;
                break;

            case OverlayAnimPreset.UpperBodyCast_Default:
                request.Mask = request.Mask != null ? request.Mask : null;
                request.Additive = false;
                request.Weight = request.Weight <= 0 ? 1f : request.Weight;
                request.FadeIn = request.FadeIn <= 0 ? baseProfile.OverlayDefaultFadeIn : request.FadeIn;
                request.FadeOut = request.FadeOut <= 0 ? baseProfile.OverlayDefaultFadeOut : request.FadeOut;
                request.Speed = request.Speed <= 0 ? 1f : request.Speed;
                request.AutoStop = true;
                request.Loop = false;
                break;

            case OverlayAnimPreset.BuffLoop_Default:
                request.Additive = true;
                request.Loop = true;
                request.Weight = request.Weight <= 0 ? 1f : request.Weight;
                request.FadeIn = request.FadeIn <= 0 ? baseProfile.OverlayDefaultFadeIn : request.FadeIn;
                request.FadeOut = request.FadeOut <= 0 ? baseProfile.OverlayDefaultFadeOut : request.FadeOut;
                request.Speed = request.Speed <= 0 ? 1f : request.Speed;
                request.AutoStop = false;
                break;

            case OverlayAnimPreset.HitReact_Default:
                request.Additive = false;
                request.Weight = request.Weight <= 0 ? 1f : request.Weight;
                request.FadeIn = request.FadeIn <= 0 ? 0.04f : request.FadeIn;
                request.FadeOut = request.FadeOut <= 0 ? 0.08f : request.FadeOut;
                request.Speed = request.Speed <= 0 ? 1f : request.Speed;
                request.AutoStop = true;
                request.Loop = false;
                break;

            case OverlayAnimPreset.Custom:
            default:
                request.Weight = request.Weight <= 0 ? 1f : request.Weight;
                request.FadeIn = request.FadeIn <= 0 ? baseProfile.OverlayDefaultFadeIn : request.FadeIn;
                request.FadeOut = request.FadeOut <= 0 ? baseProfile.OverlayDefaultFadeOut : request.FadeOut;
                request.Speed = request.Speed <= 0 ? 1f : request.Speed;
                break;
        }

        if (isOverride)
        {
            request.Mask = null;
            request.Additive = false;
        }
    }

    #endregion

    #region Runtime Slot Classes

    private abstract class RuntimeSlotBase
    {
        protected readonly PlayableGraph graph;
        protected readonly AnimationLayerMixerPlayable layerMixer;
        protected readonly AnimationMixerPlayable mixer;

        protected AnimationClipPlayable currentPlayable;
        protected float currentWeight;
        protected float targetWeight;
        protected float fadeInSpeed;
        protected float fadeOutSpeed;
        protected bool stopping;

        public int LayerIndex { get; }

        protected RuntimeSlotBase(PlayableGraph graph, AnimationLayerMixerPlayable layerMixer, int layerIndex)
        {
            this.graph = graph;
            this.layerMixer = layerMixer;
            LayerIndex = layerIndex;

            mixer = AnimationMixerPlayable.Create(graph, 1);
            layerMixer.ConnectInput(layerIndex, mixer, 0);
            layerMixer.SetInputWeight(layerIndex, 0f);
        }

        public void Tick(float deltaTime)
        {
            if (!currentPlayable.IsValid())
                return;

            if (!stopping)
            {
                currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, fadeInSpeed * deltaTime);
                layerMixer.SetInputWeight(LayerIndex, currentWeight);
            }
            else
            {
                currentWeight = Mathf.MoveTowards(currentWeight, 0f, fadeOutSpeed * deltaTime);
                layerMixer.SetInputWeight(LayerIndex, currentWeight);

                if (currentWeight <= 0.001f)
                    ClearImmediate();
            }
        }

        protected void PlayCore(OverlayAnimRequest request, float startTime)
        {
            ClearImmediate();

            if (request.ClipRef == null || request.ClipRef.Clip == null)
                return;

            currentPlayable = AnimationClipPlayable.Create(graph, request.ClipRef.Clip);
            currentPlayable.SetApplyFootIK(false);
            currentPlayable.SetApplyPlayableIK(false);
            currentPlayable.SetSpeed(request.Speed <= 0f ? 1f : request.Speed);
            currentPlayable.SetTime(startTime);

            graph.Connect(currentPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);

            currentWeight = 0f;
            targetWeight = Mathf.Clamp01(request.Weight <= 0 ? 1f : request.Weight);

            float fadeIn = request.FadeIn > 0 ? request.FadeIn : 0.08f;
            float fadeOut = request.FadeOut > 0 ? request.FadeOut : 0.08f;

            fadeInSpeed = fadeIn > 0.0001f ? targetWeight / fadeIn : targetWeight * 999f;
            fadeOutSpeed = fadeOut > 0.0001f ? targetWeight / fadeOut : targetWeight * 999f;

            stopping = false;
            layerMixer.SetInputWeight(LayerIndex, 0f);
        }

        public void ClearImmediate()
        {
            if (currentPlayable.IsValid())
            {
                mixer.DisconnectInput(0);
                currentPlayable.Destroy();
            }

            currentPlayable = default;
            currentWeight = 0f;
            targetWeight = 0f;
            fadeInSpeed = 0f;
            fadeOutSpeed = 0f;
            stopping = false;

            layerMixer.SetInputWeight(LayerIndex, 0f);
        }
    }

    private sealed class OverrideRuntimeSlot : RuntimeSlotBase
    {
        public OverrideRuntimeSlot(PlayableGraph graph, AnimationLayerMixerPlayable layerMixer, int layerIndex)
            : base(graph, layerMixer, layerIndex)
        {
        }

        public void PlayImmediate(OverlayAnimRequest request)
        {
            layerMixer.SetLayerAdditive((uint)LayerIndex, false);
            PlayCore(request, 0f);
        }

        public void PlayRebuilt(OverlayAnimRequest request, float elapsed)
        {
            layerMixer.SetLayerAdditive((uint)LayerIndex, false);

            float speed = request.Speed <= 0f ? 1f : request.Speed;
            float clipLen = request.ClipRef.Clip.length;
            float playableTime = elapsed * speed;

            if (request.Loop && clipLen > 0.0001f)
                playableTime %= clipLen;
            else
                playableTime = Mathf.Min(playableTime, clipLen);

            PlayCore(request, playableTime);
            currentWeight = targetWeight;
            layerMixer.SetInputWeight(LayerIndex, currentWeight);
        }
    }

    private sealed class StateRuntimeSlot : RuntimeSlotBase
    {
        public int RuntimeId { get; private set; } = -1;
        public int Priority { get; private set; }
        public string Tag { get; private set; }
        private AvatarMask mask;
        private bool additive;

        public StateRuntimeSlot(PlayableGraph graph, AnimationLayerMixerPlayable layerMixer, int layerIndex)
            : base(graph, layerMixer, layerIndex)
        {
        }

        public void Bind(int runtimeId, OverlayAnimRequest request)
        {
            RuntimeId = runtimeId;
            Priority = request.Priority;
            Tag = request.Tag;
            mask = request.Mask;
            additive = request.Additive;
        }

        public void PlayImmediate(OverlayAnimRequest request)
        {
            ApplyToLayerMixer();
            PlayCore(request, 0f);
        }

        public void PlayRebuilt(OverlayAnimRequest request, float elapsed)
        {
            ApplyToLayerMixer();

            float speed = request.Speed <= 0f ? 1f : request.Speed;
            float clipLen = request.ClipRef.Clip.length;
            float playableTime = elapsed * speed;

            if (request.Loop && clipLen > 0.0001f)
                playableTime %= clipLen;
            else
                playableTime = Mathf.Min(playableTime, clipLen);

            PlayCore(request, playableTime);
            currentWeight = targetWeight;
            layerMixer.SetInputWeight(LayerIndex, currentWeight);
        }

        public void ApplyToLayerMixer()
        {
            layerMixer.SetLayerAdditive((uint)LayerIndex, additive);

            if (mask != null)
                layerMixer.SetLayerMaskFromAvatarMask((uint)LayerIndex, mask);
        }

        public new void ClearImmediate()
        {
            base.ClearImmediate();
            RuntimeId = -1;
            Priority = 0;
            Tag = null;
            mask = null;
            additive = false;
        }
    }

    #endregion
}