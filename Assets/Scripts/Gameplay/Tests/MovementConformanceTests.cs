using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class MovementConformanceTests
    {
        private sealed class RadiusProbe :
            IMovementCollisionResolver
        {
            public fp Radius;
            public RadiusClass RadiusClass;
            public UnitUid SelfUid;

            public fp2 ClampPosition(
                fp2 desiredPosition,
                fp2 currentPosition,
                fp unitRadius,
                RadiusClass radiusClass,
                UnitUid selfUid)
            {
                Radius = unitRadius;
                RadiusClass = radiusClass;
                SelfUid = selfUid;
                return desiredPosition;
            }
        }

        private SimulationTickContextController _tick;

        [SetUp]
        public void SetUp()
        {
            _tick =
                new SimulationTickContextController();
            _tick.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            _tick.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void ForcedMove_OverridesRouteMove()
        {
            Unit unit = CreateUnit(100, fp2.zero);
            CrowdControlAddResult added =
                unit.CrowdControl.Add(
                    CreateForcedMove(
                        unit.UnitUid,
                        new fp2(fp.one, fp.zero),
                        2,
                        5));
            Assert.That(added.Added, Is.True);

            unit.MovementHandler.ApplyRouteMovement(
                new LocomotionResult
                {
                    UnitUid = unit.UnitUid,
                    HasMovement = true,
                    DesiredDirection =
                        new fp2(fp.zero, fp.one),
                    DesiredSpeed = (fp)4,
                });
            unit.MovementHandler.TickUpdate();

            Assert.That(
                unit.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(
                    new fp2(fp.one, fp.zero)));
        }

        [Test]
        public void EqualPriorityForcedMove_ReplacesAtomically()
        {
            Unit unit = CreateUnit(101, fp2.zero);
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CreateForcedMove(
                        unit.UnitUid,
                        new fp2(fp.one, fp.zero),
                        2,
                        7));
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CreateForcedMove(
                        unit.UnitUid,
                        new fp2(fp.zero, fp.one),
                        2,
                        7));

            Assert.That(first.Added, Is.True);
            Assert.That(second.Added, Is.True);
            Assert.That(
                unit.CrowdControl
                    .ActiveForcedMoveHandle,
                Is.EqualTo(second.Handle));

            unit.MovementHandler.TickUpdate();
            Assert.That(
                unit.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(
                    new fp2(fp.zero, fp.one)));
        }

        [Test]
        public void ForcedMoveSnapshot_RoundTripsWithControlOwner()
        {
            Unit unit = CreateUnit(102, fp2.zero);
            unit.CrowdControl.Add(
                CreateForcedMove(
                    unit.UnitUid,
                    new fp2(fp.one, fp.zero),
                    3,
                    4));

            MovementSnapshot movement = default;
            CrowdControlHandlerSnapshot control =
                default;
            unit.MovementHandler.Capture(
                ref movement);
            unit.CrowdControl.Capture(
                ref control);

            unit.MovementHandler.ClearForDeath();
            unit.CrowdControl.ClearForDeath();
            unit.MovementHandler.Restore(movement);
            unit.CrowdControl.Restore(control);
            unit.MovementHandler.Resolve(default);

            Assert.That(
                unit.MovementHandler.HasForcedMove,
                Is.True);
            Assert.That(
                unit.CrowdControl
                    .ActiveForcedMoveHandle,
                Is.EqualTo(
                    movement.ForcedMove
                        .SourceControlHandle));
        }

        [Test]
        public void Dash_RequiresPositiveDuration_AndOverridesRoute()
        {
            Unit unit = CreateUnit(103, fp2.zero);
            Assert.Throws<
                DeterministicSimulationException>(
                () => unit.MovementHandler.StartDash(
                    new DashRequest(
                        9,
                        new fp2(fp.one, fp.zero),
                        (fp)4,
                        0)));

            Assert.That(
                unit.MovementHandler.StartDash(
                    new DashRequest(
                        9,
                        new fp2(fp.one, fp.zero),
                        (fp)4,
                        2)),
                Is.True);
            unit.MovementHandler.ApplyRouteMovement(
                new LocomotionResult
                {
                    UnitUid = unit.UnitUid,
                    HasMovement = true,
                    DesiredDirection =
                        new fp2(fp.zero, fp.one),
                    DesiredSpeed = (fp)5,
                });
            unit.MovementHandler.TickUpdate();

            Assert.That(
                unit.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(
                    new fp2((fp)2, fp.zero)));
        }

        [Test]
        public void MovementCollision_UsesPhysicsShapeRadius()
        {
            Unit unit = CreateUnit(
                104,
                fp2.zero,
                RadiusClassHelper.LargeRadius);
            var probe = new RadiusProbe();
            unit.MovementHandler.SetCollisionResolver(
                probe);
            unit.MovementHandler.ApplyMoveInput(
                new MoveIntent(
                    new fp2(fp.one, fp.zero)));
            unit.MovementHandler.TickUpdate();

            Assert.That(
                probe.Radius,
                Is.EqualTo(
                    RadiusClassHelper.LargeRadius));
            Assert.That(
                probe.RadiusClass,
                Is.EqualTo(RadiusClass.Large));
            Assert.That(
                probe.SelfUid,
                Is.EqualTo(unit.UnitUid));
        }

        [Test]
        public void RvoGrid_IncludesIdleUnitAsObstacle()
        {
            Unit mover = CreateUnit(
                105,
                fp2.zero);
            Unit idle = CreateUnit(
                106,
                new fp2(fp.one, fp.zero));
            var physics = new PhysicsWorld();
            physics.RegisterUnit(
                mover.PhysicsEntity);
            physics.RegisterUnit(
                idle.PhysicsEntity);
            physics.BuildRvoGrid();

            var units = new List<Unit>
            {
                mover,
                idle,
            };
            var locomotion =
                new List<LocomotionResult>
                {
                    new LocomotionResult
                    {
                        UnitUid = mover.UnitUid,
                        HasMovement = true,
                        AllowRVO = true,
                        DesiredDirection =
                            new fp2(fp.one, fp.zero),
                        DesiredSpeed = fp.one,
                    },
                    LocomotionResult.Idle(
                        idle.UnitUid),
                };
            var orchestrator =
                new RvoOrchestrator();
            orchestrator.Step(
                new DeterministicRVOSystem(
                    RVOConfig.Default),
                physics,
                units,
                locomotion);

            mover.MovementHandler
                .ApplyRouteMovement(
                    locomotion[0]);
            idle.MovementHandler
                .ApplyRouteMovement(
                    locomotion[1]);
            mover.MovementHandler.TickUpdate();
            idle.MovementHandler.TickUpdate();

            Assert.That(
                mover.PhysicsEntity
                    .Transform2D.Position,
                Is.Not.EqualTo(
                    new fp2(fp.one, fp.zero)));
            Assert.That(
                idle.PhysicsEntity
                    .Transform2D.Position,
                Is.EqualTo(
                    new fp2(fp.one, fp.zero)));
        }

        [Test]
        public void RvoNeighborSelection_IsInputOrderIndependent()
        {
            var system =
                new DeterministicRVOSystem(
                    new RVOConfig
                    {
                        NeighborSearchRadius = (fp)4,
                        MaxNeighbors = 1,
                        TimeHorizon = fp.one,
                        SampleCount = 16,
                    });
            RVOInput self = CreateRvoInput(
                new UnitUid(0, 10, 0),
                fp2.zero,
                new fp2(fp.one, fp.zero));
            RVOInput low = CreateRvoInput(
                new UnitUid(0, 11, 0),
                new fp2(fp.one, fp.zero),
                fp2.zero);
            RVOInput high = CreateRvoInput(
                new UnitUid(0, 12, 0),
                new fp2(-fp.one, fp.zero),
                fp2.zero);

            RvoResult first = FindResult(
                system.Step(
                    new[] { self, high, low }),
                self.SelfUid);
            RvoResult second = FindResult(
                system.Step(
                    new[] { low, self, high }),
                self.SelfUid);

            Assert.That(
                first.FinalVelocity,
                Is.EqualTo(second.FinalVelocity));
        }

        private static Unit CreateUnit(
            int prefabId,
            fp2 position,
            fp radius = default)
        {
            if (radius <= fp.zero)
            {
                radius =
                    RadiusClassHelper.MediumRadius;
            }
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(0, prefabId, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
            unit.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    radius));
            unit.PhysicsEntity.TeleportLogicPosition(
                position);
            unit.MovementHandler.SetMoveSpeed(
                fp.one);
            return unit;
        }

        private static CrowdControlConstraint
            CreateForcedMove(
                UnitUid source,
                fp2 deltaPerTick,
                int durationTicks,
                byte priority)
        {
            return new CrowdControlConstraint
            {
                Type =
                    CrowdControlType.Knockback,
                RemainingTicks = durationTicks,
                Priority = priority,
                SourceUnitUid = source,
                IsForcedMove = true,
                ForcedMoveConfigId = 1,
                ForcedMoveDeltaPerTick =
                    deltaPerTick,
                ForcedMoveWallPolicy =
                    ForceMoveWallPolicy.StopAtWall,
            };
        }

        private static RVOInput CreateRvoInput(
            UnitUid uid,
            fp2 position,
            fp2 desiredVelocity)
        {
            return new RVOInput
            {
                SelfUid = uid,
                Position = position,
                DesiredVelocity =
                    desiredVelocity,
                Radius =
                    RadiusClassHelper.MediumRadius,
                MaxSpeed = fp.one,
            };
        }

        private static RvoResult FindResult(
            RvoResult[] results,
            UnitUid uid)
        {
            for (int i = 0;
                 i < results.Length;
                 i++)
            {
                if (results[i].UnitUid == uid)
                {
                    return results[i];
                }
            }
            Assert.Fail(
                $"Missing RVO result for {uid}.");
            return default;
        }
    }
}
