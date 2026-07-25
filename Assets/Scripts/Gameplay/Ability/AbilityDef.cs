using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityDef
    {
        public int AbilityId;
        public string Name;
        public int CooldownTicks;
        public AbilityCostPlan CostPlan;
        public CastModelDef CastModel;
        public fp CastRange;
        public AimKind AimKind;
        public ActiveAbilityPassiveEffectDef PassiveEffect;
        public bool IsValid => AbilityId > 0 && CastModel != null;
    }

    public struct AbilityCostPlan
    {
        public fp FlatCost;
        public StatId ResourceStat;
        public bool HasCost => FlatCost > fp.zero;
        public fp ChannelCostPerTick;
        public bool HasChannelCost => ChannelCostPerTick > fp.zero;
    }
}
