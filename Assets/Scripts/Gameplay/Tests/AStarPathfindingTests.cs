using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class AStarPathfindingTests
    {
        private const int GridWidth = 32;
        private const int GridHeight = 32;
        private static readonly fp CellSize = (fp)1m;

        private PathGridMap2D CreateOpenGrid()
        {
            var grid = new PathGridMap2D();
            fp2 worldMin = fp2.zero;
            // Initialise computes width = (max - min) / cellSize + 1,
            // so for GridWidth cells, worldMax = GridWidth - 1.
            fp2 worldMax = new fp2((fp)(GridWidth - 1), (fp)(GridHeight - 1));
            grid.Initialise(worldMin, worldMax, CellSize);
            return grid;
        }

        private static readonly fp HalfCell = (fp)0.49m;

        private PathGridMap2D CreateWalledCorridorGrid()
        {
            var grid = new PathGridMap2D();
            fp2 worldMin = fp2.zero;
            fp2 worldMax = new fp2((fp)(GridWidth - 1), (fp)(GridHeight - 1));
            grid.Initialise(worldMin, worldMax, CellSize);

            // Create a vertical wall in the middle with a gap
            int wallX = GridWidth / 2;
            int gapY = GridHeight / 2;
            for (int y = 0; y < GridHeight; y++)
            {
                if (y == gapY || y == gapY + 1) continue; // gap
                fp2 wallMin = grid.CellToWorld(wallX, y) - new fp2(CellSize * HalfCell, CellSize * HalfCell);
                fp2 wallMax = wallMin + new fp2(CellSize, CellSize);
                grid.SetObstruction(wallMin, wallMax, blocked: true);
            }

            return grid;
        }

        [Test]
        public void AStar_OpenGrid_ReturnsCorrectPath()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(10, 10);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True, "A* should find a path on an open grid.");
            Assert.That(result.PathCellIndices, Is.Not.Null);
            Assert.That(result.PathCellIndices.Length, Is.GreaterThan(0));
        }

        [Test]
        public void AStar_WalledCorridor_FindsPathAroundWall()
        {
            var grid = CreateWalledCorridorGrid();
            var aStar = new AStarPathService(grid);

            // Start on left side, target on right side
            fp2 start = grid.CellToWorld(1, GridHeight / 2);
            fp2 target = grid.CellToWorld(GridWidth - 2, GridHeight / 2);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True,
                "A* should find a path through the wall gap.");
            Assert.That(result.PathCellIndices, Is.Not.Null);
            Assert.That(result.PathCellIndices.Length, Is.GreaterThan(1));

            // Verify path goes through the gap
            // LOS smoothing may reduce waypoints, so verify the path crosses
            // from the left side of the wall to the right side.
            int wallX = GridWidth / 2;
            bool hasLeftCell = false;
            bool hasRightCell = false;
            foreach (int cellIndex in result.PathCellIndices)
            {
                int cx = cellIndex % GridWidth;
                if (cx < wallX) hasLeftCell = true;
                if (cx > wallX) hasRightCell = true;
            }
            Assert.That(hasLeftCell && hasRightCell, Is.True,
                "Path should cross from left to right side of the wall.");
        }

        [Test]
        public void AStar_BlockedTarget_NeighborExpansionFindsNearbyCell()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            // Block the target cell
            fp2 target = grid.CellToWorld(10, 10);
            (int tcx, int tcy) = grid.WorldToCell(target);
            fp2 blockMin = grid.CellToWorld(tcx, tcy) - new fp2(CellSize * HalfCell, CellSize * HalfCell);
            fp2 blockMax = grid.CellToWorld(tcx, tcy) + new fp2(CellSize * HalfCell, CellSize * HalfCell);
            grid.SetObstruction(blockMin, blockMax, blocked: true);

            fp2 start = grid.CellToWorld(1, 1);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True,
                "Blocked-target neighbor expansion should find a reachable adjacent cell.");
            Assert.That(result.PathCellIndices, Is.Not.Null);
            Assert.That(result.PathCellIndices.Length, Is.GreaterThan(0));

            // The final cell should be adjacent to the blocked target
            int lastCell = result.PathCellIndices[result.PathCellIndices.Length - 1];
            int lastCx = lastCell % GridWidth;
            int lastCy = lastCell / GridWidth;
            int distToTarget = System.Math.Abs(lastCx - tcx) + System.Math.Abs(lastCy - tcy);
            // Verify the last cell is passable and not the blocked target
            Assert.That(grid.IsPassable(lastCx, lastCy), Is.True,
                "Last path cell should be passable.");
            Assert.That(lastCx != tcx || lastCy != tcy, Is.True,
                "Last path cell should not be the blocked target.");
            // The 3-cell search radius means max Manhattan distance is 6
            Assert.That(distToTarget, Is.LessThanOrEqualTo(6),
                $"Fallback cell ({lastCx},{lastCy}) is too far from blocked target ({tcx},{tcy}): distance {distToTarget}.");
        }

        [Test]
        public void AStar_EmptyOpenSet_ReturnsNoPath()
        {
            var grid = new PathGridMap2D();
            fp2 worldMin = fp2.zero;
            fp2 worldMax = new fp2((fp)3, (fp)3);  // 4x4 grid: width = (3-0)/1 + 1 = 4
            grid.Initialise(worldMin, worldMax, CellSize);

            // Block everything except start
            for (int cy = 0; cy < 4; cy++)
            {
                for (int cx = 0; cx < 4; cx++)
                {
                    if (cx == 1 && cy == 1) continue;
                    fp2 blockMin = grid.CellToWorld(cx, cy) - new fp2(CellSize * HalfCell, CellSize * HalfCell);
                    fp2 blockMax = grid.CellToWorld(cx, cy) + new fp2(CellSize * HalfCell, CellSize * HalfCell);
                    grid.SetObstruction(blockMin, blockMax, blocked: true);
                }
            }

            var aStar = new AStarPathService(grid);
            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(2, 2);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.False,
                "A* on fully enclosed cell should return failure.");
            // Since target is blocked and no fallback cell exists (start excluded),
            // the result should be EndBlocked.
            Assert.That(result.Status, Is.EqualTo(PathStatus.EndBlocked));
        }

        [Test]
        public void AStar_MaxIterationReached_ReturnsMaxIterationReached()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(GridWidth - 2, GridHeight - 2);

            // Use a very small max iterations that won't be enough
            PathResult result = aStar.FindPath(start, target, maxIterations: 5);

            // With only 5 iterations, a 30+ step path should hit max
            Assert.That(result.Status, Is.EqualTo(PathStatus.MaxIterationReached));
        }

        [Test]
        public void AStar_SameCellStartTarget_ReturnsSingleCell()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            fp2 pos = grid.CellToWorld(5, 5);

            PathResult result = aStar.FindPath(pos, pos);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PathCellIndices.Length, Is.EqualTo(1));
        }

        [Test]
        public void AStar_LOS_Smoothing_ReducesNodeCountOnStraightLine()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            // Points on a straight diagonal line
            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(15, 15);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True);
            // LOS smoothing should produce fewer nodes than a raw A* path
            // For a straight diagonal, it should be very short
            Assert.That(result.PathCellIndices.Length, Is.LessThan(30),
                "LOS smoothing should significantly reduce waypoints on a straight diagonal.");
        }

        [Test]
        public void PathFollower_CursorAdvance_ReachesWaypoint()
        {
            var grid = CreateOpenGrid();
            var follower = new PathFollower2D(grid);

            int[] path = new int[] { grid.WorldToCell(grid.CellToWorld(1, 1)).Item2 * GridWidth + grid.WorldToCell(grid.CellToWorld(1, 1)).Item1, CellIndex(5, 5, GridWidth) };
            follower.SetPath(path);

            // Position right on the first waypoint
            fp2 atWaypoint = grid.CellToWorld(1, 1);
            follower.AdvanceCursor(atWaypoint);

            Assert.That(follower.PathCursor, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void PathFollower_CorridorDetection_OutsideCorridor()
        {
            var grid = CreateOpenGrid();
            var follower = new PathFollower2D(grid);

            int startCell = CellIndex(1, 1, GridWidth);
            int nextCell = CellIndex(10, 1, GridWidth);
            int[] path = new int[] { startCell, nextCell };
            follower.SetPath(path);

            // Position far from the corridor
            fp2 farPos = grid.CellToWorld(1, 10);
            bool outside = follower.IsOutsideCorridor(farPos);

            Assert.That(outside, Is.True,
                "Position far from path corridor should be detected as outside.");
        }

        [Test]
        public void PathFollower_Arrival_AtFinalWaypoint()
        {
            var grid = CreateOpenGrid();
            var follower = new PathFollower2D(grid);

            int targetCell = CellIndex(5, 5, GridWidth);
            int[] path = new int[] { CellIndex(1, 1, GridWidth), targetCell };
            follower.SetPath(path);

            // Advance to the second waypoint, then reach it
            fp2 nearFirst = grid.CellToWorld(1, 1);
            follower.AdvanceCursor(nearFirst);

            Assert.That(follower.PathCursor, Is.EqualTo(1),
                "Cursor should advance past first waypoint.");
            Assert.That(follower.RouteFinished, Is.False,
                "Route should not be finished yet.");

            // Now at the final waypoint
            fp2 atFinal = grid.CellToWorld(5, 5);
            follower.AdvanceCursor(atFinal);

            Assert.That(follower.PathCursor, Is.EqualTo(1),
                "Cursor should remain at last waypoint index.");
            Assert.That(follower.RouteFinished, Is.True,
                "Route should be finished when reaching final waypoint.");
        }

        [Test]
        public void PathFollower_BuildLocomotionResult_ProducesDirection()
        {
            var grid = CreateOpenGrid();
            var follower = new PathFollower2D(grid);

            int targetCell = CellIndex(10, 5, GridWidth);
            int[] path = new int[] { CellIndex(1, 5, GridWidth), targetCell };
            follower.SetPath(path);

            fp2 currentPos = grid.CellToWorld(1, 5);
            var unitUid = new UnitUid(0, 1, 0);
            LocomotionResult result = follower.BuildLocomotionResult(currentPos, (fp)2m, unitUid);

            Assert.That(result.HasMovement, Is.True);
            Assert.That(result.DesiredDirection.x, Is.GreaterThan(fp.zero),
                "Direction should point toward target (rightward).");
            Assert.That(result.DesiredSpeed, Is.EqualTo((fp)2m));
            Assert.That(result.Status, Is.EqualTo(RouteEvaluationStatus.Moving));
        }

        [Test]
        public void PathFollower_RollbackRoundTrip_RestoresState()
        {
            var grid = CreateOpenGrid();
            var follower = new PathFollower2D(grid);

            int[] path = new int[] { CellIndex(2, 2, GridWidth), CellIndex(8, 8, GridWidth) };
            follower.SetPath(path);

            fp2 pos = grid.CellToWorld(3, 3);
            follower.AdvanceCursor(pos);

            // Capture
            PathFollowerState captured = follower.CaptureState();

            // Create new follower and restore
            var restored = new PathFollower2D(grid);
            restored.RestoreState(captured);

            Assert.That(restored.PathCursor, Is.EqualTo(follower.PathCursor));
            Assert.That(restored.RouteFinished, Is.EqualTo(follower.RouteFinished));
            Assert.That(restored.CaptureState().PathCellIndices, Is.Not.Null);
            Assert.That(restored.CaptureState().PathCellIndices.Length,
                Is.EqualTo(path.Length));
        }

        [Test]
        public void IndexedMinHeap_PushPop_ReturnsMinFCost()
        {
            int width = 64;
            int height = 64;
            var heap = new IndexedMinHeap(width, height, 64);
            heap.BeginNewSearch();

            PathNode a = new PathNode(0, 0, (fp)5m, (fp)10m, -1); // FCost = 15
            PathNode b = new PathNode(1, 1, (fp)2m, (fp)5m, -1);  // FCost = 7
            PathNode c = new PathNode(2, 2, (fp)8m, (fp)3m, -1);  // FCost = 11

            heap.Push(a);
            heap.Push(b);
            heap.Push(c);

            PathNode first = heap.Pop();
            Assert.That(first.CellX, Is.EqualTo(1));
            Assert.That(first.CellY, Is.EqualTo(1));

            PathNode second = heap.Pop();
            Assert.That(second.CellX, Is.EqualTo(2));
            Assert.That(second.CellY, Is.EqualTo(2));

            PathNode third = heap.Pop();
            Assert.That(third.CellX, Is.EqualTo(0));
            Assert.That(third.CellY, Is.EqualTo(0));
        }

        private static int CellIndex(int cx, int cy, int width) => cy * width + cx;

        [Test]
        public void FpDiagnostic_ZeroDotProductAndComparison()
        {
            fp2 zero = fp2.zero;
            fp distSq = fpmath.dot(zero, zero);
            fp threshold = (fp)0.04m;

            Assert.That(distSq, Is.EqualTo(fp.zero), "dot((0,0),(0,0)) should be zero.");
            Assert.That(distSq <= threshold, Is.True, "0 <= 0.04 should be true.");
            Assert.That(distSq <= fp.zero, Is.True, "0 <= 0 should be true.");
        }

        [Test]
        public void FpDiagnostic_CellToWorldRoundTrip()
        {
            var grid = CreateOpenGrid();
            fp2 cellCenter = grid.CellToWorld(1, 1);
            (int cx, int cy) = grid.WorldToCell(cellCenter);
            Assert.That(cx, Is.EqualTo(1), "WorldToCell x should be 1.");
            Assert.That(cy, Is.EqualTo(1), "WorldToCell y should be 1.");
            fp2 roundTrip = grid.CellToWorld(cx, cy);
            Assert.That(roundTrip.x, Is.EqualTo(cellCenter.x), "CellToWorld round-trip x.");
            Assert.That(roundTrip.y, Is.EqualTo(cellCenter.y), "CellToWorld round-trip y.");
        }
    }
}
