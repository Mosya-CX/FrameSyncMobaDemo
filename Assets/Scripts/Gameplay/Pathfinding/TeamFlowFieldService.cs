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
            if (!laneConfig.IsValid)
                throw new ArgumentException(
                    "Lane flow configuration requires at least one target.",
                    nameof(laneConfig));
            if (laneConfig.GuideHalfWidth < fp.zero ||
                laneConfig.GuideCostPerCell < 0 ||
                laneConfig.OffGuideCostPerCell < 0)
                throw new ArgumentException(
                    "Lane guide width and cost must be nonnegative.",
                    nameof(laneConfig));

            int totalCells = _grid.Width * _grid.Height;
            int[] cost = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
                cost[i] = CostInf;
            int[] guidePotential =
                BuildGuidePotentialField(
                    laneConfig,
                    totalCells);

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

                    int moveCost = Dir8Helper.IsDiagonal(dir)
                        ? DiagonalMoveCost
                        : StraightMoveCost;
                    int currentGuidePotential =
                        guidePotential != null
                            ? guidePotential[current]
                            : 0;
                    int neighborIdx = CellIndex(nx, ny);
                    int neighborGuidePotential =
                        guidePotential != null
                            ? guidePotential[neighborIdx]
                            : 0;
                    if (currentGuidePotential == CostInf ||
                        neighborGuidePotential == CostInf)
                        continue;
                    int guideCost =
                        neighborGuidePotential >
                            currentGuidePotential
                            ? neighborGuidePotential -
                              currentGuidePotential
                            : 0;
                    if (guideCost >
                            CostInf - moveCost ||
                        currentCost >
                            CostInf -
                            moveCost -
                            guideCost)
                        continue;
                    int newCost =
                        currentCost +
                        moveCost +
                        guideCost;
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

        private int[] BuildGuidePotentialField(
            in LaneTargetConfig laneConfig,
            int totalCells)
        {
            fp2[] guide = laneConfig.GuidePoints;
            if (guide == null ||
                guide.Length < 2 ||
                (laneConfig.GuideCostPerCell == 0 &&
                 laneConfig.OffGuideCostPerCell == 0))
                return null;
            var result = new int[totalCells];
            for (int cell = 0;
                 cell < totalCells;
                 cell++)
            {
                result[cell] =
                    CalculateGuidePotential(
                        cell % _grid.Width,
                        cell / _grid.Width,
                        laneConfig);
            }
            return result;
        }

        private int CalculateGuidePotential(
            int cellX,
            int cellY,
            in LaneTargetConfig laneConfig)
        {
            fp2[] guide = laneConfig.GuidePoints;
            if (guide == null ||
                guide.Length < 2 ||
                (laneConfig.GuideCostPerCell == 0 &&
                 laneConfig.OffGuideCostPerCell == 0))
                return 0;

            fp2 position =
                _grid.CellToWorld(
                    cellX,
                    cellY);
            fp minimumDistanceSq =
                DistanceToSegmentSq(
                    position,
                    guide[0],
                    guide[1]);
            for (int i = 1;
                 i < guide.Length - 1;
                 i++)
            {
                fp distanceSq =
                    DistanceToSegmentSq(
                        position,
                        guide[i],
                        guide[i + 1]);
                if (distanceSq <
                    minimumDistanceSq)
                    minimumDistanceSq =
                        distanceSq;
            }

            fp distance =
                fpmath.sqrt(
                    minimumDistanceSq);
            int guideCost =
                CalculateQuadraticPotential(
                    distance /
                    _grid.CellSize,
                    laneConfig
                        .GuideCostPerCell);
            fp corridorWidth =
                laneConfig.GuideHalfWidth;
            if (distance <= corridorWidth)
                return guideCost;

            fp outsideDistance =
                distance -
                corridorWidth;
            int outsideCost =
                CalculateQuadraticPotential(
                    outsideDistance /
                    _grid.CellSize,
                    laneConfig
                        .OffGuideCostPerCell);
            if (guideCost == int.MaxValue ||
                outsideCost == int.MaxValue ||
                guideCost >
                    int.MaxValue -
                    outsideCost)
                return int.MaxValue;
            return guideCost +
                outsideCost;
        }

        private static int CalculateQuadraticPotential(
            fp distanceInCells,
            int weight)
        {
            if (distanceInCells <= fp.zero ||
                weight <= 0)
                return 0;
            fp potential =
                distanceInCells *
                distanceInCells *
                (fp)weight;
            if (potential >=
                (fp)int.MaxValue)
                return int.MaxValue;
            return (int)potential;
        }

        private static fp DistanceToSegmentSq(
            fp2 point,
            fp2 start,
            fp2 end)
        {
            fp2 segment = end - start;
            fp lengthSq =
                fpmath.lengthsq(segment);
            if (lengthSq <= fp.zero)
                return fpmath.lengthsq(
                    point - start);
            fp t =
                fpmath.dot(
                    point - start,
                    segment) /
                lengthSq;
            t = fpmath.clamp(
                t,
                fp.zero,
                fp.one);
            fp2 closest =
                start +
                segment * t;
            return fpmath.lengthsq(
                point - closest);
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
            return BuildTeamFlowField(
                teamId,
                rc,
                laneCostFields,
                null,
                config);
        }

        public TeamFlowFieldData BuildTeamFlowField(
            byte teamId,
            RadiusClass rc,
            int[][] laneCostFields,
            LaneTargetConfig[] laneConfigs,
            FlowFieldBuildConfig config)
        {
            if (laneCostFields == null ||
                laneCostFields.Length == 0)
                throw new ArgumentException(
                    "At least one lane cost field is required.",
                    nameof(laneCostFields));
            if (laneConfigs != null &&
                laneConfigs.Length !=
                    laneCostFields.Length)
                throw new ArgumentException(
                    "Lane guide configuration count must match lane costs.",
                    nameof(laneConfigs));
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
                    if (laneConfigs != null &&
                        laneCost != CostInf)
                    {
                        laneCost =
                            AddOwnershipPenalty(
                                laneCost,
                                i,
                                laneConfigs[lane],
                                config.OwnershipWeight);
                    }
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
                int bestNeighbor =
                    ChooseBestDescendingNeighbor(
                        i,
                        laneCost,
                        rc,
                        config,
                        laneConfigs != null,
                        laneConfigs != null
                            ? laneConfigs[ownerLaneIdx]
                            : default);
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

        private int AddOwnershipPenalty(
            int baseCost,
            int cell,
            in LaneTargetConfig laneConfig,
            int ownershipWeight)
        {
            fp2[] guide = laneConfig.GuidePoints;
            if (ownershipWeight <= 0 ||
                guide == null ||
                guide.Length < 2)
                return baseCost;
            fp2 position =
                _grid.CellToWorld(
                    cell % _grid.Width,
                    cell / _grid.Width);
            fp minimumDistanceSq =
                DistanceToSegmentSq(
                    position,
                    guide[0],
                    guide[1]);
            for (int i = 1;
                 i < guide.Length - 1;
                 i++)
            {
                fp candidate =
                    DistanceToSegmentSq(
                        position,
                        guide[i],
                        guide[i + 1]);
                if (candidate < minimumDistanceSq)
                    minimumDistanceSq = candidate;
            }
            fp distanceInCells =
                fpmath.sqrt(
                    minimumDistanceSq) /
                _grid.CellSize;
            fp rawPenalty =
                distanceInCells *
                (fp)ownershipWeight;
            if (rawPenalty >=
                    (fp)int.MaxValue ||
                baseCost >
                    int.MaxValue -
                    (int)rawPenalty)
                return int.MaxValue;
            return baseCost +
                (int)rawPenalty;
        }

        /// <summary>
        /// Select the best descending neighbor for a cell.
        /// (section 8.7 ChooseBestDescendingNeighbor)
        /// Must satisfy: laneCost[neighbor] &lt; laneCost[current].
        /// </summary>
        private int ChooseBestDescendingNeighbor(
            int cell,
            int[] laneCost,
            RadiusClass rc,
            FlowFieldBuildConfig config,
            bool hasLaneGuide,
            in LaneTargetConfig laneConfig)
        {
            int cx = cell % _grid.Width;
            int cy = cell / _grid.Width;
            int currentCost = laneCost[cell];
            if (currentCost == CostInf) return -1;

            int bestCell = -1;
            long bestScore = long.MinValue;
            fp2 laneTangent = fp2.zero;
            fp2 inwardDirection = fp2.zero;
            fp normalizedLaneDistance = fp.zero;
            bool hasGuideFrame =
                hasLaneGuide &&
                TryGetGuideFrame(
                    cell,
                    laneConfig,
                    out laneTangent,
                    out inwardDirection,
                    out normalizedLaneDistance);

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
                long score =
                    (long)costDelta *
                    config.CostDropWeight;
                score +=
                    (long)WallTangentScore(
                        cell,
                        neighborIdx,
                        rc) *
                    config.WallAlignWeight;
                if (hasGuideFrame)
                {
                    fp2 candidateDirection =
                        Dir8Helper.ToFP2(dir);
                    score +=
                        (long)ScaleDot(
                            candidateDirection,
                            laneTangent) *
                        config.SmoothWeight;
                    score +=
                        (long)ScaleDot(
                            candidateDirection,
                            inwardDirection,
                            normalizedLaneDistance) *
                        config.LaneWeight;
                }
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

        private bool TryGetGuideFrame(
            int cell,
            in LaneTargetConfig laneConfig,
            out fp2 tangent,
            out fp2 inward,
            out fp normalizedDistance)
        {
            tangent = fp2.zero;
            inward = fp2.zero;
            normalizedDistance = fp.zero;
            fp2[] guide = laneConfig.GuidePoints;
            if (guide == null ||
                guide.Length < 2 ||
                laneConfig.Targets == null ||
                laneConfig.Targets.Length == 0)
                return false;

            fp2 position =
                _grid.CellToWorld(
                    cell % _grid.Width,
                    cell / _grid.Width);
            fp2 target = laneConfig.Targets[0];
            bool targetAtEnd =
                fpmath.lengthsq(
                    target -
                    guide[guide.Length - 1]) <=
                fpmath.lengthsq(
                    target -
                    guide[0]);
            int bestSegment = 0;
            fp bestDistanceSq =
                new fp(int.MaxValue);
            fp2 closest = guide[0];
            for (int i = 0;
                 i < guide.Length - 1;
                 i++)
            {
                fp2 segment =
                    guide[i + 1] -
                    guide[i];
                fp lengthSq =
                    fpmath.lengthsq(segment);
                fp t = lengthSq > fp.zero
                    ? fpmath.clamp(
                        fpmath.dot(
                            position - guide[i],
                            segment) /
                        lengthSq,
                        fp.zero,
                        fp.one)
                    : fp.zero;
                fp2 candidateClosest =
                    guide[i] +
                    segment * t;
                fp distanceSq =
                    fpmath.lengthsq(
                        position -
                        candidateClosest);
                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq &&
                     (targetAtEnd
                         ? i > bestSegment
                         : i < bestSegment)))
                {
                    bestDistanceSq = distanceSq;
                    bestSegment = i;
                    closest = candidateClosest;
                }
            }

            fp2 segmentDirection =
                guide[bestSegment + 1] -
                guide[bestSegment];
            if (fpmath.lengthsq(segmentDirection) <=
                fp.zero)
                return false;
            tangent =
                fpmath.normalize(
                    targetAtEnd
                        ? segmentDirection
                        : -segmentDirection);

            fp2 toSkeleton =
                closest -
                position;
            fp distance =
                fpmath.sqrt(
                    bestDistanceSq);
            if (distance > fp.zero)
                inward = toSkeleton /
                    distance;
            fp normalizationWidth =
                laneConfig.GuideHalfWidth >
                    _grid.CellSize
                    ? laneConfig.GuideHalfWidth
                    : _grid.CellSize;
            normalizedDistance =
                distance /
                normalizationWidth;
            return true;
        }

        private static int ScaleDot(
            fp2 direction,
            fp2 reference)
        {
            return ScaleDot(
                direction,
                reference,
                fp.one);
        }

        private static int ScaleDot(
            fp2 direction,
            fp2 reference,
            fp multiplier)
        {
            fp scaled =
                fpmath.dot(
                    direction,
                    reference) *
                multiplier *
                (fp)100;
            if (scaled >= (fp)int.MaxValue)
                return int.MaxValue;
            if (scaled <= (fp)int.MinValue)
                return int.MinValue;
            return (int)scaled;
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
            while (pos > 0)
            {
                int parent = (pos - 1) / 2;
                if (!HeapEntryLess(
                        cell,
                        _heapData[parent]))
                    break;
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
            int size = _heapSize;
            while (true)
            {
                int left = pos * 2 + 1;
                int right = left + 1;
                int smallest = pos;

                if (left < size &&
                    HeapEntryLess(
                        _heapData[left],
                        cell))
                    smallest = left;
                if (right < size &&
                    HeapEntryLess(
                        _heapData[right],
                        smallest == pos
                            ? cell
                            : _heapData[smallest]))
                    smallest = right;
                if (smallest == pos) break;

                _heapData[pos] = _heapData[smallest];
                _heapPositions[_heapData[pos]] = pos;
                pos = smallest;
            }
            _heapData[pos] = cell;
            _heapPositions[cell] = pos;
        }

        private bool HeapEntryLess(
            int leftCell,
            int rightCell)
        {
            int comparison =
                _heapCostRef[leftCell]
                    .CompareTo(
                        _heapCostRef[rightCell]);
            return comparison < 0 ||
                (comparison == 0 &&
                 leftCell < rightCell);
        }

        #endregion
    }
}
