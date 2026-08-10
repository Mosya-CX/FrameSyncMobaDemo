using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class
        IntegratedPathfindingPipelineTests
    {
        private SimulationTickContextController
            tick;

        [SetUp]
        public void SetUp()
        {
            tick =
                new SimulationTickContextController();
            tick.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            tick.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void LaneAdvance_SelectsTeamFlowField()
        {
            PathGridMap2D grid = CreateGrid();
            FlowFieldRegistry registry =
                CreateRegistry(
                    grid,
                    1,
                    RadiusClass.Small);
            Unit unit = CreateUnit(
                1,
                new fp2((fp)2, (fp)10),
                new TeamId(1));
            var locomotion =
                new UnitLocomotionAgent(
                    unit,
                    grid);
            locomotion.SetFlowFieldRegistry(
                registry);

            RouteMoveRequest request =
                RouteMoveRequest.ToPosition(
                    new fp2((fp)18, (fp)10));
            request.Purpose =
                MovePurpose.LaneAdvance;
            request.AllowRVO = true;
            Assert.That(
                locomotion.AcceptRouteRequest(
                    request),
                Is.EqualTo(
                    MoveAcceptResult.Accepted));

            LocomotionResult result =
                locomotion.Evaluate();

            Assert.That(
                locomotion.Route.Kind,
                Is.EqualTo(
                    RouteKind.FlowField));
            Assert.That(
                locomotion.Route.FlowFieldKey,
                Is.EqualTo(
                    new FlowFieldKey(
                        1,
                        RadiusClass.Small)
                        .Packed));
            Assert.That(
                result.Status,
                Is.EqualTo(
                    RouteEvaluationStatus.Moving));
            Assert.That(
                result.DesiredDirection.x,
                Is.GreaterThan(fp.zero));
        }

        [Test]
        public void Chase_FirstTickBuildsPathBeforeRepathCooldown()
        {
            PathGridMap2D grid = CreateGrid();
            Unit chaser = CreateUnit(
                20,
                new fp2((fp)2, (fp)2),
                new TeamId(1));
            Unit target = CreateUnit(
                21,
                new fp2((fp)16, (fp)16),
                new TeamId(2));
            var world = new UnitWorld();
            world.RegisterUnit(chaser);
            world.RegisterUnit(target);
            chaser.World = world;
            target.World = world;
            chaser.Locomotion = new UnitLocomotionAgent(
                chaser,
                grid);

            RouteMoveRequest request =
                RouteMoveRequest.FollowUnit(
                    target.UnitUid,
                    fp.one,
                    MovePurpose.ChaseForAttack);
            Assert.That(
                chaser.Locomotion.AcceptRouteRequest(request),
                Is.EqualTo(MoveAcceptResult.Accepted));

            LocomotionResult result = chaser.Locomotion.Evaluate();

            Assert.That(
                result.Status,
                Is.EqualTo(RouteEvaluationStatus.Moving));
            Assert.That(result.HasMovement, Is.True);
            Assert.That(
                chaser.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Active));
            Assert.That(
                chaser.Locomotion.Route.NeedRepath,
                Is.False);
            Assert.That(
                chaser.Locomotion.Route.AStarPathCellIndices,
                Is.Not.Null.And.Not.Empty);

            LocomotionAgentSnapshot beforeRepeat = default;
            chaser.Locomotion.Capture(ref beforeRepeat);
            Assert.That(
                chaser.Locomotion.AcceptRouteRequest(request),
                Is.EqualTo(MoveAcceptResult.Rejected_AlreadyActive));
            LocomotionAgentSnapshot afterRepeat = default;
            chaser.Locomotion.Capture(ref afterRepeat);
            Assert.That(
                afterRepeat.FollowerState.PathCursor,
                Is.EqualTo(beforeRepeat.FollowerState.PathCursor));
            Assert.That(
                afterRepeat.Route.AStarPathCellIndices,
                Is.EqualTo(beforeRepeat.Route.AStarPathCellIndices));
        }

        [Test]
        public void
            Chase_DoesNotCompleteWhileOutsideAttackRange_AtPathDestinationCell()
        {
            PathGridMap2D grid = CreateGrid();
            Unit chaser = CreateUnit(
                30,
                new fp2((fp)2, (fp)2),
                new TeamId(1));
            // Target placed so the A* destination cell centre (the cell
            // containing the stable chase spot) lands outside the attack
            // radius: cell (15,15) centre (15.5,15.5) is ~1.27 away from
            // (16.4,16.4), while the chase stop distance is
            // range - own radius = 0.75. Reaching that cell must not mark
            // the chase task Completed; the unit keeps closing the gap.
            Unit target = CreateUnit(
                31,
                new fp2(
                    (fp)16.4m,
                    (fp)16.4m),
                new TeamId(2));
            // Realistic per-tick speed: stat 50 * world scale 0.01 =
            // 0.5 logic units per tick (below the follower reach
            // threshold), so waypoints are not overshot.
            chaser.StatHandler.SetStat(
                StatId.MoveSpeed,
                (fp)50m);
            var world = new UnitWorld();
            world.RegisterUnit(chaser);
            world.RegisterUnit(target);
            chaser.World = world;
            target.World = world;
            chaser.Locomotion = new UnitLocomotionAgent(
                chaser,
                grid);

            fp range = fp.one;
            RouteMoveRequest request =
                RouteMoveRequest.FollowUnit(
                    target.UnitUid,
                    range,
                    MovePurpose.ChaseForAttack);
            Assert.That(
                chaser.Locomotion.AcceptRouteRequest(request),
                Is.EqualTo(MoveAcceptResult.Accepted));

            const int maxTicks = 300;
            bool completed = false;
            for (int i = 0;
                 i < maxTicks && !completed;
                 i++)
            {
                LocomotionResult result =
                    chaser.Locomotion.Evaluate();
                chaser.MovementHandler
                    .ApplyRouteMovement(result);
                chaser.MovementHandler
                    .TickUpdate();

                if (chaser.Locomotion.CurrentTask.State ==
                    MovementTaskState.Completed)
                {
                    completed = true;
                    fp2 chaserPos =
                        chaser.PhysicsEntity
                            .Transform2D.Position;
                    fp2 targetPos =
                        target.PhysicsEntity
                            .Transform2D.Position;
                    fp dist =
                        fpmath.length(
                            targetPos -
                            chaserPos);
                    Assert.That(
                        dist,
                        Is.LessThanOrEqualTo(range),
                        "Chase task completed while still outside " +
                        $"attack range (dist={dist} range={range}).");
                }
            }

            Assert.That(
                completed,
                Is.True,
                "Chase never reached the attack range within " +
                "the tick budget.");
        }

        [Test]
        public void PointMove_UsesDirectOrAStarByGrid()
        {
            PathGridMap2D grid = CreateGrid();
            Unit unit = CreateUnit(
                2,
                new fp2((fp)2, (fp)2),
                new TeamId(1));
            var locomotion =
                new UnitLocomotionAgent(
                    unit,
                    grid);

            RouteMoveRequest direct =
                RouteMoveRequest.ToPosition(
                    new fp2((fp)4, (fp)2));
            locomotion.AcceptRouteRequest(
                direct);
            Assert.That(
                locomotion.Route.Kind,
                Is.EqualTo(RouteKind.Direct));
            Assert.That(
                locomotion.Evaluate()
                    .HasMovement,
                Is.True);

            RouteMoveRequest routed =
                RouteMoveRequest.ToPosition(
                    new fp2((fp)16, (fp)16));
            locomotion.AcceptRouteRequest(
                routed);
            Assert.That(
                locomotion.Route.Kind,
                Is.EqualTo(RouteKind.AStar));
            Assert.That(
                locomotion.Evaluate()
                    .Status,
                Is.EqualTo(
                    RouteEvaluationStatus.Moving));
        }

        [Test]
        public void
            FlowFieldRvoMovement_ProducesRepeatableMotion()
        {
            fp2[] first = ExecuteIntegratedStep();
            fp2[] second = ExecuteIntegratedStep();

            Assert.That(
                second,
                Is.EqualTo(first));
            Assert.That(
                first[0].x,
                Is.GreaterThan((fp)4));
            Assert.That(
                first[1].x,
                Is.GreaterThan((fp)4));
            Assert.That(
                first[0],
                Is.Not.EqualTo(first[1]));
        }

        [Test]
        public void RadiusAwareLineOfSight_BlocksLargeUnit()
        {
            PathGridMap2D grid = CreateGrid();
            grid.SetObstruction(
                new fp2(
                    (fp)7.5m,
                    (fp)9.5m),
                new fp2(
                    (fp)8.5m,
                    (fp)10.5m),
                true,
                RadiusClass.Large);

            Assert.That(
                grid.HasLineOfSight(
                    new fp2(
                        (fp)4,
                        (fp)10),
                    new fp2(
                        (fp)12,
                        (fp)10),
                    RadiusClass.Small),
                Is.True);
            Assert.That(
                grid.HasLineOfSight(
                    new fp2(
                        (fp)4,
                        (fp)10),
                    new fp2(
                        (fp)12,
                        (fp)10),
                    RadiusClass.Large),
                Is.False);
        }

        private static fp2[]
            ExecuteIntegratedStep()
        {
            PathGridMap2D grid = CreateGrid();
            FlowFieldRegistry registry =
                CreateRegistry(
                    grid,
                    1,
                    RadiusClass.Small);
            Unit first = CreateUnit(
                10,
                new fp2((fp)4, (fp)9.5m),
                new TeamId(1));
            Unit second = CreateUnit(
                11,
                new fp2((fp)4, (fp)10.5m),
                new TeamId(1));
            var units = new List<Unit>
            {
                first,
                second,
            };
            var physics = new PhysicsWorld();
            for (int i = 0; i < units.Count; i++)
            {
                units[i].Locomotion =
                    new UnitLocomotionAgent(
                        units[i],
                        grid);
                units[i].Locomotion
                    .SetFlowFieldRegistry(
                        registry);
                RouteMoveRequest request =
                    RouteMoveRequest.ToPosition(
                        new fp2(
                            (fp)18,
                            (fp)10));
                request.Purpose =
                    MovePurpose.LaneAdvance;
                request.AllowRVO = true;
                units[i].Locomotion
                    .AcceptRouteRequest(
                        request);
                physics.RegisterUnit(
                    units[i].PhysicsEntity);
            }

            var locomotion =
                new List<LocomotionResult>();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                locomotion.Add(
                    units[i].Locomotion
                        .Evaluate());
            }
            physics.BuildRvoGrid();
            new RvoOrchestrator().Step(
                new DeterministicRVOSystem(
                    RVOConfig.Default),
                physics,
                units,
                locomotion);
            var positions =
                new fp2[units.Count];
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                units[i].MovementHandler
                    .ApplyRouteMovement(
                        locomotion[i]);
                units[i].MovementHandler
                    .TickUpdate();
                positions[i] =
                    units[i].PhysicsEntity
                        .Transform2D.Position;
            }
            return positions;
        }

        private static Unit CreateUnit(
            int prefabId,
            fp2 position,
            TeamId teamId)
        {
            Unit unit =
                UnitTestFactory.CreateUnit(
                    new UnitUid(
                        0,
                        prefabId,
                        0),
                    UnitKind.Minion,
                    0,
                    teamId);
            unit.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    RadiusClassHelper
                        .SmallRadius));
            unit.PhysicsEntity
                .TeleportLogicPosition(
                    position);
            unit.MovementHandler
                .SetMoveSpeed(fp.one);
            unit.StatHandler.SetStat(
                StatId.MoveSpeed,
                fp.one);
            return unit;
        }

        private static FlowFieldRegistry
            CreateRegistry(
                PathGridMap2D grid,
                byte teamId,
                RadiusClass radiusClass)
        {
            var service =
                new TeamFlowFieldService(
                    grid);
            int[] costs =
                service.BuildLaneCostField(
                    new LaneTargetConfig
                    {
                        LaneIndex = 0,
                        Targets = new[]
                        {
                            new fp2(
                                (fp)18,
                                (fp)10),
                        },
                    },
                    radiusClass);
            TeamFlowFieldData field =
                service.BuildTeamFlowField(
                    teamId,
                    radiusClass,
                    new[] { costs },
                    FlowFieldBuildConfig.Default);
            var registry =
                new FlowFieldRegistry();
            registry.Register(field);
            return registry;
        }

        private static PathGridMap2D
            CreateGrid()
        {
            var grid =
                new PathGridMap2D();
            grid.Initialise(
                fp2.zero,
                new fp2(
                    (fp)20,
                    (fp)20),
                fp.one);
            return grid;
        }
    }
}
