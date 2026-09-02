using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class PullStageDef : StageDef
    {
        private static readonly AbilityBlackboardKey<CrowdControlHandle>
            PullControlHandleKey =
                new AbilityBlackboardKey<CrowdControlHandle>(
                    9001);

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
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetFp2(
                ControlParamKeys.Direction,
                direction);
            parameters.SetFp(
                ControlParamKeys.Distance,
                travelDistance);
            parameters.SetInt(
                ControlParamKeys.MoveTicks,
                durationTicks);
            parameters.SetShort(
                ControlParamKeys.ForcedMovePriority,
                (short)Priority);
            CrowdControlAddResult result =
                StructureEffectPolicy.TryApplyCrowdControl(
                    target,
                    caster.UnitUid,
                    CrowdControlIds.KnockBack,
                    durationTicks,
                    parameters);
            if (!result.Added)
            {
                return StageResult.Failed;
            }
            session.Blackboard.Set(
                PullControlHandleKey,
                result.Handle);
            return StageResult.Running;
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
            if (!session.Blackboard.TryGet(
                    PullControlHandleKey,
                    out CrowdControlHandle handle) ||
                !handle.IsValid)
            {
                return StageResult.Completed;
            }
            return target.CrowdControl
                    .GetRemainingTicks(handle) > 0
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
                session.Blackboard.TryGet(
                    PullControlHandleKey,
                    out CrowdControlHandle handle) &&
                handle.IsValid)
            {
                target.CrowdControl.Remove(
                    handle,
                    ControlRemoveReason.Explicit);
            }
        }
    }
}
