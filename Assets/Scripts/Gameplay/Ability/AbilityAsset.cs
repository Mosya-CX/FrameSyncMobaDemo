using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// ScriptableObject authoring surface for AbilityDef.
    /// Designers create instances of this asset instead of
    /// writing C# code. At Bake time, the AbilityRegistryPopulator
    /// converts all AbilityAsset instances into AbilityDef entries
    /// and registers them in AbilityDefinitionRegistry.
    ///
    /// Design: moba_ability_system_design_v15_2 section 5
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewAbility",
        menuName = "FrameSyncMoba/Ability/Ability Asset")]
    public sealed class AbilityAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private int abilityId = 1;
        [SerializeField] private string abilityName = "New Ability";
        [SerializeField] private Texture2D icon;

        [Header("Casting")]
        [SerializeReference]
        [SerializeField] private CastModelAuthoring castModel = new CommitCastModelAuthoring();
        [SerializeField] private float castRange = 0f;
        [SerializeField] private AimKind aimKind;

        [Header("Timing")]
        [Min(0)]
        [SerializeField] private int cooldownTicks;

        [Header("Cost")]
        [SerializeField] private AbilityCostTiming costTiming =
            AbilityCostTiming.OnSessionStart;
        [SerializeField] private float[] castResourceCostByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] healthCostByLevel =
            Array.Empty<float>();

        [Header("Cast Conditions")]
        [SerializeReference]
        [SerializeField] private AbilityCastConditionAuthoring[]
            castConditions = Array.Empty<AbilityCastConditionAuthoring>();

        [Header("Stages")]
        [SerializeReference]
        [SerializeField] private StageDefAuthoring[] stageDefs = Array.Empty<StageDefAuthoring>();

        public int AbilityId => abilityId;
        public string AbilityName => abilityName;
        public Texture2D Icon => icon;
        public CastModelAuthoring CastModel => castModel;
        public float AuthoringCastRange => castRange;
        public AimKind AimKind => aimKind;
        public int CooldownTicks => cooldownTicks;
        public AbilityCostTiming CostTiming => costTiming;
        public float[] CastResourceCostByLevel =>
            castResourceCostByLevel;
        public float[] HealthCostByLevel => healthCostByLevel;
        public StageDefAuthoring[] Stages => stageDefs;

        public AbilityDef Bake()
        {
            if (abilityId <= 0)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' has invalid AbilityId {abilityId}.");
            if (string.IsNullOrWhiteSpace(abilityName))
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' requires a name.");
            if (cooldownTicks < 0)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' cooldown must be nonnegative.");
            if (float.IsNaN(castRange) ||
                float.IsInfinity(castRange) ||
                castRange < 0f)
            {
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' cast range must be finite and nonnegative.");
            }
            if (!Enum.IsDefined(typeof(AimKind), aimKind) ||
                !Enum.IsDefined(
                    typeof(AbilityCostTiming),
                    costTiming))
            {
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' contains an undefined enum value.");
            }
            ValidateStageAuthoring(stageDefs);

            var def = new AbilityDef
            {
                AbilityId = abilityId,
                Name = abilityName,
                CooldownTicks = cooldownTicks,
                AimKind = aimKind,
                CastRange = (Unity.Mathematics.FixedPoint.fp)castRange,
                CastModel = castModel?.Bake(stageDefs),
                CostPlan = new AbilityCostPlan(
                    BakeLevelValues(
                        castResourceCostByLevel,
                        nameof(castResourceCostByLevel)),
                    BakeLevelValues(
                        healthCostByLevel,
                        nameof(healthCostByLevel)),
                    costTiming),
                CastConditions = BakeConditions(castConditions),
            };

            if (!def.IsValid)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' baked an invalid definition.");
            return def;
        }

        private void ValidateStageAuthoring(
            StageDefAuthoring[] stages)
        {
            if (stages == null || stages.Length == 0)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' requires at least one explicit StageDef.");
            var keys =
                new System.Collections.Generic.HashSet<byte>();
            for (int i = 0; i < stages.Length; i++)
            {
                StageDefAuthoring stage = stages[i] ??
                    throw new InvalidOperationException(
                        $"AbilityAsset '{name}' stage {i} is null.");
                if (!keys.Add(stage.StageKey))
                    throw new InvalidOperationException(
                        $"AbilityAsset '{name}' has duplicate StageKey {stage.StageKey}.");
                if (string.IsNullOrWhiteSpace(stage.DebugName))
                    throw new InvalidOperationException(
                        $"AbilityAsset '{name}' stage {i} requires a debug name.");
                StageDef baked = stage.Bake() ??
                    throw new InvalidOperationException(
                        $"AbilityAsset '{name}' stage {i} baked null.");
                if (baked.StageDefId != stage.StageKey)
                    throw new InvalidOperationException(
                        $"AbilityAsset '{name}' stage {i} changed its stable key during Bake.");
            }
        }

        private static AbilityLevelValue BakeLevelValues(
            float[] values,
            string fieldName)
        {
            if (values == null || values.Length == 0)
                return default;
            var baked =
                new Unity.Mathematics.FixedPoint.fp[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) ||
                    float.IsInfinity(value) ||
                    value < 0f)
                {
                    throw new InvalidOperationException(
                        $"{fieldName}[{i}] must be finite and nonnegative.");
                }
                baked[i] =
                    (Unity.Mathematics.FixedPoint.fp)value;
            }
            return new AbilityLevelValue(baked);
        }

        private static AbilityCastConditionDef[] BakeConditions(
            AbilityCastConditionAuthoring[] authoring)
        {
            if (authoring == null || authoring.Length == 0)
                return Array.Empty<AbilityCastConditionDef>();
            var baked =
                new AbilityCastConditionDef[authoring.Length];
            for (int i = 0; i < authoring.Length; i++)
            {
                if (authoring[i] == null)
                    throw new InvalidOperationException(
                        $"Cast condition {i} is null.");
                baked[i] = authoring[i].Bake() ??
                    throw new InvalidOperationException(
                        $"Cast condition {i} baked null.");
            }
            return baked;
        }

        private void OnValidate()
        {
            if (abilityId <= 0) abilityId = 1;
            if (string.IsNullOrWhiteSpace(abilityName))
                abilityName = "Unnamed Ability";
        }
    }

    // ---- CastModel Authoring Types ----

    [Serializable]
    public abstract class CastModelAuthoring
    {
        public abstract CastModelKind Kind { get; }
        public abstract CastModelDef Bake(StageDefAuthoring[] stages);
    }

    [Serializable]
    public sealed class CommitCastModelAuthoring : CastModelAuthoring
    {
        public override CastModelKind Kind => CastModelKind.Commit;

        [SerializeField] internal byte castStageKey;
        [SerializeField] internal int durationTicks;
        [SerializeField] internal bool notifyAbilityCastOnEnter = true;
        [SerializeField] internal bool interruptible = true;

        public byte CastStageKey => castStageKey;
        public int DurationTicks => durationTicks;

        public override CastModelDef Bake(StageDefAuthoring[] stages)
        {
            return new CommitCastModelDef
            {
                Cast = BakeHelpers.BakeStage(castStageKey, durationTicks,
                    notifyAbilityCastOnEnter, interruptible, stages),
            };
        }
    }

    [Serializable]
    public sealed class HoldReleaseCastModelAuthoring : CastModelAuthoring
    {
        public override CastModelKind Kind => CastModelKind.HoldRelease;

        [SerializeField] internal byte holdStageKey;
        [SerializeField] internal int holdDurationTicks;
        [SerializeField] internal bool holdInterruptible = true;
        [SerializeField] internal byte releaseStageKey;
        [SerializeField] internal int releaseDurationTicks;
        [SerializeField] internal bool releaseNotifyAbilityCastOnEnter;
        [SerializeField] internal bool releaseInterruptible;

        public byte HoldStageKey => holdStageKey;
        public int HoldDurationTicks => holdDurationTicks;
        public byte ReleaseStageKey => releaseStageKey;
        public int ReleaseDurationTicks => releaseDurationTicks;

        public override CastModelDef Bake(StageDefAuthoring[] stages)
        {
            return new HoldReleaseCastModelDef
            {
                Hold = BakeHelpers.BakeStage(holdStageKey, holdDurationTicks,
                    true, holdInterruptible, stages),
                Release = BakeHelpers.BakeStage(releaseStageKey, releaseDurationTicks,
                    releaseNotifyAbilityCastOnEnter, releaseInterruptible, stages),
            };
        }
    }

    [Serializable]
    public sealed class ChannelCastModelAuthoring : CastModelAuthoring
    {
        public override CastModelKind Kind => CastModelKind.Channel;

        [SerializeField] internal byte channelStageKey;
        [SerializeField] internal int durationTicks;
        [SerializeField] internal bool notifyAbilityCastOnEnter = true;
        [SerializeField] internal bool interruptible = true;

        public byte ChannelStageKey => channelStageKey;
        public int DurationTicks => durationTicks;

        public override CastModelDef Bake(StageDefAuthoring[] stages)
        {
            return new ChannelCastModelDef
            {
                Channel = BakeHelpers.BakeStage(channelStageKey, durationTicks,
                    notifyAbilityCastOnEnter, interruptible, stages),
            };
        }
    }

    [Serializable]
    public sealed class ActiveSignalCastModelAuthoring : CastModelAuthoring
    {
        public override CastModelKind Kind => CastModelKind.ActiveSignal;

        [SerializeField] internal byte activeStageKey;
        [SerializeField] internal int durationTicks;
        [SerializeField] internal bool notifyAbilityCastOnEnter = true;
        [SerializeField] internal bool interruptible;

        public byte ActiveStageKey => activeStageKey;
        public int DurationTicks => durationTicks;

        public override CastModelDef Bake(StageDefAuthoring[] stages)
        {
            return new ActiveSignalCastModelDef
            {
                Active = BakeHelpers.BakeStage(activeStageKey, durationTicks,
                    notifyAbilityCastOnEnter, interruptible, stages),
            };
        }
    }

    // ---- Stage Authoring ----

    [Serializable]
    public abstract class StageDefAuthoring
    {
        [SerializeField] internal byte stageKey;
        [SerializeField] internal string debugName = "Stage";

        public byte StageKey => stageKey;
        public string DebugName => debugName;

        public abstract StageDef Bake();
    }

    [Serializable]
    public abstract class AbilityCastConditionAuthoring
    {
        public abstract AbilityCastConditionDef Bake();
    }

    internal static class BakeHelpers
    {
        public static CastStage BakeStage(
            byte stageKey,
            int durationTicks,
            bool notifyAbilityCastOnEnter,
            bool interruptible,
            StageDefAuthoring[] stages)
        {
            StageDef def = null;
            if (stages != null)
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    if (stages[i] != null && stages[i].StageKey == stageKey)
                    {
                        def = stages[i].Bake();
                        break;
                    }
                }
            }

            if (def == null)
                throw new InvalidOperationException(
                    $"Cast stage key {stageKey} has no StageDef authoring.");

            return new CastStage
            {
                StageKey = stageKey,
                Def = def,
                DurationTicks = durationTicks,
                NotifyAbilityCastOnEnter = notifyAbilityCastOnEnter,
                Interruptible = interruptible,
            };
        }
    }
}
