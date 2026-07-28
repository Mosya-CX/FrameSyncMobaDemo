using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Correction request generated when a unit penetrates a static wall.
    /// (Pathfinding Design v13.1 section 12.3)
    /// </summary>
    public struct MovementCorrectionRequest
    {
        public UnitUid UnitUid;
        public fp2 Delta;               // Push-out delta
        public MovementCorrectionReason Reason;
    }

    /// <summary>
    /// Reason for a movement correction.
    /// </summary>
    public enum MovementCorrectionReason : byte
    {
        None,
        WallDepenetration,
        DashEndOverlap,
        TeleportWallOverlap,
    }

    /// <summary>
    /// Detects units that have penetrated static walls and
    /// generates push-out correction requests.
    /// Does NOT directly modify positions — outputs requests for MovementHandler.
    /// (Pathfinding Design v13.1 section 12)
    /// </summary>
    public static class WallPenetrationResolver
    {
        private static readonly fp MaxDepenetrationDistance = (fp)1.0m;
        private static readonly fp CellHalfSize = (fp)0.5m;

        /// <summary>
        /// Check if a unit at the given world position with the given radius
        /// is inside an unwalkable cell. If so, compute a push-out delta.
        /// Returns null if the unit is not penetrating any wall.
        /// (section 12.3 DetectWallPenetration)
        /// </summary>
        public static MovementCorrectionRequest? Detect(
            UnitUid uid,
            fp2 position,
            fp radius,
            PathGridMap2D grid)
        {
            (int cx, int cy) = grid.WorldToCell(position);
            RadiusClass radiusClass =
                RadiusClassHelper.FromRadius(radius);

            // If the cell itself is walkable, check nearby cells too for large units
            if (!grid.IsPassable(cx, cy, radiusClass))
            {
                fp2 pushOut = ComputePushOut(
                    position,
                    radius,
                    radiusClass,
                    cx,
                    cy,
                    grid);
                if (pushOut.x != fp.zero || pushOut.y != fp.zero)
                {
                    // Clamp push-out magnitude
                    fp lenSq = fpmath.dot(pushOut, pushOut);
                    if (lenSq > MaxDepenetrationDistance * MaxDepenetrationDistance)
                    {
                        fp len = fpmath.sqrt(lenSq);
                        pushOut = pushOut / len * MaxDepenetrationDistance;
                    }

                    return new MovementCorrectionRequest
                    {
                        UnitUid = uid,
                        Delta = pushOut,
                        Reason = MovementCorrectionReason.WallDepenetration,
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Compute a push-out vector from a blocked cell.
        /// Pushes toward the nearest walkable direction.
        /// </summary>
        private static fp2 ComputePushOut(
            fp2 position,
            fp radius,
            RadiusClass radiusClass,
            int cx,
            int cy,
            PathGridMap2D grid)
        {
            fp2 cellCenter = grid.CellToWorld(cx, cy);
            fp2 fromCenter = position - cellCenter;
            fp2 dir = fp2.zero;

            // Push out in the direction of the nearest walkable neighbor
            fp bestDist = fp.max_value;

            for (int d = 1; d <= 8; d++)
            {
                Dir8 dir8 = (Dir8)d;
                var (dx, dy) = Dir8Helper.Delta(dir8);
                int nx = cx + dx;
                int ny = cy + dy;

                if (nx < 0 || nx >= grid.Width || ny < 0 || ny >= grid.Height) continue;
                if (!grid.IsPassable(
                        nx,
                        ny,
                        radiusClass))
                {
                    continue;
                }

                fp2 neighborCenter = grid.CellToWorld(nx, ny);
                fp2 toNeighbor = neighborCenter - position;
                fp distSq = fpmath.dot(toNeighbor, toNeighbor);

                if (distSq < bestDist)
                {
                    bestDist = distSq;
                    fp dist = fpmath.sqrt(distSq);
                    if (dist > fp.zero)
                        dir = toNeighbor / dist;
                }
            }

            // Push by cell size + radius
            if (dir.x == fp.zero && dir.y == fp.zero)
            {
                // Fallback: push away from cell center
                fp len = fpmath.sqrt(fpmath.dot(fromCenter, fromCenter));
                if (len > fp.zero)
                    dir = fromCenter / len;
                else
                    dir = new fp2(fp.one, fp.zero);
            }

            return dir * (grid.CellSize + radius);
        }
    }
}
