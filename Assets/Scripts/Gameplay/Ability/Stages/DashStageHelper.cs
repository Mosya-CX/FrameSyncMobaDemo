using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime helper for dash ability stages.
    /// Provides deterministic per-tick dash evaluation with wall resolution
    /// and post-dash wall penetration validation.
    /// Works alongside the existing DashStageDef.OnTick implementation.
    /// (Pathfinding Design v13.1 sections 11.4, 12.2)
    /// </summary>
    public static class DashStageHelper
    {
        /// <summary>
        /// Evaluate one tick of dash movement with wall collision.
        /// Computes the raw dash delta, resolves it against the grid,
        /// and applies it through MovementHandler.
        /// Returns true if the dash should continue, false if blocked by wall.
        /// </summary>
        public static bool ApplyDashTick(
            Unit caster,
            fp2 direction,
            fp speedPerTick,
            PathGridMap2D grid,
            bool allowRVO = false)
        {
            if (caster?.MovementHandler == null) return false;

            fp2 rawDelta = direction * speedPerTick;

            // Resolve wall collision
            if (grid != null)
            {
                rawDelta = ForcedMoveExecutor.ResolveWall(
                    caster.MovementHandler.Snapshot.Position,
                    rawDelta,
                    grid,
                    RadiusClass.Medium);
            }

            if (rawDelta.x == fp.zero && rawDelta.y == fp.zero)
                return false; // Blocked by wall

            caster.MovementHandler.ApplyForcedMovement(rawDelta, allowRVO);
            return true;
        }

        /// <summary>
        /// Validate wall penetration after a dash finishes.
        /// Generates MovementCorrectionRequest if the unit is stuck in a wall.
        /// (Pathfinding Design v13.1 section 12.2)
        /// </summary>
        public static void ValidatePostDash(Unit caster, PathGridMap2D grid)
        {
            if (caster?.MovementHandler == null || grid == null) return;

            var correction = WallPenetrationResolver.Detect(
                caster.UnitUid,
                caster.MovementHandler.Snapshot.Position,
                RadiusClassHelper.GetRadius(RadiusClass.Medium),
                grid);

            if (correction.HasValue)
            {
                caster.MovementHandler.ApplyForcedMovement(correction.Value.Delta);
            }
        }

        /// <summary>
        /// Cleanup dash state on death or ability interruption.
        /// Stops any active forced movement.
        /// </summary>
        public static void ClearDashForDeath(Unit caster)
        {
            caster?.MovementHandler?.ClearForDeath();
        }
    }
}
