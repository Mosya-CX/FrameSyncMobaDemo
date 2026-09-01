using FrameSyncMoba.Unit;
using FrameSyncMoba.Presentation;
using System.Collections.Generic;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    [DefaultExecutionOrder(1000)]
    public sealed class UnitAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private UnitPresentationHost _host;
        private UnitAnimationProfile _profile;
        private readonly HashSet<int> _parameterHashes = new HashSet<int>();
        private bool _wasAttacking;
        private int _lastAttackSequence = int.MinValue;
        private int _lastAttackStartLogicTick = int.MinValue;
        private int _lastAbilityId;
        private int _lastAbilityStage = -1;
        private LifeState _lastLifeState = (LifeState)byte.MaxValue;
        private bool _wasMoving;
        private bool _wasCasting;
        private bool _hasDrivenFrame;
        private bool _wasPassiveReady;
        private bool _wasAnimationVariantActive;
        private readonly ConfigurableAnimationProgressSampler
            _attackProgressSampler =
                new ConfigurableAnimationProgressSampler();
        private readonly ConfigurableAnimationProgressSampler
            _loopProgressSampler =
                new ConfigurableAnimationProgressSampler();
        private readonly LoopAnimationPhaseTracker
            _loopPhaseTracker =
                new LoopAnimationPhaseTracker();
        private readonly List<AnimatorClipInfo> _loopClipInfos =
            new List<AnimatorClipInfo>(2);

        private void Awake()
        {
            _host = GetComponent<UnitPresentationHost>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            _profile = _host != null ? _host.Profile : null;
            CacheAnimatorParameters();
        }

        private void Update()
        {
            if (_host == null ||
                _host.OwnerUnit == null ||
                _host.OwnerUnit.World == null ||
                _animator == null)
                return;

            UnitType unit = _host.OwnerUnit;
            if (_profile == null)
                _profile = _host.Profile;
            bool lifeStateChanged =
                unit.LifeState != _lastLifeState;
            bool isDead = unit.LifeState == LifeState.Dead
                       || unit.LifeState == LifeState.Respawning;
            SetInteger(
                HashOrDefault(
                    _profile?.LifeStateHash ?? 0,
                    "LifeState"),
                (int)unit.LifeState);
            SetBool(
                HashOrDefault(
                    _profile?.IsControlledHash ?? 0,
                    "IsControlled"),
                unit.ControlledByPlayerSlot >= 0);

            if (lifeStateChanged)
            {
                // LifeState parameter already drives the Animator transitions
                // (AnyState -> Death, Death -> Idle on respawn). No code-side
                // CrossFade is needed.
                _lastLifeState = unit.LifeState;
            }

            if (isDead)
            {
                SetBool(HashOrDefault(_profile?.IsMovingHash ?? 0, "IsMoving"), false);
                SetFloat(HashOrDefault(_profile?.MoveSpeedHash ?? 0, "MoveSpeed"), 0f);
                SetBool(HashOrDefault(_profile?.IsAttackingHash ?? 0, "IsAttacking"), false);
                SetBool(HashOrDefault(_profile?.IsAttackRecoveringHash ?? 0, "IsAttackRecovering"), false);
                SetBool(HashOrDefault(_profile?.IsEmpoweredAttackHash ?? 0, "IsEmpoweredAttack"), false);
                SetBool(HashOrDefault(_profile?.IsCastingHash ?? 0, "IsCasting"), false);
                SetBool(HashOrDefault(_profile?.IsPassiveReadyHash ?? 0, "IsPassiveReady"), false);
                SetBool(HashOrDefault(_profile?.IsAnimationVariantActiveHash ?? 0, "IsAnimationVariantActive"), false);
                _wasMoving = false;
                _wasCasting = false;
                _wasAttacking = false;
                _wasPassiveReady = false;
                _wasAnimationVariantActive = false;
                _attackProgressSampler.Clear();
                _loopProgressSampler.Clear();
                _loopPhaseTracker.Clear();
                _hasDrivenFrame = true;
                return;
            }

            var movement = unit.MovementHandler;
            bool isMoving = false;
            float moveSpeed = 0f;
            if (movement != null)
            {
                isMoving = movement.IsMoving;
                // Keep the stat-derived playback speed while Idle/Walk is
                // transitioning. IsMoving selects the state; zeroing the
                // multiplier on the stop Tick would first snap the still-
                // current Walk clip to its epoch origin before Animator can
                // blend into Idle. Instantaneous collision/RVO velocity is
                // deliberately not used because it can wobble per Tick.
                moveSpeed =
                    (float)movement.LogicMoveSpeed;
            }
            SetBool(HashOrDefault(_profile?.IsMovingHash ?? 0, "IsMoving"), isMoving);
            SetFloat(HashOrDefault(_profile?.MoveSpeedHash ?? 0, "MoveSpeed"), moveSpeed);

            var abilityHandler = unit.AbilityHandler;
            AnimationPresentationTime animationTime =
                ResolveAnimationPresentationTime(unit);
            int currentLogicTick = System.Math.Max(
                0,
                animationTime.CompletedLogicTick);
            bool isPassiveReady =
                _profile != null &&
                abilityHandler != null &&
                abilityHandler.IsFixedPassiveReady(
                    _profile.PassiveReadyAbilityId,
                    currentLogicTick);
            bool isAnimationVariantActive =
                _profile != null &&
                _profile.AnimationVariantBuffConfigId > 0 &&
                unit.BuffHandler != null &&
                unit.BuffHandler.HasBuff(
                    new BuffConfigId(
                        _profile.AnimationVariantBuffConfigId));
            SetBool(
                HashOrDefault(
                    _profile?.IsPassiveReadyHash ?? 0,
                    "IsPassiveReady"),
                isPassiveReady);
            SetBool(
                HashOrDefault(
                    _profile?.IsAnimationVariantActiveHash ?? 0,
                    "IsAnimationVariantActive"),
                isAnimationVariantActive);
            if (_hasDrivenFrame &&
                _wasAnimationVariantActive &&
                !isAnimationVariantActive)
            {
                SetTrigger(HashOrDefault(
                    _profile?.AnimationVariantExitHash ?? 0,
                    "AnimationVariantExit"));
            }

            var attack = unit.AttackHandler;
            bool isAttacking = false;
            int attackSequence = 0;
            bool isRecovering = false;
            float attackMotionTime = 0f;
            bool isEmpoweredAttack = false;
            int attackStartLogicTick = -1;
            if (attack != null)
            {
                var attackAnim =
                    attack.GetAnimationSnapshot();
                isAttacking = attackAnim.IsAttacking;
                attackStartLogicTick =
                    attackAnim.AttackStartLogicTick;
                attackSequence = attackAnim.SequenceIndex;
                isEmpoweredAttack =
                    attackAnim.IsEmpoweredAttack;
                if (attackAnim.ImpactCommitted)
                {
                    isRecovering = isAttacking;
                }
                float samplingRate =
                    UnitAnimationSynchronizationSettings
                        .SynchronizationRateHz;
                double interval = 1d / samplingRate;
                double nowLogicTicks =
                    animationTime.LogicTimeTicks;
                double nextLogicTicks =
                    nowLogicTicks +
                    interval * animationTime.TickRate;
                if (!attackAnim.ImpactCommitted &&
                    attackAnim.ImpactLogicTick >= 0)
                {
                    nextLogicTicks = System.Math.Min(
                        nextLogicTicks,
                        attackAnim.ImpactLogicTick);
                }
                if (attackAnim.NextAttackReadyLogicTick >= 0)
                {
                    nextLogicTicks = System.Math.Min(
                        nextLogicTicks,
                        attackAnim.NextAttackReadyLogicTick);
                }
                float attackProgressNow =
                    EvaluateAttackMotionTime(
                        attackAnim,
                        nowLogicTicks);
                float attackProgressNext =
                    EvaluateAttackMotionTime(
                        attackAnim,
                        nextLogicTicks);
                attackMotionTime =
                    _attackProgressSampler.Sample(
                        animationTime.LogicTimeSeconds,
                        attackProgressNow,
                        attackProgressNext,
                        BuildAttackSamplingStateKey(
                            attackAnim),
                        false,
                        samplingRate,
                        UnitAnimationSynchronizationSettings
                            .InterpolateProgress,
                        System.Math.Max(
                            0d,
                            (nextLogicTicks - nowLogicTicks) /
                            animationTime.TickRate));
            }
            else
            {
                _attackProgressSampler.Clear();
            }
            SetBool(HashOrDefault(_profile?.IsAttackingHash ?? 0, "IsAttacking"), isAttacking);
            SetBool(HashOrDefault(_profile?.IsAttackRecoveringHash ?? 0, "IsAttackRecovering"), isRecovering);
            SetBool(HashOrDefault(_profile?.IsEmpoweredAttackHash ?? 0, "IsEmpoweredAttack"), isEmpoweredAttack);
            int attackAnimationVariant =
                ResolveAttackAnimationVariant(
                    attackSequence,
                    _profile?.AttackStateHashes?.Length ?? 0);
            SetInteger(
                HashOrDefault(
                    _profile?.AttackSequenceIndexHash ?? 0,
                    "AttackSequenceIndex"),
                attackAnimationVariant);
            SetFloat(HashOrDefault(_profile?.AttackMotionTimeHash ?? 0, "AttackMotionTime"), attackMotionTime);

            if (isAttacking &&
                (!_wasAttacking ||
                 attackStartLogicTick !=
                    _lastAttackStartLogicTick))
            {
                SetTrigger(HashOrDefault(_profile?.AttackStartHash ?? 0, "AttackStart"));
                _lastAttackSequence = attackSequence;
                _lastAttackStartLogicTick =
                    attackStartLogicTick;
            }
            bool attackEnded =
                _wasAttacking &&
                !isAttacking;

            bool isCasting = false;
            int abilityId = 0;
            int abilityStage = -1;
            int stageElapsedTicks = 0;
            if (abilityHandler != null)
            {
                var activeCasts = abilityHandler.ActiveCasts;
                if (activeCasts != null && activeCasts.Count > 0)
                {
                    // Prefer the first cast entry that actually has an
                    // animation binding. Persistent toggles (e.g. Varus W)
                    // sit in ActiveCasts without a binding and must not
                    // shadow a real cast (E/R/Q release).
                    ActiveAbilityCastInfo? selected = null;
                    for (int ci = 0;
                         ci < activeCasts.Count;
                         ci++)
                    {
                        var candidate = activeCasts[ci];
                        StageAnimationBinding binding =
                            default;
                        bool hasBinding =
                            _profile != null &&
                            _profile.TryGetStageBinding(
                                candidate.AbilityId,
                                candidate.StageKey,
                                out binding);
                        if (_profile == null ||
                            hasBinding)
                        {
                            selected = candidate;
                            break;
                        }
                    }
                    if (selected.HasValue)
                    {
                        isCasting = true;
                        abilityId = selected.Value.AbilityId;
                        abilityStage = selected.Value.StageKey;
                        stageElapsedTicks =
                            selected.Value.StageElapsedTicks;
                    }
                }
            }
            SetBool(HashOrDefault(_profile?.IsCastingHash ?? 0, "IsCasting"), isCasting);
            SetFloat(
                HashOrDefault(
                    _profile?.AbilityStageProgressHash ?? 0,
                    "AbilityStageProgress"),
                stageElapsedTicks);
            bool abilityStageChanged =
                isCasting &&
                (abilityId != _lastAbilityId ||
                 abilityStage != _lastAbilityStage);
            if (abilityStageChanged)
            {
                // Ability casts keep the profile stage binding (heroes use
                // named states today); minions have no casts.
                PlayAbilityStage(
                    abilityId,
                    abilityStage,
                    isPassiveReady,
                    isAnimationVariantActive);
                _lastAbilityId = abilityId;
                _lastAbilityStage = abilityStage;
            }
            else if (!isCasting)
            {
                _lastAbilityId = 0;
                _lastAbilityStage = -1;
            }

            bool locomotionRoutingChanged =
                !isAttacking &&
                !abilityStageChanged &&
                (!_hasDrivenFrame ||
                 lifeStateChanged ||
                 isMoving != _wasMoving ||
                 (!isCasting &&
                  (isPassiveReady != _wasPassiveReady ||
                   isAnimationVariantActive !=
                       _wasAnimationVariantActive ||
                   _wasAttacking ||
                   _wasCasting)));
            if (locomotionRoutingChanged &&
                _animator.isActiveAndEnabled)
            {
                // Parameters normally resolve during Animator's later update.
                // Refresh parameter-driven routing with zero duration. A newly
                // selected ability stage already routes through CrossFade, so
                // it must not receive a second zero-time evaluation. Movement
                // changes during an existing movable cast still need this
                // refresh (for example Varus Q idle <-> walk channel loops) so
                // the correct loop is sampled on the same rendered frame.
                _animator.Update(0f);
            }

            float loopMotionTime = ResolveLoopMotionTime(
                animationTime,
                isAttacking,
                moveSpeed);
            SetFloat(
                HashOrDefault(
                    _profile?.LoopMotionTimeHash ?? 0,
                    "LoopMotionTime"),
                loopMotionTime);

            if (!isAttacking &&
                !isCasting &&
                (!_hasDrivenFrame ||
                 attackEnded ||
                 _wasCasting ||
                 isMoving != _wasMoving))
            {
                // IsMoving / MoveSpeed parameters drive Idle <-> Move.
            }
            _wasMoving = isMoving;
            _wasCasting = isCasting;
            _wasAttacking = isAttacking;
            _wasPassiveReady = isPassiveReady;
            _wasAnimationVariantActive = isAnimationVariantActive;
            _hasDrivenFrame = true;
        }

        public static float EvaluateAttackMotionTime(
            in AttackAnimationSnapshot snapshot,
            double logicTimeTicks,
            float impactNormalizedTime = 0.5f)
        {
            float impactTime = Mathf.Clamp01(
                impactNormalizedTime);
            if (snapshot.AttackStartLogicTick < 0 ||
                snapshot.ImpactLogicTick <
                    snapshot.AttackStartLogicTick ||
                snapshot.NextAttackReadyLogicTick <
                    snapshot.ImpactLogicTick)
            {
                return 0f;
            }

            if (logicTimeTicks <= snapshot.AttackStartLogicTick)
                return 0f;
            if (logicTimeTicks < snapshot.ImpactLogicTick)
            {
                int windupTicks =
                    snapshot.ImpactLogicTick -
                    snapshot.AttackStartLogicTick;
                if (windupTicks <= 0)
                    return impactTime;
                return impactTime * Mathf.Clamp01((float)(
                    (logicTimeTicks -
                     snapshot.AttackStartLogicTick) /
                    windupTicks));
            }

            int recoveryTicks =
                snapshot.NextAttackReadyLogicTick -
                snapshot.ImpactLogicTick;
            if (recoveryTicks <= 0 ||
                logicTimeTicks >=
                    snapshot.NextAttackReadyLogicTick)
            {
                return 1f;
            }

            float recoveryProgress = Mathf.Clamp01((float)(
                (logicTimeTicks - snapshot.ImpactLogicTick) /
                recoveryTicks));
            return impactTime +
                (1f - impactTime) * recoveryProgress;
        }

        private static int BuildAttackSamplingStateKey(
            in AttackAnimationSnapshot snapshot)
        {
            unchecked
            {
                int hash = snapshot.AttackStartLogicTick;
                hash = hash * 397 ^ snapshot.SequenceIndex;
                hash = hash * 397 ^
                    (snapshot.ImpactCommitted ? 1 : 0);
                hash = hash * 397 ^
                    (snapshot.IsAttacking ? 1 : 0);
                return hash;
            }
        }

        private static AnimationPresentationTime
            ResolveAnimationPresentationTime(
                UnitType unit)
        {
            int tickRate = unit.World.TickRate;
            if (tickRate <= 0)
            {
                throw new System.InvalidOperationException(
                    "UnitWorld TickRate must be positive before " +
                    "animation presentation can be sampled.");
            }
            if (AnimationPresentationClock.TryGetCurrent(
                    unit.World,
                    out AnimationPresentationTime current) &&
                current.TickRate == tickRate)
            {
                return current;
            }

            return new AnimationPresentationTime(
                -1,
                tickRate,
                0d);
        }

        private float ResolveLoopMotionTime(
            in AnimationPresentationTime animationTime,
            bool isAttacking,
            float moveSpeed)
        {
            if (isAttacking ||
                !TryGetLoopClip(
                    out AnimatorStateInfo stateInfo,
                    out AnimationClip clip,
                    out bool isWalkClip))
            {
                _loopProgressSampler.Clear();
                _loopPhaseTracker.Clear();
                return 0f;
            }

            double playbackRate = System.Math.Max(
                0d,
                stateInfo.speed *
                (isWalkClip ? moveSpeed : 1f));
            double cyclesPerSecond = clip.length > 0.000001f
                ? playbackRate / clip.length
                : 0d;
            int loopStateKey = stateInfo.fullPathHash;
            double nowSeconds = animationTime.LogicTimeSeconds;
            bool loopAnchorChanged = _loopPhaseTracker.Observe(
                loopStateKey,
                nowSeconds,
                cyclesPerSecond);
            if (loopAnchorChanged)
                _loopProgressSampler.Clear();

            float samplingRate =
                UnitAnimationSynchronizationSettings
                    .SynchronizationRateHz;
            double interval = 1d / samplingRate;
            float phaseNow = (float)_loopPhaseTracker
                .EvaluateUnwrapped(nowSeconds);
            float phaseNext = (float)_loopPhaseTracker
                .EvaluateUnwrapped(nowSeconds + interval);
            return _loopProgressSampler.Sample(
                nowSeconds,
                phaseNow,
                phaseNext,
                loopStateKey,
                true,
                samplingRate,
                UnitAnimationSynchronizationSettings
                    .InterpolateProgress);
        }

        private bool TryGetLoopClip(
            out AnimatorStateInfo stateInfo,
            out AnimationClip clip,
            out bool isWalkClip)
        {
            bool useNext = _animator.IsInTransition(0);
            stateInfo = useNext
                ? _animator.GetNextAnimatorStateInfo(0)
                : _animator.GetCurrentAnimatorStateInfo(0);
            _loopClipInfos.Clear();
            if (useNext)
                _animator.GetNextAnimatorClipInfo(
                    0,
                    _loopClipInfos);
            else
                _animator.GetCurrentAnimatorClipInfo(
                    0,
                    _loopClipInfos);

            clip = null;
            float bestWeight = float.MinValue;
            for (int i = 0; i < _loopClipInfos.Count; i++)
            {
                AnimatorClipInfo info = _loopClipInfos[i];
                if (info.clip != null && info.weight > bestWeight)
                {
                    clip = info.clip;
                    bestWeight = info.weight;
                }
            }

            if (clip == null || !clip.isLooping)
            {
                isWalkClip = false;
                return false;
            }

            string clipName = clip.name;
            isWalkClip = clipName.IndexOf(
                "Walk",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isIdleClip = clipName.IndexOf(
                "Idle",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            return isWalkClip || isIdleClip;
        }

        private void CacheAnimatorParameters()
        {
            _parameterHashes.Clear();
            if (_animator == null)
                return;
            AnimatorControllerParameter[] parameters =
                _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                _parameterHashes.Add(
                    parameters[i].nameHash);
        }

        private void PlayAbilityStage(
            int abilityId,
            int stageIndex,
            bool isPassiveReady,
            bool isAnimationVariantActive)
        {
            if (_profile == null ||
                !_profile.TryGetStageBinding(
                    abilityId,
                    stageIndex,
                    out StageAnimationBinding binding) ||
                binding.StateNameHash == 0)
                return;
            int stateNameHash =
                isAnimationVariantActive &&
                binding.AnimationVariantStateNameHash != 0
                    ? binding.AnimationVariantStateNameHash
                    : isPassiveReady &&
                      binding.PassiveReadyStateNameHash != 0
                        ? binding.PassiveReadyStateNameHash
                        : binding.StateNameHash;
            _animator.CrossFade(
                stateNameHash,
                0.04f,
                0,
                binding.StartNormalizedTime);
        }

        private static int HashOrDefault(
            int configuredHash,
            string defaultName)
        {
            return configuredHash != 0
                ? configuredHash
                : Animator.StringToHash(defaultName);
        }

        /// <summary>
        /// Maps the deterministic, monotonically cycling Gameplay attack
        /// sequence onto the finite presentation variants authored by the
        /// unit animation profile.
        /// </summary>
        public static int ResolveAttackAnimationVariant(
            int attackSequence,
            int variantCount)
        {
            if (variantCount <= 0)
                return attackSequence;
            int remainder = attackSequence % variantCount;
            return remainder < 0
                ? remainder + variantCount
                : remainder;
        }

        private void SetBool(int hash, bool value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetBool(hash, value);
        }

        private void SetFloat(int hash, float value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetFloat(hash, value);
        }

        private void SetInteger(int hash, int value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetInteger(hash, value);
        }

        private void SetTrigger(int hash)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetTrigger(hash);
        }
    }
}
