using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Kind of forced movement trajectory.
    /// </summary>
    public enum ForceMoveKind : byte
    {
        /// <summary>Velocity vector applied over a fixed number of ticks (knockback).</summary>
        Knockback,

        /// <summary>Linear dash from start toward direction over distance/time.</summary>
        Dash,

        /// <summary>Position pulled toward a source point at a fixed speed.</summary>
        Pull,
    }

    /// <summary>
    /// Wall collision policy for forced movement.
    /// </summary>
    public enum ForceMoveWallPolicy : byte
    {
        /// <summary>Stop at the first blocked cell boundary.</summary>
        StopAtWall,

        /// <summary>Allow movement through walls (teleport-style).</summary>
        PassThrough,
    }

    public readonly struct DashRequest
    {
        public readonly int ConfigId;
        public readonly fp2 Direction;
        public readonly fp Distance;
        public readonly int DurationTicks;
        public readonly ForceMoveWallPolicy WallPolicy;

        public DashRequest(
            int configId,
            fp2 direction,
            fp distance,
            int durationTicks,
            ForceMoveWallPolicy wallPolicy =
                ForceMoveWallPolicy.StopAtWall)
        {
            ConfigId = configId;
            Direction = direction;
            Distance = distance;
            DurationTicks = durationTicks;
            WallPolicy = wallPolicy;
        }
    }

    public struct DashRuntime
    {
        public bool IsActive;
        public int StartTick;
        public int ConfigId;
        public int DurationTicks;
        public fp2 StartPosition;
        public fp2 Direction;
        public fp2 TargetPosition;
        public ForceMoveWallPolicy WallPolicy;
    }

    public readonly struct ResolvedForcedMove
    {
        public readonly CrowdControlHandle SourceControlHandle;
        public readonly int ConfigId;
        public readonly int DurationTicks;
        public readonly fp2 Direction;
        public readonly fp2 TargetPosition;
        public readonly ForceMoveWallPolicy WallPolicy;

        public ResolvedForcedMove(
            CrowdControlHandle sourceControlHandle,
            int configId,
            int durationTicks,
            fp2 direction,
            fp2 targetPosition,
            ForceMoveWallPolicy wallPolicy)
        {
            SourceControlHandle = sourceControlHandle;
            ConfigId = configId;
            DurationTicks = durationTicks;
            Direction = direction;
            TargetPosition = targetPosition;
            WallPolicy = wallPolicy;
        }
    }

    public struct ForcedMoveRuntime
    {
        public bool IsActive;
        public CrowdControlHandle SourceControlHandle;
        public int StartTick;
        public int DurationTicks;
        public fp2 StartPosition;
        public fp2 Direction;
        public fp2 TargetPosition;
        public int ConfigId;
        public ForceMoveWallPolicy WallPolicy;
    }

    /// <summary>
    /// Configuration for a forced movement trajectory.
    /// (Pathfinding Design v13.1 section 11.6)
    /// </summary>
    public struct ForcedMoveConfig
    {
        public ForceMoveKind Kind;
        public fp2 Direction;       // Normalized direction vector
        public fp2 SourcePosition;  // Source point (for Pull)
        public fp Speed;            // Units per tick
        public fp Distance;         // Total displacement distance (0 = unlimited for duration-based)
        public int DurationTicks;   // Total tick count (0 = distance-based)
        public int StartTick;       // Tick when movement began
        public ForceMoveWallPolicy WallPolicy;

        public bool IsFinished(int currentTick)
        {
            if (DurationTicks > 0)
                return currentTick >= StartTick + DurationTicks;
            return false;
        }

        public int EndTick => DurationTicks > 0 ? StartTick + DurationTicks : int.MaxValue;
    }

    /// <summary>
    /// Deterministic forced-movement trajectory executor.
    /// Evaluates per-tick deltas for knockback, dash, and pull.
    /// Does NOT modify positions — returns deltas for MovementHandler to apply.
    /// (Pathfinding Design v13.1 sections 11.4, 11.6)
    /// </summary>
    public static class ForcedMoveExecutor
    {
        /// <summary>
        /// Evaluate one tick of forced movement.
        /// Returns the delta for this tick and whether the trajectory is finished.
        /// </summary>
        public static (fp2 delta, bool isFinished) Evaluate(
            in ForcedMoveConfig config,
            fp2 currentPosition,
            int currentTick)
        {
            if (config.IsFinished(currentTick))
                return (fp2.zero, true);

            fp2 rawDelta;

            switch (config.Kind)
            {
                case ForceMoveKind.Knockback:
                    rawDelta = EvaluateKnockback(config, currentTick);
                    break;

                case ForceMoveKind.Dash:
                    rawDelta = EvaluateDash(config, currentTick);
                    break;

                case ForceMoveKind.Pull:
                    rawDelta = EvaluatePull(config, currentPosition, currentTick);
                    break;

                default:
                    return (fp2.zero, true);
            }

            bool isFinished = config.IsFinished(currentTick);
            return (rawDelta, isFinished);
        }

        private static fp2 EvaluateKnockback(in ForcedMoveConfig config, int currentTick)
        {
            // Knockback: constant velocity per tick
            fp2 delta = config.Direction * config.Speed;

            // Distance cap
            fp elapsedDistance = (fp)(currentTick - config.StartTick + 1) * config.Speed;
            if (config.Distance > fp.zero && elapsedDistance > config.Distance)
            {
                fp remaining = config.Distance - (fp)(currentTick - config.StartTick) * config.Speed;
                if (remaining <= fp.zero)
                    return fp2.zero;
                delta = config.Direction * remaining;
            }

            return delta;
        }

        private static fp2 EvaluateDash(in ForcedMoveConfig config, int currentTick)
        {
            // Dash: constant speed in direction, distance-limited
            fp2 delta = config.Direction * config.Speed;

            // Distance cap
            fp elapsedDistance = (fp)(currentTick - config.StartTick + 1) * config.Speed;
            if (config.Distance > fp.zero && elapsedDistance > config.Distance)
            {
                fp remaining = config.Distance - (fp)(currentTick - config.StartTick) * config.Speed;
                if (remaining <= fp.zero)
                    return fp2.zero;
                delta = config.Direction * remaining;
            }

            return delta;
        }

        private static fp2 EvaluatePull(in ForcedMoveConfig config, fp2 currentPosition, int currentTick)
        {
            // Pull: move toward source position at fixed speed
            fp2 toSource = config.SourcePosition - currentPosition;
            fp distSq = fpmath.dot(toSource, toSource);

            if (distSq <= fp.zero)
                return fp2.zero;

            fp dist = fpmath.sqrt(distSq);
            fp step = config.Speed;

            if (step >= dist)
                return toSource; // Arrived at source

            fp2 delta = toSource / dist * step;

            // Distance cap
            fp elapsedDistance = (fp)(currentTick - config.StartTick + 1) * config.Speed;
            if (config.Distance > fp.zero && elapsedDistance > config.Distance)
            {
                fp remaining = config.Distance - (fp)(currentTick - config.StartTick) * config.Speed;
                if (remaining <= fp.zero)
                    return fp2.zero;
                delta = toSource / dist * remaining;
            }

            return delta;
        }

        /// <summary>
        /// Resolve wall collision for a forced movement delta.
        /// StopAtWall: clamp delta at the first blocked cell boundary.
        /// PassThrough: return delta unchanged.
        /// (Pathfinding Design v13.1 section 11.4)
        /// </summary>
        public static fp2 ResolveWall(
            fp2 from,
            fp2 delta,
            PathGridMap2D grid,
            RadiusClass rc = RadiusClass.Medium)
        {
            if (grid == null) return delta;
            if (delta.x == fp.zero && delta.y == fp.zero) return delta;

            fp2 to = from + delta;
            (int fromX, int fromY) = grid.WorldToCell(from);
            (int toX, int toY) = grid.WorldToCell(to);

            // If destination cell is walkable, allow full delta
            if (grid.IsPassable(toX, toY, rc))
                return delta;

            // Binary search along the ray to find the furthest walkable point
            fp tMin = fp.zero;
            fp tMax = fp.one;
            fp t = (fp)0.5m;

            for (int i = 0; i < 8; i++)
            {
                fp2 mid = from + delta * t;
                (int mx, int my) = grid.WorldToCell(mid);

                if (grid.IsPassable(mx, my, rc))
                {
                    tMin = t;
                }
                else
                {
                    tMax = t;
                }
                t = (tMin + tMax) / (fp)2m;
            }

            return delta * tMin;
        }
    }
}
