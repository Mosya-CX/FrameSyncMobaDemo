using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic A* pathfinding service.
    /// Uses octile heuristic, 8-direction stable neighbor expansion,
    /// LOS-based Bresenham path smoothing, and a max-iteration guard.
    /// Internal state arrays are reusable across searches via SearchId clearing.
    /// </summary>
    public sealed class AStarPathService
    {
        private const int MaxIterationsDefault = 1200;
        private const int BlockedTargetNeighborRadius = 3;
        private static readonly fp StraightCost = fp.one;
        // Octile diagonal cost: sqrt(2) ¡Ö 1.414213562. Use literal to avoid fpmath.sqrt precision.
        private static readonly fp DiagonalCost = (fp)1.414213562m;

        private readonly PathGridMap2D _grid;
        private readonly IndexedMinHeap _openSet;

        // Reusable state arrays
        private int[] _closedSetSearchId;
        private int[] _parentIndices;
        private fp[] _gCosts;
        private int _searchId;

        // Pre-allocated neighbor buffer
        private static readonly (int dx, int dy)[] NeighborDirs = new (int, int)[]
        {
            ( 0, -1),  // N   (cost 1)
            ( 1, -1),  // NE  (cost sqrt2)
            ( 1,  0),  // E   (cost 1)
            ( 1,  1),  // SE  (cost sqrt2)
            ( 0,  1),  // S   (cost 1)
            (-1,  1),  // SW  (cost sqrt2)
            (-1,  0),  // W   (cost 1)
            (-1, -1),  // NW  (cost sqrt2)
        };

        private static readonly int[] NeighborCostScale = new int[]
        {
            1, 1, 1, 1, 1, 1, 1, 1,
        };

        // Path reconstruction buffer
        private int[] _pathBuffer;
        private int _pathBufferCount;

        public AStarPathService(PathGridMap2D grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            int totalCells = grid.Width * grid.Height;
            _openSet = new IndexedMinHeap(grid.Width, grid.Height, 512);
            _closedSetSearchId = new int[totalCells];
            _parentIndices = new int[totalCells];
            _gCosts = new fp[totalCells];
            _pathBuffer = new int[512];
            _searchId = 0;
        }

        /// <summary>
        /// Find a path from start to target world positions.
        /// Returns a PathResult with cell indices if successful.
        /// </summary>
        public PathResult FindPath(fp2 start, fp2 target, int maxIterations = MaxIterationsDefault)
        {
            if (_grid.Width <= 0 || _grid.Height <= 0)
                return PathResult.Failed(PathStatus.SystemNotReady);

            (int startCx, int startCy) = _grid.WorldToCell(start);
            (int targetCx, int targetCy) = _grid.WorldToCell(target);

            // Validate start
            if (!_grid.IsPassable(startCx, startCy))
                return PathResult.Failed(PathStatus.InvalidStart);
            return FindPathImpl(startCx, startCy, targetCx, targetCy, maxIterations);
        }

        /// <summary>
        /// Find a path from start to target for a specific RadiusClass.
        /// Uses the appropriate clearance layer for passability checks.
        /// </summary>
        public PathResult FindPath(fp2 start, fp2 target, RadiusClass rc, int maxIterations = MaxIterationsDefault)
        {
            if (_grid.Width <= 0 || _grid.Height <= 0)
                return PathResult.Failed(PathStatus.SystemNotReady);

            (int startCx, int startCy) = _grid.WorldToCell(start);
            (int targetCx, int targetCy) = _grid.WorldToCell(target);

            // Validate start with radius-aware check
            if (!_grid.IsPassable(startCx, startCy, rc))
                return PathResult.Failed(PathStatus.InvalidStart);

            return FindPathImplRadiusAware(startCx, startCy, targetCx, targetCy, rc, maxIterations);
        }

        private PathResult FindPathImpl(int startCx, int startCy, int targetCx, int targetCy, int maxIterations)
        {

            // Check if start and target are the same cell
            if (startCx == targetCx && startCy == targetCy)
            {
                return PathResult.Ok(new int[] { CellIndex(startCx, startCy) });
            }

            int targetCellIndex = CellIndex(targetCx, targetCy);
            int startCellIndex = CellIndex(startCx, startCy);

            // If target is blocked, search for nearest passable neighbor
            if (!_grid.IsPassable(targetCx, targetCy))
            {
                int? fallbackCell = FindNearestPassable(targetCx, targetCy, startCx, startCy);
                if (!fallbackCell.HasValue)
                    return PathResult.Failed(PathStatus.EndBlocked);

                targetCellIndex = fallbackCell.Value;
                targetCy = targetCellIndex / _grid.Width;
                targetCx = targetCellIndex % _grid.Width;
            }

            BeginNewSearch();

            // Initialize start node
            fp hStart = OctileHeuristic(startCx, startCy, targetCx, targetCy);
            PathNode startNode = new PathNode(startCx, startCy, fp.zero, hStart, -1);
            _openSet.Push(startNode);
            _gCosts[startCellIndex] = fp.zero;
            _parentIndices[startCellIndex] = -1;

            int iterations = 0;
            while (_openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                PathNode current = _openSet.Pop();
                int currentIndex = CellIndex(current.CellX, current.CellY);

                // Mark as closed
                _closedSetSearchId[currentIndex] = _searchId;

                // Check if reached target
                if (current.CellX == targetCx && current.CellY == targetCy)
                {
                    return BuildPathResult(currentIndex, startCellIndex);
                }

                // Expand neighbors in stable clockwise order
                for (int i = 0; i < NeighborDirs.Length; i++)
                {
                    int nx = current.CellX + NeighborDirs[i].dx;
                    int ny = current.CellY + NeighborDirs[i].dy;

                    if (!_grid.IsPassable(nx, ny))
                        continue;

                    // Corner cutting prevention: if diagonal, both cardinal neighbors must be passable
                    int dirIdx = i;
                    if (dirIdx == 1 && (!_grid.IsPassable(current.CellX + 1, current.CellY) || !_grid.IsPassable(current.CellX, current.CellY - 1)))
                        continue;
                    if (dirIdx == 3 && (!_grid.IsPassable(current.CellX + 1, current.CellY) || !_grid.IsPassable(current.CellX, current.CellY + 1)))
                        continue;
                    if (dirIdx == 5 && (!_grid.IsPassable(current.CellX - 1, current.CellY) || !_grid.IsPassable(current.CellX, current.CellY + 1)))
                        continue;
                    if (dirIdx == 7 && (!_grid.IsPassable(current.CellX - 1, current.CellY) || !_grid.IsPassable(current.CellX, current.CellY - 1)))
                        continue;

                    int neighborIndex = CellIndex(nx, ny);

                    // Skip if already closed in this search
                    if (_closedSetSearchId[neighborIndex] == _searchId)
                        continue;

                    bool isDiagonal = (dirIdx % 2 == 1);
                    fp moveCost = isDiagonal ? DiagonalCost : StraightCost;
                    fp tentativeG = current.GCost + moveCost;

                    // Check if neighbor is in open set
                    int heapIdx = _openSet.GetHeapIndex(neighborIndex);
                    if (heapIdx >= 0)
                    {
                        if (tentativeG < _gCosts[neighborIndex])
                        {
                            _gCosts[neighborIndex] = tentativeG;
                            _parentIndices[neighborIndex] = currentIndex;
                            _openSet.DecreaseKey(neighborIndex, tentativeG);
                        }
                    }
                    else
                    {
                        _gCosts[neighborIndex] = tentativeG;
                        _parentIndices[neighborIndex] = currentIndex;
                        fp h = OctileHeuristic(nx, ny, targetCx, targetCy);
                        PathNode neighbor = new PathNode(nx, ny, tentativeG, h, currentIndex);
                        _openSet.Push(neighbor);
                    }
                }
            }

            if (iterations >= maxIterations)
                return PathResult.Failed(PathStatus.MaxIterationReached);

            return PathResult.Failed(PathStatus.NoPath);
        }

        /// <summary>
        /// Octile distance heuristic.
        /// h = max(dx, dy) * sqrt2_cost + min(dx, dy) * straight_cost
        /// </summary>
        private fp OctileHeuristic(int cx, int cy, int gx, int gy)
        {
            int dx = Math.Abs(cx - gx);
            int dy = Math.Abs(cy - gy);
            if (dx < dy)
            {
                return (fp)dx * DiagonalCost + (fp)(dy - dx) * StraightCost;
            }
            else
            {
                return (fp)dy * DiagonalCost + (fp)(dx - dy) * StraightCost;
            }
        }

        /// <summary>
        /// Reconstruct path from target cell back to start cell via parent indices,
        /// then apply LOS-based smoothing to reduce waypoint count.
        /// </summary>
        private PathResult BuildPathResult(int targetCellIndex, int startCellIndex)
        {
            _pathBufferCount = 0;
            int current = targetCellIndex;
            while (current != -1)
            {
                if (_pathBufferCount >= _pathBuffer.Length)
                {
                    // Double buffer size
                    int newSize = _pathBuffer.Length * 2;
                    int[] newBuffer = new int[newSize];
                    Array.Copy(_pathBuffer, newBuffer, _pathBuffer.Length);
                    _pathBuffer = newBuffer;
                }
                _pathBuffer[_pathBufferCount++] = current;
                current = _parentIndices[current];
            }

            // Reverse to get start ¡ú target order
            Array.Reverse(_pathBuffer, 0, _pathBufferCount);

            // Apply LOS smoothing
            int[] smoothed = SmoothPath();
            return PathResult.Ok(smoothed);
        }

        /// <summary>
        /// Bresenham-based line-of-sight path smoothing.
        /// Removes intermediate waypoints when there is a clear straight/diagonal line
        /// of passable cells between the current waypoint and a later waypoint.
        /// </summary>
        private int[] SmoothPath()
        {
            if (_pathBufferCount <= 2)
            {
                int[] result = new int[_pathBufferCount];
                Array.Copy(_pathBuffer, result, _pathBufferCount);
                return result;
            }

            // Use an in-place write cursor
            int writeIdx = 0;
            int[] smoothed = new int[_pathBufferCount];
            smoothed[writeIdx++] = _pathBuffer[0];

            for (int i = 1; i < _pathBufferCount - 1; i++)
            {
                int prevCell = smoothed[writeIdx - 1];
                int nextCell = _pathBuffer[i + 1];

                if (!HasLineOfSight(prevCell, nextCell))
                {
                    smoothed[writeIdx++] = _pathBuffer[i];
                }
            }

            // Always include the last waypoint
            smoothed[writeIdx++] = _pathBuffer[_pathBufferCount - 1];

            if (writeIdx < smoothed.Length)
            {
                int[] trimmed = new int[writeIdx];
                Array.Copy(smoothed, trimmed, writeIdx);
                return trimmed;
            }
            return smoothed;
        }

        /// <summary>
        /// Check if there is a clear line of passable cells between two cell indices
        /// using Bresenham's line algorithm.
        /// </summary>
        private bool HasLineOfSight(int fromCell, int toCell)
        {
            int fromCx = fromCell % _grid.Width;
            int fromCy = fromCell / _grid.Width;
            int toCx = toCell % _grid.Width;
            int toCy = toCell / _grid.Width;

            int dx = Math.Abs(toCx - fromCx);
            int dy = -Math.Abs(toCy - fromCy);
            int sx = fromCx < toCx ? 1 : -1;
            int sy = fromCy < toCy ? 1 : -1;
            int err = dx + dy;

            int x = fromCx;
            int y = fromCy;

            while (true)
            {
                if (x == toCx && y == toCy)
                    return true;

                if (!_grid.IsPassable(x, y))
                    return false;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    if (x == toCx) break;
                    err += dy;
                    x += sx;
                }
                if (e2 <= dx)
                {
                    if (y == toCy) break;
                    err += dx;
                    y += sy;
                }
            }

            return true;
        }

        /// <summary>
        /// When the target cell is blocked, search for the nearest passable cell
        /// within a 3-cell radius, preferring cells closer to the start position.
        /// </summary>
        private int? FindNearestPassable(int blockedCx, int blockedCy, int startCx, int startCy)
        {
            int bestCell = -1;
            fp bestDist = (fp)999999m;

            for (int dy = -BlockedTargetNeighborRadius; dy <= BlockedTargetNeighborRadius; dy++)
            {
                for (int dx = -BlockedTargetNeighborRadius; dx <= BlockedTargetNeighborRadius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = blockedCx + dx;
                    int ny = blockedCy + dy;

                    // Never select the start cell as fallback target
                    if (nx == startCx && ny == startCy) continue;

                    if (!_grid.IsPassable(nx, ny))
                        continue;

                    // Prefer cells closer to the start position
                    int distToStart = Math.Abs(nx - startCx) + Math.Abs(ny - startCy);
                    fp dist = new fp(distToStart);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCell = CellIndex(nx, ny);
                    }
                }
            }

            return bestCell >= 0 ? bestCell : null;
        }

        private void BeginNewSearch()
        {
            _searchId++;
            if (_searchId == int.MaxValue)
            {
                _searchId = 1;
                Array.Clear(_closedSetSearchId, 0, _closedSetSearchId.Length);
            }
            _openSet.BeginNewSearch();
        }

        private int CellIndex(int cx, int cy) => cy * _grid.Width + cx;

        private PathResult FindPathImplRadiusAware(
            int startCx, int startCy, int targetCx, int targetCy,
            RadiusClass rc, int maxIterations)
        {
            if (startCx == targetCx && startCy == targetCy)
            {
                return PathResult.Ok(new int[] { CellIndex(startCx, startCy) });
            }

            int targetCellIndex = CellIndex(targetCx, targetCy);
            int startCellIndex = CellIndex(startCx, startCy);

            if (!_grid.IsPassable(targetCx, targetCy, rc))
            {
                int? fallbackCell = FindNearestPassable(targetCx, targetCy, startCx, startCy, rc);
                if (!fallbackCell.HasValue)
                    return PathResult.Failed(PathStatus.EndBlocked);
                targetCellIndex = fallbackCell.Value;
                targetCy = targetCellIndex / _grid.Width;
                targetCx = targetCellIndex % _grid.Width;
            }

            BeginNewSearch();
            fp hStart = OctileHeuristic(startCx, startCy, targetCx, targetCy);
            PathNode startNode = new PathNode(startCx, startCy, fp.zero, hStart, -1);
            _openSet.Push(startNode);
            _gCosts[startCellIndex] = fp.zero;
            _parentIndices[startCellIndex] = -1;

            int iterations = 0;
            while (_openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                PathNode current = _openSet.Pop();
                int currentIndex = CellIndex(current.CellX, current.CellY);
                _closedSetSearchId[currentIndex] = _searchId;

                if (current.CellX == targetCx && current.CellY == targetCy)
                    return BuildPathResult(currentIndex, startCellIndex);

                for (int i = 0; i < NeighborDirs.Length; i++)
                {
                    int nx = current.CellX + NeighborDirs[i].dx;
                    int ny = current.CellY + NeighborDirs[i].dy;
                    if (!_grid.IsPassable(nx, ny, rc)) continue;

                    int dirIdx = i;
                    if (dirIdx == 1 && (!_grid.IsPassable(current.CellX + 1, current.CellY, rc) || !_grid.IsPassable(current.CellX, current.CellY - 1, rc))) continue;
                    if (dirIdx == 3 && (!_grid.IsPassable(current.CellX + 1, current.CellY, rc) || !_grid.IsPassable(current.CellX, current.CellY + 1, rc))) continue;
                    if (dirIdx == 5 && (!_grid.IsPassable(current.CellX - 1, current.CellY, rc) || !_grid.IsPassable(current.CellX, current.CellY + 1, rc))) continue;
                    if (dirIdx == 7 && (!_grid.IsPassable(current.CellX - 1, current.CellY, rc) || !_grid.IsPassable(current.CellX, current.CellY - 1, rc))) continue;

                    int neighborIndex = CellIndex(nx, ny);
                    if (_closedSetSearchId[neighborIndex] == _searchId) continue;

                    bool isDiagonal = (dirIdx % 2 == 1);
                    fp moveCost = isDiagonal ? DiagonalCost : StraightCost;
                    fp tentativeG = current.GCost + moveCost;

                    int heapIdx = _openSet.GetHeapIndex(neighborIndex);
                    if (heapIdx >= 0)
                    {
                        if (tentativeG < _gCosts[neighborIndex])
                        {
                            _gCosts[neighborIndex] = tentativeG;
                            _parentIndices[neighborIndex] = currentIndex;
                            _openSet.DecreaseKey(neighborIndex, tentativeG);
                        }
                    }
                    else
                    {
                        _gCosts[neighborIndex] = tentativeG;
                        _parentIndices[neighborIndex] = currentIndex;
                        fp h = OctileHeuristic(nx, ny, targetCx, targetCy);
                        PathNode neighbor = new PathNode(nx, ny, tentativeG, h, currentIndex);
                        _openSet.Push(neighbor);
                    }
                }
            }

            if (iterations >= maxIterations)
                return PathResult.Failed(PathStatus.MaxIterationReached);
            return PathResult.Failed(PathStatus.NoPath);
        }

        private int? FindNearestPassable(int blockedCx, int blockedCy, int startCx, int startCy, RadiusClass rc)
        {
            int bestCell = -1;
            fp bestDist = (fp)999999m;
            for (int dy = -BlockedTargetNeighborRadius; dy <= BlockedTargetNeighborRadius; dy++)
            {
                for (int dx = -BlockedTargetNeighborRadius; dx <= BlockedTargetNeighborRadius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = blockedCx + dx;
                    int ny = blockedCy + dy;
                    if (nx == startCx && ny == startCy) continue;
                    if (!_grid.IsPassable(nx, ny, rc)) continue;
                    int distToStart = Math.Abs(nx - startCx) + Math.Abs(ny - startCy);
                    fp dist = new fp(distToStart);
                    if (dist < bestDist) { bestDist = dist; bestCell = CellIndex(nx, ny); }
                }
            }
            return bestCell >= 0 ? bestCell : null;
        }
    }
}
