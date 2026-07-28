using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Starts one Movement-owned Dash runtime and observes its completion.
    /// </summary>
    public sealed class DashStageDef : StageDef
    {
        public fp SpeedPerTick;
        public fp TotalDistance;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World == null ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                caster.MovementHandler == null ||
                SpeedPerTick <= fp.zero ||
                TotalDistance <= fp.zero)
            {
                return StageResult.Failed;
            }

            int durationTicks = (int)fpmath.ceil(
                TotalDistance / SpeedPerTick);
            bool started = caster.MovementHandler.StartDash(
                new DashRequest(
                    runtime.Definition.AbilityId,
                    session.Aim.Direction,
                    TotalDistance,
                    durationTicks));
            return started
                ? StageResult.Running
                : StageResult.Failed;
        }

        public override StageResult OnTick(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World == null ||
                !runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            return caster.MovementHandler != null &&
                   caster.MovementHandler.IsDashActive(
                       runtime.Definition.AbilityId)
                ? StageResult.Running
                : StageResult.Completed;
        }

        public override void OnExit(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (runtime.World != null &&
                runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster))
            {
                caster.MovementHandler
                    ?.StopDash(
                        runtime.Definition.AbilityId);
            }
        }
    }
}
