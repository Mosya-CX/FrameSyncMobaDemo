using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: grants a shield to Self or AimTarget for DurationTicks.
    /// </summary>
    public sealed class ShieldStageDef : StageDef
    {
        public fp BaseShield;
        public ShieldType ShieldType = ShieldType.Magic;
        public int DurationTicks = 60;
        public BuffTargetRule TargetRule = BuffTargetRule.Self;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World?.CombatSystem == null || BaseShield <= fp.zero)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            UnitUid targetUid = TargetRule == BuffTargetRule.Self
                ? runtime.CasterUnitUid
                : session.Aim.TargetUnitUid;
            if (!targetUid.IsValid())
                return StageResult.Failed;

            var request = new ShieldRequest
            {
                TargetUnitUid = targetUid,
                SourceUnitUid = runtime.CasterUnitUid,
                BaseValue = BaseShield,
                ShieldType = ShieldType,
                DurationTicks = DurationTicks,
            };
            runtime.World.CombatSystem.SubmitShield(request);
            return StageResult.Completed;
        }
    }
}
