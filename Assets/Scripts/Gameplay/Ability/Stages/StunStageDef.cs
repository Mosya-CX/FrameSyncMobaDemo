using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: applies a Stun crowd control to AimTarget for DurationTicks.
    /// Respects CC immunity.
    /// </summary>
    public sealed class StunStageDef : StageDef
    {
        public int DurationTicks = 30;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            UnitUid targetUid = session.Aim.TargetUnitUid;
            if (!targetUid.IsValid())
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(targetUid, out Unit target))
                return StageResult.Failed;

            var constraint = new CrowdControlConstraint
            {
                Type = CrowdControlType.Stun,
                SourceUnitUid = runtime.CasterUnitUid,
                RemainingTicks = DurationTicks,
                Priority = 0,
            };
            target.CrowdControl?.Add(constraint);
            return StageResult.Completed;
        }
    }
}
