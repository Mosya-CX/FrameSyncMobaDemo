using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class MatchTopologyTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void InitialTopology_RegistersTwoStructureBasesInStableRoles()
        {
            var world = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
            ConfigureBase(world, 101);
            ConfigureBase(world, 102);
            var rule = new MatchRuleRuntime(3);
            rule.BeginCountdown(0, 0);
            var pipeline = new SimulationTickPipeline(
                world,
                world.PhysicsWorld)
            {
                MatchRule = rule,
            };
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    101,
                    new TeamId(1),
                    new fp2(-10, 0),
                    new fp2(1, 0)),
                MatchTopologyRole.BlueBase);
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    102,
                    new TeamId(2),
                    new fp2(10, 0),
                    new fp2(-1, 0)),
                MatchTopologyRole.RedBase);
            var controller =
                new SimulationTickContextController();

            pipeline.ExecuteTick(
                controller,
                ExecutionMode.ClientPrediction);

            Assert.That(
                rule.BlueBaseUnitUid.IsValid(),
                Is.True);
            Assert.That(
                rule.RedBaseUnitUid.IsValid(),
                Is.True);
            Assert.That(
                rule.BlueBaseUnitUid,
                Is.Not.EqualTo(
                    rule.RedBaseUnitUid));
            Assert.That(world.TryGetUnit(
                rule.BlueBaseUnitUid,
                out UnitType blue), Is.True);
            Assert.That(world.TryGetUnit(
                rule.RedBaseUnitUid,
                out UnitType red), Is.True);
            Assert.That(blue.UnitKind,
                Is.EqualTo(UnitKind.Structure));
            Assert.That(red.UnitKind,
                Is.EqualTo(UnitKind.Structure));
            Assert.That(rule.CurrentPhase,
                Is.EqualTo(MatchPhase.Running));
        }

        [Test]
        public void InitialAuthoritySnapshot_DiscardsClientAuthoringSpawnQueue()
        {
            const int prototypeId = 151;
            UnitSpawnRequest request = new UnitSpawnRequest(
                prototypeId,
                new TeamId(1),
                fp2.zero,
                new fp2(fp.one, fp.zero));
            var serverWorld = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
            ConfigureBase(serverWorld, prototypeId);
            var serverRuntime = CreateRuntime(serverWorld);
            serverRuntime.QueueInitialSpawn(request);
            serverRuntime.MaterializeInitialSpawnsForBootstrap(3);
            GameplaySnapshot authoritySnapshot =
                serverRuntime.TickPipeline.CaptureAggregateSnapshot();

            var clientWorld = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
            ConfigureBase(clientWorld, prototypeId);
            var clientRuntime = CreateRuntime(clientWorld);
            clientRuntime.QueueInitialSpawn(request);

            clientRuntime.RestoreInitialSnapshot(
                authoritySnapshot,
                3,
                ExecutionMode.ClientPrediction);

            Assert.That(
                clientRuntime.Prediction.ExecutePredictionTick(),
                Is.True);
            Assert.That(clientWorld.GetAllUnits().Count, Is.EqualTo(1));
            Assert.That(clientRuntime.CurrentTick, Is.EqualTo(4));
        }

        [Test]
        public void InitialTopology_RejectsBasesOnSameTeam()
        {
            var world = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
            ConfigureBase(world, 201);
            ConfigureBase(world, 202);
            var pipeline = new SimulationTickPipeline(
                world,
                world.PhysicsWorld)
            {
                MatchRule = new MatchRuleRuntime(3),
            };
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    201,
                    new TeamId(1),
                    new fp2(-10, 0),
                    new fp2(1, 0)),
                MatchTopologyRole.BlueBase);
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    202,
                    new TeamId(1),
                    new fp2(10, 0),
                    new fp2(-1, 0)),
                MatchTopologyRole.RedBase);

            Assert.Throws<
                DeterministicSimulationException>(
                () => pipeline.ExecuteTick(
                    new SimulationTickContextController(),
                    ExecutionMode.ServerAuthority));
        }

        private static void ConfigureBase(
            UnitWorld world,
            int prototypeId)
        {
            UnitTestFactory.ConfigureWorldForPrototype(
                world,
                new UnitPrototype
                {
                    UnitPrototypeId = prototypeId,
                    RuntimeEntityPrefabId = prototypeId,
                    UnitKind = UnitKind.Structure,
                    BaseStats = new StatPreset(),
                    PhysicsProfile =
                        PhysicsProfile2D.DefaultTower,
                    LocomotionProfile =
                        LocomotionProfile.DefaultTower,
                },
                fp.zero,
                fp.zero);
        }

        private static FrameSyncGameRuntime CreateRuntime(
            UnitWorld world)
        {
            return new FrameSyncGameRuntime(
                world,
                world.PhysicsWorld,
                2,
                0,
                3,
                1,
                1,
                fp.one,
                1,
                snapshotWindowTicks: 8,
                maxPredictionLeadTicks: 2);
        }
    }
}
