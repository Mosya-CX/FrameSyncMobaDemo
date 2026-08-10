using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Waypoint follower that consumes a cell-index path and produces
    /// per-tick LocomotionResult outputs. Owned by RouteRuntime / UnitLocomotionAgent.
    /// State must be captured/restored for rollback (Pathfinding Design v13.1 section 9, section 15.3).
    /// </summary>
    public sealed class PathFollower2D
    {
        /// <summary>
        /// Tolerance distance (in world units) to consider a waypoint reached.
        /// </summary>
        private static readonly fp ReachThreshold = (fp)0.2m;
        // Precomputed: (fp)0.2m * (fp)0.2m = (fp)0.04m
        private static readonly fp ReachThresholdSq = (fp)0.04m;

        /// <summary>
        /// Maximum lateral distance from the path corridor before triggering NeedRepath.
        /// Measured perpendicular to the current path segment.
        /// </summary>
        private static readonly fp CorridorTolerance = (fp)2.5m;

        /// <summary>
        /// Current index into the path cell indices array.
        /// -1 means no path is active.
        /// </summary>
        public int PathCursor { get; private set; }

        /// <summary>
        /// True when the entire route has been consumed and the unit has arrived.
        /// </summary>
        public bool RouteFinished { get; private set; }

        /// <summary>
        /// The cell-indices array being followed.
        /// </summary>
        private int[] _pathCellIndices;

        /// <summary>
        /// Reference to the grid for cell-to-world conversion.
        /// </summary>
        private PathGridMap2D _grid;

        public PathFollower2D(PathGridMap2D grid)
        {
            _grid = grid;
            PathCursor = -1;
            RouteFinished = true;
            _pathCellIndices = null;
        }

        /// <summary>
        /// Start following a new path. Resets cursor and finished flag.
        /// </summary>
        public void SetPath(int[] cellIndices)
        {
            _pathCellIndices = cellIndices;
            if (cellIndices != null && cellIndices.Length > 0)
            {
                PathCursor = 0;
                RouteFinished = false;
            }
            else
            {
                PathCursor = -1;
                RouteFinished = true;
            }
        }

        /// <summary>
        /// Reset state for restart or route cancellation.
        /// </summary>
        public void Reset()
        {
            PathCursor = -1;
            RouteFinished = true;
            _pathCellIndices = null;
        }

        /// <summary>
        /// Advance the cursor if the unit is within reach of the current waypoint.
        /// Returns true if the cursor advanced (or has already finished).
        /// </summary>
        public bool AdvanceCursor(fp2 currentPosition)
        {
            if (RouteFinished || _pathCellIndices == null || PathCursor < 0)
                return true;

            if (PathCursor >= _pathCellIndices.Length)
            {
                RouteFinished = true;
                return true;
            }

            int targetCellIndex = _pathCellIndices[PathCursor];
            int cx = targetCellIndex % _grid.Width;
            int cy = targetCellIndex / _grid.Width;
            fp2 waypointWorld = _grid.CellToWorld(cx, cy);

            fp distSq = fpmath.dot(currentPosition - waypointWorld, currentPosition - waypointWorld);

            // Advance cursor past consecutive waypoints within reach
            while (distSq <= ReachThresholdSq && PathCursor < _pathCellIndices.Length - 1)
            {
                PathCursor++;
                int nextCellIndex = _pathCellIndices[PathCursor];
                int ncx = nextCellIndex % _grid.Width;
                int ncy = nextCellIndex / _grid.Width;
                waypointWorld = _grid.CellToWorld(ncx, ncy);
                distSq = fpmath.dot(currentPosition - waypointWorld, currentPosition - waypointWorld);
            }

            // Check arrival at final waypoint
            if (PathCursor >= _pathCellIndices.Length - 1)
            {
                if (distSq <= ReachThresholdSq)
                {
                    RouteFinished = true;
                }
            }

            return true;
        }

        /// <summary>
        /// Skip waypoints the unit is already pressed against (distance at
        /// or below tolerance). A waypoint whose center sits inside another
        /// unit's collision body can never be reached exactly; waiting on it
        /// would dead-lock route movement forever.
        /// </summary>
        public bool SkipWaypointsWithin(
            fp2 currentPosition,
            fp tolerance)
        {
            if (RouteFinished ||
                _pathCellIndices == null ||
                PathCursor < 0)
            {
                return false;
            }
            bool skipped = false;
            fp toleranceSq =
                tolerance * tolerance;
            while (PathCursor <
                   _pathCellIndices.Length)
            {
                int cellIndex =
                    _pathCellIndices[PathCursor];
                int cx = cellIndex %
                    _grid.Width;
                int cy = cellIndex /
                    _grid.Width;
                fp2 waypointWorld =
                    _grid.CellToWorld(
                        cx, cy);
                fp distSq = fpmath.dot(
                    currentPosition -
                        waypointWorld,
                    currentPosition -
                        waypointWorld);
                if (distSq > toleranceSq)
                {
                    return skipped;
                }
                skipped = true;
                if (PathCursor >=
                    _pathCellIndices.Length - 1)
                {
                    RouteFinished = true;
                    return skipped;
                }
                PathCursor++;
            }
            return skipped;
        }

        /// <summary>
        /// Check whether the unit has deviated laterally from the path corridor.
        /// Returns true if the unit is outside the allowed corridor width
        /// from the current path segment.
        /// </summary>
        public bool IsOutsideCorridor(fp2 currentPosition)
        {
            if (RouteFinished || _pathCellIndices == null || PathCursor < 0 || PathCursor >= _pathCellIndices.Length)
                return false;

            int lastIndex = _pathCellIndices.Length - 1;
            int segmentStartCursor;
            int segmentEndCursor;
            if (PathCursor < lastIndex)
            {
                segmentStartCursor = PathCursor;
                segmentEndCursor = PathCursor + 1;
            }
            else if (PathCursor > 0)
            {
                // At the final waypoint the active corridor is the segment
                // leading into it. Measuring only from the final waypoint
                // makes every long two-point route look off-corridor and
                // repeatedly resets the path cursor to its start.
                segmentStartCursor = PathCursor - 1;
                segmentEndCursor = PathCursor;
            }
            else
            {
                int onlyCell = _pathCellIndices[PathCursor];
                fp2 onlyWaypoint = _grid.CellToWorld(
                    onlyCell % _grid.Width,
                    onlyCell / _grid.Width);
                fp2 onlyDelta = currentPosition - onlyWaypoint;
                fp onlyDistanceSq = fpmath.dot(onlyDelta, onlyDelta);
                fp tolerance = CorridorTolerance + ReachThreshold;
                return onlyDistanceSq > tolerance * tolerance;
            }

            int startCell = _pathCellIndices[segmentStartCursor];
            int endCell = _pathCellIndices[segmentEndCursor];
            fp2 start = _grid.CellToWorld(
                startCell % _grid.Width,
                startCell / _grid.Width);
            fp2 end = _grid.CellToWorld(
                endCell % _grid.Width,
                endCell / _grid.Width);
            fp2 segment = end - start;
            fp segmentLengthSq = fpmath.dot(segment, segment);
            if (segmentLengthSq <= fp.zero)
            {
                fp2 delta = currentPosition - end;
                return fpmath.dot(delta, delta) >
                    CorridorTolerance * CorridorTolerance;
            }

            fp t = fpmath.dot(
                currentPosition - start,
                segment) / segmentLengthSq;
            t = fpmath.clamp(t, fp.zero, fp.one);
            fp2 projection = start + segment * t;
            fp2 lateral = currentPosition - projection;
            return fpmath.dot(lateral, lateral) >
                CorridorTolerance * CorridorTolerance;
        }

        /// <summary>
        /// Build a LocomotionResult from the current follower state.
        /// Computes desired direction toward the current waypoint.
        /// </summary>
        public LocomotionResult BuildLocomotionResult(fp2 currentPosition, fp moveSpeed, UnitUid unitUid, bool allowRVO = false)
        {
            if (RouteFinished || _pathCellIndices == null || PathCursor < 0 || PathCursor >= _pathCellIndices.Length)
                return LocomotionResult.Idle(unitUid);

            int targetCellIndex = _pathCellIndices[PathCursor];
            int cx = targetCellIndex % _grid.Width;
            int cy = targetCellIndex / _grid.Width;
            fp2 waypointWorld = _grid.CellToWorld(cx, cy);

            fp2 toTarget = waypointWorld - currentPosition;
            fp distSq = fpmath.dot(toTarget, toTarget);

            if (distSq <= fp.zero)
            {
                // Try next waypoint if available
                if (PathCursor < _pathCellIndices.Length - 1)
                {
                    PathCursor++;
                    return BuildLocomotionResult(currentPosition, moveSpeed, unitUid, allowRVO);
                }
                return LocomotionResult.Idle(unitUid);
            }

            fp dist = fpmath.sqrt(distSq);
            fp2 direction = toTarget / dist;

            return new LocomotionResult
            {
                UnitUid = unitUid,
                HasMovement = true,
                DesiredDirection = direction,
                DesiredSpeed = moveSpeed,
                AllowRVO = allowRVO,
                Status = RouteEvaluationStatus.Moving,
            };
        }

        /// <summary>
        /// Capture follower state into a snapshot-compatible struct.
        /// </summary>
        public PathFollowerState CaptureState()
        {
            int[] indicesCopy = null;
            if (_pathCellIndices != null)
            {
                indicesCopy = new int[_pathCellIndices.Length];
                System.Array.Copy(_pathCellIndices, indicesCopy, _pathCellIndices.Length);
            }

            return new PathFollowerState
            {
                PathCursor = PathCursor,
                RouteFinished = RouteFinished,
                PathCellIndices = indicesCopy,
            };
        }

        /// <summary>
        /// Restore follower state from snapshot.
        /// </summary>
        public void RestoreState(in PathFollowerState state)
        {
            PathCursor = state.PathCursor;
            RouteFinished = state.RouteFinished;
            if (state.PathCellIndices != null)
            {
                _pathCellIndices = new int[state.PathCellIndices.Length];
                System.Array.Copy(state.PathCellIndices, _pathCellIndices, state.PathCellIndices.Length);
            }
            else
            {
                _pathCellIndices = null;
            }
        }

        /// <summary>
        /// Build a LocomotionResult from flow-field direction query.
        /// (Pathfinding Design v13.1 section 9.7)
        /// </summary>
        public LocomotionResult BuildFlowFieldLocomotionResult(
            fp2 currentPosition, fp moveSpeed, UnitUid unitUid,
            in TeamFlowFieldData flowField, TeamFlowFieldService service, bool allowRVO = false)
        {
            fp2 direction = service.GetFlowDirection(flowField, currentPosition);
            if (direction.x == fp.zero && direction.y == fp.zero)
            {
                return new LocomotionResult
                {
                    UnitUid = unitUid,
                    HasMovement = false,
                    AllowRVO = allowRVO,
                    Status = RouteEvaluationStatus.Blocked,
                };
            }
            return new LocomotionResult
            {
                UnitUid = unitUid,
                HasMovement = true,
                DesiredDirection = direction,
                DesiredSpeed = moveSpeed,
                AllowRVO = allowRVO,
                Status = RouteEvaluationStatus.Moving,
            };
        }
    }

    /// <summary>
    /// Serializable state of PathFollower2D for snapshot capture/restore.
    /// </summary>
    public struct PathFollowerState
    {
        public int PathCursor;
        public bool RouteFinished;
        public int[] PathCellIndices;

        public static readonly PathFollowerState Empty = new PathFollowerState
        {
            PathCursor = -1,
            RouteFinished = true,
            PathCellIndices = null,
        };
    }
}
