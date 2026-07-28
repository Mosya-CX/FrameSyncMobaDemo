using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class PullStageDef : StageDef
    {
        public fp SpeedPerTick;
        public fp MinDistance = fp.one;
        public byte Priority;

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (runtime.World == null ||
                SpeedPerTick <= fp.zero ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                !runtime.World.TryGetUnit(
                    session.Aim.TargetUnitUid,
                    out Unit target))
            {
                return StageResult.Failed;
            }

            fp2 casterPosition =
                caster.PhysicsEntity.Transform2D.Position;
            fp2 targetPosition =
                target.PhysicsEntity.Transform2D.Position;
            fp2 towardCaster =
                casterPosition - targetPosition;
            fp distance = fpmath.sqrt(
                fpmath.lengthsq(towardCaster));
            fp travelDistance =
                distance - MinDistance;
            if (travelDistance <= fp.zero)
            {
                return StageResult.Completed;
            }
            if (!Physics.PhysicsGeometry2D
                .TryCreateFacing(
                    towardCaster,
                    out fp2 direction,
                    out _))
            {
                return StageResult.Failed;
            }

            int durationTicks = (int)fpmath.ceil(
                travelDistance /
                SpeedPerTick);
            CrowdControlAddResult result =
                target.CrowdControl.Add(
                    new CrowdControlConstraint
                    {
                        Type =
                            CrowdControlType.Knockback,
                        RemainingTicks =
                            durationTicks,
                        Priority = Priority,
                        SourceUnitUid =
                            caster.UnitUid,
                        IsForcedMove = true,
                        ForcedMoveConfigId =
                            runtime.Definition.AbilityId,
                        ForcedMoveDeltaPerTick =
                            direction * SpeedPerTick,
                        ForcedMoveWallPolicy =
                            ForceMoveWallPolicy.StopAtWall,
                    });
            return result.Added
                ? StageResult.Running
                : StageResult.Failed;
        }

        public override StageResult OnTick(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (runtime.World == null ||
                !runtime.World.TryGetUnit(
                    session.Aim.TargetUnitUid,
                    out Unit target))
            {
                return StageResult.Failed;
            }
            return target.CrowdControl
                .TryGetActiveForcedMove(
                    runtime.CasterUnitUid,
                    runtime.Definition.AbilityId,
                    out _)
                ? StageResult.Running
                : StageResult.Completed;
        }

        public override void OnExit(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (runtime.World != null &&
                runtime.World.TryGetUnit(
                    session.Aim.TargetUnitUid,
                    out Unit target) &&
                target.CrowdControl
                    .TryGetActiveForcedMove(
                        runtime.CasterUnitUid,
                        runtime.Definition.AbilityId,
                        out CrowdControlHandle handle))
            {
                target.CrowdControl.Remove(
                    handle,
                    ControlRemoveReason.Manual);
            }
        }
    }
}
