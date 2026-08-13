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
        public fp MaxTerrainCrossingDistance;
        public bool ExtendThroughTerrain;
        public ForceMoveWallPolicy WallPolicy =
            ForceMoveWallPolicy.StopAtWall;
        public bool ResetAttackTimerOnStart;

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

            fp resolvedDistance = ResolveDistance(caster, session.Aim.Direction);
            if (resolvedDistance <= fp.zero)
                return StageResult.Failed;
            int durationTicks = (int)fpmath.ceil(
                resolvedDistance / SpeedPerTick);
            bool started = caster.MovementHandler.StartDash(
                new DashRequest(
                    runtime.Definition.AbilityId,
                    session.Aim.Direction,
                    resolvedDistance,
                    durationTicks,
                    WallPolicy));
            if (started && ResetAttackTimerOnStart)
            {
                caster.AttackHandler?.ResetAttackTimer(
                    AttackTimerResetReason.AbilityEffect);
            }
            return started
                ? StageResult.Running
                : StageResult.Failed;
        }

        private fp ResolveDistance(Unit caster, fp2 direction)
        {
            if (!ExtendThroughTerrain ||
                MaxTerrainCrossingDistance <= TotalDistance ||
                caster?.World?.PathGrid == null ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction,
                    out fp2 forward,
                    out _))
            {
                return TotalDistance;
            }

            PathGridMap2D grid = caster.World.PathGrid;
            fp2 start = caster.PhysicsEntity.Transform2D.Position;
            fp2 baseEnd = start + forward * TotalDistance;
            (int baseX, int baseY) = grid.WorldToCell(baseEnd);
            RadiusClass radiusClass =
                RadiusClassHelper.FromRadius(
                    caster.PhysicsEntity.Shape.Radius);
            if (grid.IsPassable(baseX, baseY, radiusClass))
                return TotalDistance;

            fp step = grid.CellSize / (fp)2;
            if (step <= fp.zero)
                step = (fp)0.05m;
            for (fp distance = TotalDistance + step;
                 distance <= MaxTerrainCrossingDistance;
                 distance += step)
            {
                fp2 landing = start + forward * distance;
                (int x, int y) = grid.WorldToCell(landing);
                if (grid.IsPassable(x, y, radiusClass))
                    return distance;
            }
            return fp.zero;
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
