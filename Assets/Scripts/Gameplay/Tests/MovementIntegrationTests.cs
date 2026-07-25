using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class MovementIntegrationTests
    {
        private UnitWorld world;
        private StatDefinitionTable definitionTable;
        private UnitPrototype prototype;
        private SimulationTickContextController tickController;

        [SetUp]
        public void SetUp()
        {
            tickController = new SimulationTickContextController();
            tickController.BeginTick(1, ExecutionMode.ServerAuthority);

            world = new UnitWorld();
            definitionTable = StatTestHelpers.CreateDefaultTable();
            world.StatDefinitionTable = definitionTable;

            prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestHero",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = StatTestHelpers.CreateSimplePreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
        }

        [TearDown]
        public void TearDown()
        {
            tickController.EndTick();
        }

        [Test]
        public void SpawnUnit_CreatesMovementHandlerWithDefaults()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.MovementHandler);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Snapshot.Position);
            Assert.AreEqual(fp.one, unit.MovementHandler.Snapshot.MoveSpeed);
        }

        [Test]
        public void MovementHandler_MoveAndCheckSnapshot()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            var moveHandler = unit.MovementHandler;

            moveHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            moveHandler.TickUpdate();

            Assert.AreNotEqual(fp2.zero, moveHandler.Snapshot.Position);
            Assert.AreEqual(new fp2(fp.one, fp.zero), moveHandler.Snapshot.Facing);
        }

        [Test]
        public void TwoUnits_SameInput_SamePosition()
        {
            Unit u1 = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit u2 = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            u1.MovementHandler.SetMoveSpeed(3m);
            u2.MovementHandler.SetMoveSpeed(3m);

            for (int i = 0; i < 10; i++)
            {
                var intent = new MoveIntent(new fp2(fp.one, fp.zero));
                u1.MovementHandler.ApplyMoveInput(intent);
                u2.MovementHandler.ApplyMoveInput(intent);
                u1.MovementHandler.TickUpdate();
                u2.MovementHandler.TickUpdate();
            }

            Assert.AreEqual(
                u1.MovementHandler.Snapshot.Position,
                u2.MovementHandler.Snapshot.Position);
        }

        [Test]
        public void TwoUnits_DifferentSpeeds_DifferentPositions()
        {
            Unit fast = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit slow = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            fast.MovementHandler.SetMoveSpeed(5m);
            slow.MovementHandler.SetMoveSpeed(2m);

            for (int i = 0; i < 10; i++)
            {
                var intent = new MoveIntent(new fp2(fp.one, fp.zero));
                fast.MovementHandler.ApplyMoveInput(intent);
                slow.MovementHandler.ApplyMoveInput(intent);
                fast.MovementHandler.TickUpdate();
                slow.MovementHandler.TickUpdate();
            }

            Assert.Greater(
                fast.MovementHandler.Snapshot.Position.x,
                slow.MovementHandler.Snapshot.Position.x);
        }

        [Test]
        public void TwoUnits_DifferentDirections_Diverge()
        {
            Unit right = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit up = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            right.MovementHandler.SetMoveSpeed(3m);
            up.MovementHandler.SetMoveSpeed(3m);

            for (int i = 0; i < 5; i++)
            {
                right.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                up.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
                right.MovementHandler.TickUpdate();
                up.MovementHandler.TickUpdate();
            }

            Assert.AreEqual(new fp2(fp.one, fp.zero), right.MovementHandler.Snapshot.Facing);
            Assert.AreEqual(new fp2(fp.zero, fp.one), up.MovementHandler.Snapshot.Facing);
            Assert.AreNotEqual(
                right.MovementHandler.Snapshot.Position,
                up.MovementHandler.Snapshot.Position);
        }

        [Test]
        public void MoveCommand_StoresUnitIntentAndTick()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 42, 0m, 0m);
            var intent = new MoveIntent(new fp2(fp.one, fp.zero));
            var command = new MoveCommand(unit.UnitUid, intent, 42);

            Assert.AreEqual(unit.UnitUid, command.UnitUid);
            Assert.IsTrue(command.Intent.HasInput);
            Assert.AreEqual(42, command.Tick);
        }

        [Test]
        public void MoveCommand_None_IsDefault()
        {
            var cmd = MoveCommand.None;

            Assert.AreEqual(default(UnitUid), cmd.UnitUid);
            Assert.IsFalse(cmd.Intent.HasInput);
            Assert.AreEqual(0, cmd.Tick);
        }

        [Test]
        public void ClearForDeath_StopsMovement()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            unit.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            unit.MovementHandler.TickUpdate();

            fp2 positionBeforeDeath = unit.MovementHandler.Snapshot.Position;

            unit.ClearForDeath();

            Assert.AreEqual(positionBeforeDeath, unit.MovementHandler.Snapshot.Position);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Snapshot.Velocity);
        }

        [Test]
        public void ResetForPool_PreservesMovementComponentAndClearsRuntimeState()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Assert.IsNotNull(unit.MovementHandler);

            unit.ResetForPool();

            Assert.IsNotNull(unit.MovementHandler);
            Assert.AreEqual(MovementSnapshot.Default, unit.MovementHandler.Snapshot);
        }

        [Test]
        public void MovementHandler_ImplementsIRollback()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            var handler = unit.MovementHandler;

            Assert.IsInstanceOf<IRollback<MovementSnapshot>>(handler);
        }

        [Test]
        public void MovementSnapshot_Default_HasExpectedValues()
        {
            var snap = MovementSnapshot.Default;

            Assert.AreEqual(fp2.zero, snap.Position);
            Assert.AreEqual(fp2.zero, snap.Velocity);
            Assert.AreEqual(new fp2(fp.one, fp.zero), snap.Facing);
            Assert.AreEqual(fp.one, snap.MoveSpeed);
        }

        [Test]
        public void MovementHandler_ImplementsIMovementAgent()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsInstanceOf<IMovementAgent>(unit.MovementHandler);
        }

        [Test]
        public void IdleUnit_DoesNotMove()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            for (int i = 0; i < 5; i++)
            {
                unit.MovementHandler.TickUpdate();
            }

            Assert.AreEqual(fp2.zero, unit.MovementHandler.Snapshot.Position);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Snapshot.Velocity);
        }

        [Test]
        public void TwoWorlds_SameSpawnAndMove_SameResult()
        {
            var world2 = new UnitWorld
            {
                StatDefinitionTable = StatTestHelpers.CreateDefaultTable()
            };

            var proto2 = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestHero",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = StatTestHelpers.CreateSimplePreset(),
            };

            Unit u1 = world.SpawnUnit(prototype, TeamId.Neutral, 10, 0m, 0m);
            Unit u2 = world2.SpawnUnit(proto2, TeamId.Neutral, 10, 0m, 0m);

            u1.MovementHandler.SetMoveSpeed(4m);
            u2.MovementHandler.SetMoveSpeed(4m);

            for (int i = 0; i < 10; i++)
            {
                var intent = new MoveIntent(new fp2(fp.one, fp.zero));
                u1.MovementHandler.ApplyMoveInput(intent);
                u2.MovementHandler.ApplyMoveInput(intent);
                u1.MovementHandler.TickUpdate();
                u2.MovementHandler.TickUpdate();
            }

            Assert.AreEqual(
                u1.MovementHandler.Snapshot.Position,
                u2.MovementHandler.Snapshot.Position);
            Assert.AreEqual(
                u1.MovementHandler.Snapshot.Velocity,
                u2.MovementHandler.Snapshot.Velocity);
            Assert.AreEqual(
                u1.MovementHandler.Snapshot.Facing,
                u2.MovementHandler.Snapshot.Facing);
        }
    }
}
