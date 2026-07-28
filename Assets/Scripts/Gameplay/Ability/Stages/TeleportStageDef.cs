using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: moves the caster to the aim point, or forward by Distance if no point aim.
    /// </summary>
    public sealed class TeleportStageDef : StageDef
    {
        public fp Distance;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            fp2 targetPos;
            if (session.Aim.Kind == AimKind.Point || session.Aim.Kind == AimKind.Unit)
            {
                targetPos = session.Aim.TargetPoint;
            }
            else if (session.Aim.Kind == AimKind.Direction)
            {
                fp2 casterPos = caster.MovementHandler?.Position ?? fp2.zero;
                targetPos = casterPos + session.Aim.Direction * Distance;
            }
            else
            {
                return StageResult.Failed;
            }

            caster.MovementHandler?.ForceSetPosition(targetPos);
            return StageResult.Completed;
        }
    }
}
