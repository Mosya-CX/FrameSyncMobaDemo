using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime helper for charge ability stages.
    /// Provides deterministic per-tick charge evaluation with linear interpolation
    /// from start to target position, wall resolution, and progress tracking.
    /// (Ability Design v15.2 sections 5.5)
    /// </summary>
    public static class ChargeStageHelper
    {
        /// <summary>
        /// Evaluate one tick of charging.
        /// Returns (currentPosition, progress, reachedTarget).
        /// Progress ranges from 0.0 to 1.0.
        /// Applies movement delta through MovementHandler.
        /// </summary>
        public static (fp2 currentPos, fp progress, bool reachedTarget) EvaluateCharge(
            Unit caster,
            int currentTick,
            int startTick,
            fp2 startPos,
            fp2 targetPos,
            fp chargeSpeed,
            int durationTicks,
            PathGridMap2D grid = null)
        {
            if (caster?.MovementHandler == null)
                return (startPos, fp.zero, false);

            int elapsed = currentTick - startTick;
            if (elapsed >= durationTicks || durationTicks <= 0)
                return (targetPos, fp.one, true);

            fp progress = (fp)elapsed / (fp)durationTicks;

            // Linear interpolation toward target
            fp2 desiredPos = startPos + (targetPos - startPos) * progress;

            // Compute actual delta from current position
            fp2 currentPos = caster.MovementHandler.Snapshot.Position;
            fp2 rawDelta = desiredPos - currentPos;

            // Cap delta by charge speed
            fp dist = fpmath.sqrt(fpmath.dot(rawDelta, rawDelta));
            if (dist > chargeSpeed)
            {
                rawDelta = rawDelta / dist * chargeSpeed;
            }

            // Wall resolution
            if (grid != null && (rawDelta.x != fp.zero || rawDelta.y != fp.zero))
            {
                rawDelta = ForcedMoveExecutor.ResolveWall(currentPos, rawDelta, grid, RadiusClass.Medium);
            }

            // Apply movement
            if (rawDelta.x != fp.zero || rawDelta.y != fp.zero)
            {
                caster.MovementHandler.ApplyForcedMovement(rawDelta);
            }

            fp2 resultingPos = currentPos + rawDelta;

            // Check if we've reached or passed the target
            fp2 toTarget = targetPos - resultingPos;
            fp remainingDist = fpmath.sqrt(fpmath.dot(toTarget, toTarget));
            bool reached = remainingDist <= (fp)0.1m || elapsed >= durationTicks;

            return (resultingPos, progress, reached);
        }

        /// <summary>
        /// Compute the effect strength multiplier based on charge progress.
        /// Many charge abilities scale their effect with charge time.
        /// Returns 0.0 (min) to 1.0 (max).
        /// </summary>
        public static fp GetEffectMultiplier(fp progress, fp minMultiplier = default)
        {
            if (progress <= minMultiplier) return minMultiplier;
            return minMultiplier + (fp.one - minMultiplier) * progress;
        }
    }
}
