using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnTick: applies forced movement pulling AimTarget toward caster by SpeedPerTick.
    /// Completes when target is within MinDistance of caster.
    /// </summary>
    public sealed class PullStageDef : StageDef
    {
        public fp SpeedPerTick;
        public fp MinDistance = fp.one;

        public override StageResult OnTick(AbilitySession session, AbilityRuntime runtime)
        {
            UnitUid targetUid = session.Aim.TargetUnitUid;
            if (!targetUid.IsValid())
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(targetUid, out Unit target))
                return StageResult.Failed;

            fp2 casterPos = caster.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp2 targetPos = target.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp2 direction = casterPos - targetPos;
            fp distSq = direction.x * direction.x + direction.y * direction.y;

            fp minSq = MinDistance * MinDistance;
            if (distSq <= minSq)
                return StageResult.Completed;

            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction, out fp2 facing, out _))
                return StageResult.Completed;

            fp2 delta = facing * SpeedPerTick;
            target.MovementHandler?.ApplyForcedMovement(delta);
            return StageResult.Running;
        }
    }
}
