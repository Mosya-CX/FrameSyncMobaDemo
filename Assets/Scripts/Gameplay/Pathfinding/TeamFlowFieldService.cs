using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic flow-field construction and query service.
    /// Builds team-level static flow fields from lane target configurations.
    /// Runtime queries are O(1) cell lookups.
    /// (Pathfinding Design v13.1 section 8)
    /// </summary>
    public sealed class TeamFlowFieldService
    {
        private const int CostInf = int.MaxValue;
        private const int TargetSearchRadiusDefault = 6;
        private static readonly int StraightMoveCost = 10;
        private static readonly int DiagonalMoveCost = 14;

        private readonly PathGridMap2D _grid;

        // Reusable heap state for Dijkstra BFS
        private int[] _heapData;
        private int[] _heapPositions; // cellIndex → heap position (-1 = not in heap)
        private int _heapSize;
        private int[] _heapCostRef;   // reference to the cost array being built

        public TeamFlowFieldService(PathGridMap2D grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>
        /// Build a single lane's integrated cost field from its target positions.
        /// Dijkstra BFS outward from targets (cost=0 at target, increasing outward).
        /// (section 8.5 BuildLaneCostField)
        /// </summary>
        public int[] BuildLaneCostField(
            LaneTargetConfig laneConfig,
            RadiusClass rc,
            int targetSearchRadius = TargetSearchRadiusDefault)
        {
            int totalCells = _grid.Width * _grid.Height;
            int[] cost = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
                cost[i] = CostInf;

            EnsureHeapCapacity(totalCells);
            _heapCostRef = cost;
            _heapSize = 0;

            // Seed with all valid lane targets
            for (int t = 0; t < laneConfig.Targets.Length; t++)
            {
                (int tx, int ty) = _grid.WorldToCell(laneConfig.Targets[t]);
                int targetIdx = CellIndex(tx, ty);

                if (!_grid.IsPassable(tx, ty, rc))
                {
                    int? fallback = FindNearestWalkable(tx, ty, rc, targetSearchRadius);
                    if (!fallback.HasValue) continue;
                    targetIdx = fallback.Value;
                }

                if (cost[targetIdx] > 0)
                {
                    cost[targetIdx] = 0;
                    if (_heapPositions[targetIdx] < 0)
                        HeapPush(targetIdx);
                    else
                        HeapDecreaseKey(targetIdx);
                }
            }

            // Dijkstra expansion
            while (_heapSize > 0)
            {
                int current = HeapPop();
                if (current < 0) break;

                int cx = current % _grid.Width;
                int cy = current / _grid.Width;
                int currentCost = cost[current];

                for (int d = 1; d <= 8; d++)
                {
                    Dir8 dir = (Dir8)d;
                    var (dx, dy) = Dir8Helper.Delta(dir);
                    int nx = cx + dx;
                    int ny = cy + dy;

                    if (nx < 0 || nx >= _grid.Width || ny < 0 || ny >= _grid.Height)
                        continue;
                    if (!_grid.IsPassable(nx, ny, rc)) continue;

                    // Diagonal corner-cutting check
                    if (Dir8Helper.IsDiagonal(dir))
                    {
                        if (!_grid.IsPassable(cx + dx, cy, rc) ||
                            !_grid.IsPassable(cx, cy + dy, rc))
                            continue;
                    }

                    int moveCost = Dir8Helper.IsDiagonal(dir) ? DiagonalMoveCost : StraightMoveCost;
                    if (currentCost > CostInf - moveCost) continue;
                    int newCost = currentCost + moveCost;
                    int neighborIdx = CellIndex(nx, ny);

                    if (newCost < cost[neighborIdx])
                    {
                        cost[neighborIdx] = newCost;
                        if (_heapPositions[neighborIdx] >= 0)
                            HeapDecreaseKey(neighborIdx);
                        else
                            HeapPush(neighborIdx);
                    }
                }
            }

            return cost;
        }

        /// <summary>
        /// Build a team-level merged flow field from multiple lane cost fields.
        /// Phase 1: OwnerLane assignment (section 8.6)
        /// Phase 2: Direction selection via ChooseBestDescendingNeighbor (section 8.7)
        /// </summary>
        public TeamFlowFieldData BuildTeamFlowField(
            byte teamId,
            RadiusClass rc,
            int[][] laneCostFields,
            FlowFieldBuildConfig config)
        {
            int totalCells = _grid.Width * _grid.Height;
            var result = new TeamFlowFieldData
            {
                Key = new FlowFieldKey(teamId, rc),
                Cost = new int[totalCells],
                OwnerLane = new byte[totalCells],
                NextCell = new int[totalCells],
                DirectionCode = new byte[totalCells],
                Width = _grid.Width,
                Height = _grid.Height,
                CellCount = totalCells,
            };

            int laneCount = laneCostFields.Length;

            // Phase 1: OwnerLane merge
            for (int i = 0; i < totalCells; i++)
            {
                if (!_grid.IsPassable(i % _grid.Width, i / _grid.Width, rc))
                {
                    result.Cost[i] = CostInf;
                    result.OwnerLane[i] = 255;
                    result.NextCell[i] = -1;
                    result.DirectionCode[i] = (byte)Dir8.None;
                    continue;
                }

                int bestLane = -1;
                int bestCost = CostInf;

                for (int lane = 0; lane < laneCount; lane++)
                {
                    int laneCost = laneCostFields[lane][i];
                    if (laneCost < bestCost)
                    {
                        bestCost = laneCost;
                        bestLane = lane;
                    }
                    else if (laneCost == bestCost && lane < bestLane)
                    {
                        bestLane = lane;
                    }
                }

                result.Cost[i] = bestCost;
                result.OwnerLane[i] = (byte)bestLane;
            }

            // Phase 2: Direction selection
            for (int i = 0; i < totalCells; i++)
            {
                if (result.Cost[i] == CostInf) continue;

                int ownerLaneIdx = result.OwnerLane[i];
                if (ownerLaneIdx >= laneCount) continue;

                int[] laneCost = laneCostFields[ownerLaneIdx];
                int bestNeighbor = ChooseBestDescendingNeighbor(i, laneCost, rc, config);
                if (bestNeighbor >= 0)
                {
                    result.NextCell[i] = bestNeighbor;
                    result.DirectionCode[i] = (byte)Dir8Helper.FromCellDelta(i, bestNeighbor, _grid.Width);
                }
                else
                {
                    result.NextCell[i] = -1;
                    result.DirectionCode[i] = (byte)Dir8.None;
                }
            }

            return result;
        }

        /// <summary>
        /// Select the best descending neighbor for a cell.
        /// (section 8.7 ChooseBestDescendingNeighbor)
        /// Must satisfy: laneCost[neighbor] &lt; laneCost[current].
        /// </summary>
        private int ChooseBestDescendingNeighbor(
            int cell, int[] laneCost, RadiusClass rc, FlowFieldBuildConfig config)
        {
            int cx = cell % _grid.Width;
            int cy = cell / _grid.Width;
            int currentCost = laneCost[cell];
            if (currentCost == CostInf) return -1;

            int bestCell = -1;
            int bestScore = int.MinValue;

            for (int d = 1; d <= 8; d++)
            {
                Dir8 dir = (Dir8)d;
                var (dx, dy) = Dir8Helper.Delta(dir);
                int nx = cx + dx;
                int ny = cy + dy;

                if (nx < 0 || nx >= _grid.Width || ny < 0 || ny >= _grid.Height)
                    continue;
                if (!_grid.IsPassable(nx, ny, rc)) continue;

                // Diagonal corner-cutting check
                if (Dir8Helper.IsDiagonal(dir))
                {
                    if (!_grid.IsPassable(cx + dx, cy, rc) ||
                        !_grid.IsPassable(cx, cy + dy, rc))
                        continue;
                }

                int neighborIdx = CellIndex(nx, ny);
                int neighborCost = laneCost[neighborIdx];

                // Strictly descending constraint
                if (neighborCost >= currentCost) continue;

                // Score
                int costDelta = currentCost - neighborCost;
                int score = costDelta * config.CostDropWeight;
                score += WallTangentScore(cell, neighborIdx, rc) * config.WallAlignWeight;
                score -= Dir8Helper.Priority(dir); // Tie-breaker

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = neighborIdx;
                }
                else if (score == bestScore && bestCell >= 0)
                {
                    if (neighborIdx < bestCell)
                        bestCell = neighborIdx;
                }
            }

            return bestCell;
        }

        /// <summary>
        /// Score neighbor for staying along wall edges.
        /// Bonus if current cell is near walls AND neighbor is also near walls.
        /// </summary>
        private int WallTangentScore(int cell, int neighbor, RadiusClass rc)
        {
            int cx = cell % _grid.Width;
            int cy = cell / _grid.Width;

            bool currentNearWall = false;
            for (int d = 1; d <= 8; d++)
            {
                var (dx, dy) = Dir8Helper.Delta((Dir8)d);
                int wx = cx + dx;
                int wy = cy + dy;
                if (wx < 0 || wx >= _grid.Width || wy < 0 || wy >= _grid.Height ||
                    !_grid.IsPassable(wx, wy, rc))
                {
                    currentNearWall = true;
                    break;
                }
            }

            if (!currentNearWall) return 0;

            int nx = neighbor % _grid.Width;
            int ny = neighbor / _grid.Width;
            for (int d = 1; d <= 8; d++)
            {
                var (dx, dy) = Dir8Helper.Delta((Dir8)d);
                int wx = nx + dx;
                int wy = ny + dy;
                if (wx < 0 || wx >= _grid.Width || wy < 0 || wy >= _grid.Height ||
                    !_grid.IsPassable(wx, wy, rc))
                    return 1; // Both near walls → wall-hugging bonus
            }

            return 0;
        }

        /// <summary>
        /// Query the flow direction at a world position.
        /// O(1) cell lookup. (section 8.8 GetFlowDirection)
        /// </summary>
        public fp2 GetFlowDirection(in TeamFlowFieldData field, fp2 position)
        {
            if (!field.IsValid) return fp2.zero;

            (int cx, int cy) = _grid.WorldToCell(position);
            if (cx < 0 || cx >= field.Width || cy < 0 || cy >= field.Height)
                return fp2.zero;

            int index = cy * field.Width + cx;
            byte dirCode = field.DirectionCode[index];
            if (dirCode == (byte)Dir8.None) return fp2.zero;

            return Dir8Helper.ToFP2((Dir8)dirCode);
        }

        private int? FindNearestWalkable(int cx, int cy, RadiusClass rc, int maxRadius)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r) continue;
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx >= 0 && nx < _grid.Width && ny >= 0 && ny < _grid.Height &&
                            _grid.IsPassable(nx, ny, rc))
                            return CellIndex(nx, ny);
                    }
                }
            }
            return null;
        }

        private int CellIndex(int cx, int cy) => cy * _grid.Width + cx;

        #region Deterministic Integer Min-Heap

        private void EnsureHeapCapacity(int size)
        {
            if (_heapData == null || _heapData.Length < size)
            {
                _heapData = new int[size];
                _heapPositions = new int[size];
            }
            for (int i = 0; i < size; i++)
                _heapPositions[i] = -1;
            _heapSize = 0;
        }

        private void HeapPush(int cellIndex)
        {
            _heapData[_heapSize] = cellIndex;
            _heapPositions[cellIndex] = _heapSize;
            _heapSize++;
            HeapSiftUp(_heapSize - 1);
        }

        private int HeapPop()
        {
            if (_heapSize == 0) return -1;
            int result = _heapData[0];
            _heapPositions[result] = -1;
            _heapSize--;
            if (_heapSize > 0)
            {
                _heapData[0] = _heapData[_heapSize];
                _heapPositions[_heapData[0]] = 0;
                HeapSiftDown(0);
            }
            return result;
        }

        private void HeapDecreaseKey(int cellIndex)
        {
            int pos = _heapPositions[cellIndex];
            if (pos >= 0) HeapSiftUp(pos);
        }

        private void HeapSiftUp(int pos)
        {
            int cell = _heapData[pos];
            int key = _heapCostRef[cell];
            while (pos > 0)
            {
                int parent = (pos - 1) / 2;
                if (key >= _heapCostRef[_heapData[parent]]) break;
                _heapData[pos] = _heapData[parent];
                _heapPositions[_heapData[pos]] = pos;
                pos = parent;
            }
            _heapData[pos] = cell;
            _heapPositions[cell] = pos;
        }

        private void HeapSiftDown(int pos)
        {
            int cell = _heapData[pos];
            int key = _heapCostRef[cell];
            int size = _heapSize;
            while (true)
            {
                int left = pos * 2 + 1;
                int right = left + 1;
                int smallest = pos;

                if (left < size && _heapCostRef[_heapData[left]] < key)
                    smallest = left;
                if (right < size && _heapCostRef[_heapData[right]] < _heapCostRef[_heapData[smallest]])
                    smallest = right;
                if (smallest == pos) break;

                _heapData[pos] = _heapData[smallest];
                _heapPositions[_heapData[pos]] = pos;
                pos = smallest;
            }
            _heapData[pos] = cell;
            _heapPositions[cell] = pos;
        }

        #endregion
    }
}
