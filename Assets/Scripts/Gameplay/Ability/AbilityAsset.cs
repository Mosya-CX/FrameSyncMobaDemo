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
        [SerializeField] private CastModelAuthoring castModel = new CommitCastModelAuthoring();
        [SerializeField] private float castRange = 0f;
        [SerializeField] private AimKind aimKind;

        [Header("Timing")]
        [Min(0)]
        [SerializeField] private int cooldownTicks;

        [Header("Cost")]
        [SerializeField] private bool hasResourceCost;
        [SerializeField] private StatId resourceStat;
        [SerializeField] private float flatCost;

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
        public bool HasResourceCost => hasResourceCost;
        public StatId ResourceStat => resourceStat;
        public float FlatCost => flatCost;
        public StageDefAuthoring[] Stages => stageDefs;

        public AbilityDef Bake()
        {
            if (abilityId <= 0)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' has invalid AbilityId {abilityId}.");

            var def = new AbilityDef
            {
                AbilityId = abilityId,
                Name = abilityName,
                CooldownTicks = cooldownTicks,
                AimKind = aimKind,
                CastRange = (Unity.Mathematics.FixedPoint.fp)castRange,
                CastModel = castModel?.Bake(stageDefs),
                CostPlan = hasResourceCost
                    ? new AbilityCostPlan
                    {
                        FlatCost = (Unity.Mathematics.FixedPoint.fp)flatCost,
                        ResourceStat = resourceStat,
                    }
                    : default,
            };

            return def;
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
    public class StageDefAuthoring
    {
        [SerializeField] internal byte stageKey;
        [SerializeField] internal string debugName = "Stage";

        public byte StageKey => stageKey;
        public string DebugName => debugName;

        public virtual StageDef Bake()
        {
            return new RuntimePlaceholderStageDef
            {
                StageDefId = stageKey,
                DebugName = debugName,
            };
        }
    }

    internal sealed class RuntimePlaceholderStageDef : StageDef
    {
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

            return new CastStage
            {
                StageKey = stageKey,
                Def = def ?? new RuntimePlaceholderStageDef
                {
                    StageDefId = stageKey,
                    DebugName = "Stage_" + stageKey,
                },
                DurationTicks = durationTicks,
                NotifyAbilityCastOnEnter = notifyAbilityCastOnEnter,
                Interruptible = interruptible,
            };
        }
    }
}
