using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnTick: moves the caster forward each tick by SpeedPerTick units.
    /// Completes when accumulated distance reaches TotalDistance or the
    /// stage times out via CastStage.DurationTicks.
    /// </summary>
    public sealed class DashStageDef : StageDef
    {
        public fp SpeedPerTick;
        public fp TotalDistance;

        private static readonly AbilityBlackboardKey<fp> AccumulatedDistanceKey =
            new AbilityBlackboardKey<fp>(7001);

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            session.Blackboard.Set(AccumulatedDistanceKey, fp.zero);
            return StageResult.Running;
        }

        public override StageResult OnTick(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World == null ||
                !runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            fp2 direction = session.Aim.Direction;
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(direction, out fp2 facing, out _))
                return StageResult.Failed;

            fp2 delta = facing * SpeedPerTick;
            caster.MovementHandler?.ApplyForcedMovement(delta);

            fp accumulated = fp.zero;
            session.Blackboard.TryGet(AccumulatedDistanceKey, out accumulated);
            accumulated += SpeedPerTick;
            session.Blackboard.Set(AccumulatedDistanceKey, accumulated);

            if (accumulated >= TotalDistance)
                return StageResult.Completed;

            return StageResult.Running;
        }
    }
}
