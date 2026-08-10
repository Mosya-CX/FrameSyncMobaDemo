using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public class FrameSyncPipelineTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private StatDefinitionTable _statTable;

        [SetUp]
        public void SetUp()
        {
            _world = new UnitWorld();
            _statTable = new StatDefinitionTable();
            _statTable.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
            });
            _world.StatDefinitionTable = _statTable;

            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "Hero",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = new StatPreset(),
            };
        }

        [Test]
        public void GameplayCommand_CreateMove_WritesCanonicalBytes()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);

            try
            {
                var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
                var cmd = GameplayCommand.CreateMove(
                    CreateHeader(unit.UnitUid, 11, 1),
                    new fp2(fp.one, fp.zero));

                var buffer = new byte[256];
                var writer = new CanonicalByteWriter(buffer);
                cmd.WriteCanonicalBytes(writer);

                Assert.Greater(writer.WrittenCount, 0);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void CommandCollector_MergeMove_LastWins()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);

            try
            {
                var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
                var collector = new CommandCollector();

                var point1 = new fp2(fp.one, fp.zero);
                var point2 = new fp2(fp.zero, fp.one);

                collector.Collect(GameplayCommand.CreateMove(
                    CreateHeader(unit.UnitUid, 1, 1), point1));
                collector.Collect(GameplayCommand.CreateMove(
                    CreateHeader(unit.UnitUid, 1, 2), point2));

                var commands = collector.GetCanonicalCommands();
                Assert.AreEqual(1, commands.Count);
                Assert.AreEqual(point2, commands[0].MoveTargetPoint);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void Pipeline_SingleUnit_MovesWithCommand()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(0, ExecutionMode.ServerAuthority);

            try
            {
                var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 0, 0m, 0m);
                unit.MovementHandler.SetMoveSpeed(3m);
            }
            finally
            {
                controller.EndTick();
            }

            var pipeline = new SimulationTickPipeline(_world);
            var runtime = new FrameSyncGameRuntime(
                _world, null, 0, 0, 180, 300, 60, (fp)7 / (fp)10, 42u);

            // Submit a move command targeting the first tick after spawn.
            // D-008 spawn-Tick gate: a unit cannot act during its own spawn
            // Tick (SpawnLogicTick), so tick 0 is a setup Tick only.
            var unit2 = _world.GetAllUnits()[0];
            runtime.SubmitCommand(GameplayCommand.CreateMove(
                CreateHeader(unit2.UnitUid, 1, 1),
                new fp2(10, 0)));

            // Execute the setup Tick and the command Tick.
            runtime.ExecuteTicks(2);

            Assert.AreEqual(2, runtime.CurrentTick);
            Assert.AreNotEqual(fp2.zero, unit2.MovementHandler.Position);
        }

        [Test]
        public void Checksum_SameState_SameHash()
        {
            var w1 = CreateWorldWithUnit();
            var w2 = CreateWorldWithUnit();

            var writer = new CanonicalByteWriter(new byte[4096]);
            var checksum1 = SharedGameplayChecksum.Compute(w1.GetAllUnits(), writer);
            var checksum2 = SharedGameplayChecksum.Compute(w2.GetAllUnits(), writer);

            Assert.AreEqual(checksum1, checksum2);
        }

        [Test]
        public void Checksum_DifferentState_DifferentHash()
        {
            var w1 = CreateWorldWithUnit();
            var w2 = CreateWorldWithUnit();

            var u1 = w1.GetAllUnits()[0];
            var u2 = w2.GetAllUnits()[0];
            u2.MovementHandler.ForceSetPosition(new fp2(100m, 200m));

            var writer = new CanonicalByteWriter(new byte[4096]);
            var checksum1 = SharedGameplayChecksum.Compute(w1.GetAllUnits(), writer);
            var checksum2 = SharedGameplayChecksum.Compute(w2.GetAllUnits(), writer);

            Assert.AreNotEqual(checksum1, checksum2);
        }

        [Test]
        public void Pipeline_Deterministic_SameCommandsSameResult()
        {
            var runtime1 = CreateRuntime();
            var runtime2 = CreateRuntime();

            var u1 = runtime1.UnitWorld.GetAllUnits()[0];
            var u2 = runtime2.UnitWorld.GetAllUnits()[0];

            for (int i = 0; i < 10; i++)
            {
                fp2 targetPoint = new fp2(i + 1, 0);
                runtime1.SubmitCommand(GameplayCommand.CreateMove(
                    CreateHeader(u1.UnitUid, i, (uint)(i + 1)), targetPoint));
                runtime2.SubmitCommand(GameplayCommand.CreateMove(
                    CreateHeader(u2.UnitUid, i, (uint)(i + 1)), targetPoint));
                runtime1.ExecuteOneTick();
                runtime2.ExecuteOneTick();
            }

            Assert.AreEqual(
                u1.MovementHandler.Position,
                u2.MovementHandler.Position);
            Assert.AreEqual(runtime1.LastChecksum, runtime2.LastChecksum);
        }

        [Test]
        public void ExecuteOneTick_LocalAuthority_ReleasesConfirmedHistoryBeyondSnapshotCapacity()
        {
            var world = CreateWorldWithUnit();
            var runtime = new FrameSyncGameRuntime(
                world,
                null,
                0,
                0,
                180,
                300,
                60,
                (fp)7 / (fp)10,
                42u,
                snapshotWindowTicks: 4);

            runtime.ExecuteTicks(20);

            Assert.That(runtime.CurrentTick, Is.EqualTo(20));
            Assert.That(
                runtime.LatestSynchronizedServerTick,
                Is.EqualTo(19));
            Assert.That(
                runtime.Prediction.LocalFrameVerificationRecordByTick,
                Is.Empty);
        }

        private UnitWorld CreateWorldWithUnit()
        {
            var world = new UnitWorld
            {
                StatDefinitionTable = new StatDefinitionTable()
            };
            world.StatDefinitionTable.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
            });

            var proto = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "Hero",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = new StatPreset(),
            };

            var ctrl = new SimulationTickContextController();
            ctrl.BeginTick(0, ExecutionMode.ServerAuthority);
            try { world.SpawnUnit(proto, TeamId.Neutral, 0, 0m, 0m); }
            finally { ctrl.EndTick(); }

            return world;
        }

        private FrameSyncGameRuntime CreateRuntime()
        {
            var world = CreateWorldWithUnit();
            world.GetAllUnits()[0].MovementHandler.SetMoveSpeed(4m);
            return new FrameSyncGameRuntime(
                world, null, 0, 0, 180, 300, 60, (fp)7 / (fp)10, 42u);
        }

        private static CommandHeader CreateHeader(
            UnitUid unitUid,
            int targetTick,
            uint commandSeq)
        {
            return new CommandHeader(
                commandSeq,
                1,
                0,
                unitUid,
                targetTick,
                GameplayCommandKind.None,
                targetTick - 1,
                0);
        }
    }
}
