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

            CrowdControlAddResult result =
                StructureEffectPolicy.TryApplyCrowdControl(
                    target,
                    runtime.CasterUnitUid,
                    CrowdControlIds.Stun,
                    DurationTicks,
                    default);
            return result.Added
                ? StageResult.Completed
                : StageResult.Failed;
        }
    }
}
