using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: heals Self or AimTarget for BaseHeal. Scales with HealPower stat.
    /// </summary>
    public sealed class HealStageDef : StageDef
    {
        public fp BaseHeal;
        public BuffTargetRule TargetRule = BuffTargetRule.Self;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World?.CombatSystem == null || BaseHeal <= fp.zero)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            UnitUid targetUid = TargetRule == BuffTargetRule.Self
                ? runtime.CasterUnitUid
                : session.Aim.TargetUnitUid;
            if (!targetUid.IsValid())
                return StageResult.Failed;

            fp healAmount = BaseHeal;
            if (runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit src) && src.StatHandler != null)
            {
                fp healPower = src.StatHandler.GetStat(StatId.HealPower);
                healAmount *= (fp.one + healPower);
            }

            var request = new HealRequest
            {
                TargetUnitUid = targetUid,
                SourceUnitUid = runtime.CasterUnitUid,
                BaseValue = healAmount,
            };
            runtime.World.CombatSystem.SubmitHeal(request);
            return StageResult.Completed;
        }
    }
}
