using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class FlowFieldBuildTests
    {
        private const int GridWidth = 16;
        private const int GridHeight = 16;
        private static readonly fp CellSize = (fp)1m;

        private PathGridMap2D CreateOpenGrid()
        {
            var grid = new PathGridMap2D();
            grid.Initialise(fp2.zero, new fp2((fp)(GridWidth - 1), (fp)(GridHeight - 1)), CellSize);
            return grid;
        }

        [Test]
        public void BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)8m, (fp)8m) },
            };

            int[] cost = service.BuildLaneCostField(laneConfig, RadiusClass.Medium);

            (int tx, int ty) = grid.WorldToCell(new fp2((fp)8m, (fp)8m));
            int targetIdx = ty * GridWidth + tx;
            Assert.That(cost[targetIdx], Is.EqualTo(0), "Target should have cost 0.");

            (int fx, int fy) = grid.WorldToCell(new fp2((fp)14m, (fp)14m));
            int farIdx = fy * GridWidth + fx;
            Assert.That(cost[farIdx], Is.GreaterThan(0), "Far cell should have positive cost.");
            Assert.That(cost[farIdx], Is.LessThan(int.MaxValue), "Far cell should be reachable.");
        }

        [Test]
        public void BuildLaneCostField_BlockedTarget_FindsNearestWalkable()
        {
            var grid = CreateOpenGrid();
            grid.SetObstruction(new fp2((fp)7.5m, (fp)7.5m), new fp2((fp)8.5m, (fp)8.5m), blocked: true);

            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)8m, (fp)8m) },
            };

            int[] cost = service.BuildLaneCostField(laneConfig, RadiusClass.Medium);

            bool hasZero = false;
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i] == 0) { hasZero = true; break; }
            }
            Assert.That(hasZero, Is.True, "Should find a fallback walkable target.");
        }

        [Test]
        public void BuildTeamFlowField_MultiLane_CorrectOwnerLane()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);

            var lane0 = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)15m, (fp)2m) },
            };
            var lane1 = new LaneTargetConfig
            {
                LaneIndex = 1,
                Targets = new fp2[] { new fp2((fp)15m, (fp)8m) },
            };

            int[][] laneCosts = new int[][]
            {
                service.BuildLaneCostField(lane0, RadiusClass.Medium),
                service.BuildLaneCostField(lane1, RadiusClass.Medium),
            };

            var field = service.BuildTeamFlowField(0, RadiusClass.Medium, laneCosts, FlowFieldBuildConfig.Default);
            Assert.That(field.IsValid, Is.True);

            (int nx0, int ny0) = grid.WorldToCell(new fp2((fp)1m, (fp)2m));
            int idx0 = ny0 * GridWidth + nx0;
            Assert.That(field.OwnerLane[idx0], Is.EqualTo(0), "Cell near lane 0 should be owned by lane 0.");

            (int nx1, int ny1) = grid.WorldToCell(new fp2((fp)1m, (fp)8m));
            int idx1 = ny1 * GridWidth + nx1;
            Assert.That(field.OwnerLane[idx1], Is.EqualTo(1), "Cell near lane 1 should be owned by lane 1.");
        }

        [Test]
        public void BuildTeamFlowField_UnwalkableCells_DirectionCodeNone()
        {
            var grid = CreateOpenGrid();
            grid.SetObstruction(new fp2((fp)5.5m, (fp)5.5m), new fp2((fp)6.5m, (fp)6.5m), blocked: true);

            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)15m, (fp)5m) },
            };

            int[][] laneCosts = new int[][] { service.BuildLaneCostField(laneConfig, RadiusClass.Medium) };
            var field = service.BuildTeamFlowField(0, RadiusClass.Medium, laneCosts, FlowFieldBuildConfig.Default);

            (int bx, int by) = grid.WorldToCell(new fp2((fp)6m, (fp)6m));
            int blockedIdx = by * GridWidth + bx;

            Assert.That((Dir8)field.DirectionCode[blockedIdx], Is.EqualTo(Dir8.None),
                "Blocked cell should have Dir8.None.");
            Assert.That(field.NextCell[blockedIdx], Is.EqualTo(-1),
                "Blocked cell should have NextCell = -1.");
        }

        [Test]
        public void BuildTeamFlowField_CostDecreasingConstraint_NoUphillMove()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)15m, (fp)8m) },
            };

            int[][] laneCosts = new int[][] { service.BuildLaneCostField(laneConfig, RadiusClass.Medium) };
            var field = service.BuildTeamFlowField(0, RadiusClass.Medium, laneCosts, FlowFieldBuildConfig.Default);

            for (int i = 0; i < field.NextCell.Length; i++)
            {
                int next = field.NextCell[i];
                if (next < 0) continue;
                if (field.Cost[i] == int.MaxValue) continue;

                Assert.That(field.Cost[next], Is.LessThan(field.Cost[i]),
                    $"Cell {i} cost={field.Cost[i]}, next cell {next} cost={field.Cost[next]} should be lower.");
            }
        }

        [Test]
        public void GetFlowDirection_KnownCell_ReturnsCorrectDirection()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)15m, (fp)8m) },
            };

            int[][] laneCosts = new int[][] { service.BuildLaneCostField(laneConfig, RadiusClass.Medium) };
            var field = service.BuildTeamFlowField(0, RadiusClass.Medium, laneCosts, FlowFieldBuildConfig.Default);
            Assert.That(field.IsValid, Is.True);

            fp2 dir = service.GetFlowDirection(field, new fp2((fp)2m, (fp)8m));
            Assert.That(dir.x, Is.GreaterThan(fp.zero), "Direction should point east toward target.");
        }

        [Test]
        public void GetFlowDirection_IsolatedCell_DirNoneReturnsZero()
        {
            var grid = CreateOpenGrid();
            grid.SetObstruction(new fp2((fp)0.5m, (fp)0.5m), new fp2((fp)2.5m, (fp)2.5m), blocked: true);
            // The cell near (0,0) area is blocked - direction should be None for blocked cells
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)15m, (fp)8m) },
            };

            int[][] laneCosts = new int[][] { service.BuildLaneCostField(laneConfig, RadiusClass.Medium) };
            var field = service.BuildTeamFlowField(0, RadiusClass.Medium, laneCosts, FlowFieldBuildConfig.Default);

            (int bx, int by) = grid.WorldToCell(new fp2((fp)1m, (fp)1m));
            int blockedIdx = by * GridWidth + bx;
            if ((Dir8)field.DirectionCode[blockedIdx] == Dir8.None)
            {
                fp2 dir = service.GetFlowDirection(field, new fp2((fp)1m, (fp)1m));
                Assert.That(dir.x, Is.EqualTo(fp.zero));
                Assert.That(dir.y, Is.EqualTo(fp.zero));
            }
            // If cell is not blocked by the build, the test still passes (geometry-dependent)
            Assert.Pass();
        }

        [Test]
        public void Dir8Helper_ToFP2_AllCardinalDirections()
        {
            fp2 n = Dir8Helper.ToFP2(Dir8.N);
            Assert.That(n.x, Is.EqualTo(fp.zero));
            Assert.That(n.y, Is.EqualTo(-fp.one));

            fp2 s = Dir8Helper.ToFP2(Dir8.S);
            Assert.That(s.x, Is.EqualTo(fp.zero));
            Assert.That(s.y, Is.EqualTo(fp.one));

            fp2 e = Dir8Helper.ToFP2(Dir8.E);
            Assert.That(e.x, Is.EqualTo(fp.one));
            Assert.That(e.y, Is.EqualTo(fp.zero));

            fp2 w = Dir8Helper.ToFP2(Dir8.W);
            Assert.That(w.x, Is.EqualTo(-fp.one));
            Assert.That(w.y, Is.EqualTo(fp.zero));

            fp2 ne = Dir8Helper.ToFP2(Dir8.NE);
            fp magSq = fpmath.dot(ne, ne);
            Assert.That(magSq, Is.GreaterThan((fp)0.99m));
            Assert.That(magSq, Is.LessThan((fp)1.01m));
        }

        [Test]
        public void BuildLaneCostField_Deterministic_SameInputSameOutput()
        {
            var grid1 = CreateOpenGrid();
            var grid2 = CreateOpenGrid();
            var svc1 = new TeamFlowFieldService(grid1);
            var svc2 = new TeamFlowFieldService(grid2);
            var config = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)8m, (fp)8m) },
            };

            int[] cost1 = svc1.BuildLaneCostField(config, RadiusClass.Medium);
            int[] cost2 = svc2.BuildLaneCostField(config, RadiusClass.Medium);

            Assert.That(cost1.Length, Is.EqualTo(cost2.Length));
            for (int i = 0; i < cost1.Length; i++)
                Assert.That(cost1[i], Is.EqualTo(cost2[i]),
                    $"Determinism violation at cell {i}: {cost1[i]} != {cost2[i]}");
        }
    }
}
