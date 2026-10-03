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

            world = new UnitWorld();
            definitionTable = StatTestHelpers.CreateDefaultTable();
            world.StatDefinitionTable = definitionTable;

            prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
            Name = "Varus",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = StatTestHelpers.CreateSimplePreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
                LocomotionProfile = new LocomotionProfile
                {
                    BaseMoveSpeed = fp.one,
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (tickController.IsTickActive)
                tickController.EndTick();
        }

        [Test]
        public void SpawnUnit_CreatesMovementHandlerWithDefaults()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.MovementHandler);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Position);
            Assert.AreEqual(fp.one, unit.MovementHandler.MoveSpeed);
            Assert.AreEqual(
                (fp)0.01m,
                unit.MovementHandler.LogicMoveSpeed);
        }

        [Test]
        public void MoveSpeed_UsesWorldLogicVelocityScaleExactlyOnce()
        {
            MovementHandler movement =
                UnitTestFactory.CreateMovementHandler(
                    fp2.zero,
                    (fp)350m);
            movement.SetMoveSpeedToLogicVelocityScale(
                (fp)0.01m);
            movement.SetLogicSecondsPerTick(
                fp.one / (fp)30);

            movement.ApplyMoveInput(
                new MoveIntent(new fp2(fp.one, fp.zero)));
            movement.TickUpdate();

            Assert.AreEqual(
                (fp)350m * (fp)0.01m,
                movement.Velocity.x);
            Assert.AreEqual(fp.zero, movement.Velocity.y);
            Assert.AreEqual(
                movement.Velocity.x *
                (fp.one / (fp)30),
                movement.Position.x);
        }

        [Test]
        public void MovementHandler_MoveAndCheckSnapshot()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            var moveHandler = unit.MovementHandler;
            BeginActiveGameplayTick();

            moveHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            moveHandler.TickUpdate();

            Assert.AreNotEqual(fp2.zero, moveHandler.Position);
            Assert.That(
                (double)moveHandler.Facing.x,
                Is.EqualTo(1d).Within(0.000001d));
            Assert.AreEqual(fp.zero, moveHandler.Facing.y);
        }

        [Test]
        public void TwoUnits_SameInput_SamePosition()
        {
            Unit u1 = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit u2 = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            BeginActiveGameplayTick();

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
                u1.MovementHandler.Position,
                u2.MovementHandler.Position);
        }

        [Test]
        public void TwoUnits_DifferentSpeeds_DifferentPositions()
        {
            Unit fast = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit slow = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            BeginActiveGameplayTick();

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
                fast.MovementHandler.Position.x,
                slow.MovementHandler.Position.x);
        }

        [Test]
        public void TwoUnits_DifferentDirections_Diverge()
        {
            Unit right = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit up = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            BeginActiveGameplayTick();

            right.MovementHandler.SetMoveSpeed(3m);
            up.MovementHandler.SetMoveSpeed(3m);

            for (int i = 0; i < 5; i++)
            {
                right.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                up.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
                right.MovementHandler.TickUpdate();
                up.MovementHandler.TickUpdate();
            }

            Assert.That(
                (double)right.MovementHandler.Facing.x,
                Is.EqualTo(1d).Within(0.000001d));
            Assert.AreEqual(fp.zero, right.MovementHandler.Facing.y);
            Assert.AreEqual(fp.zero, up.MovementHandler.Facing.x);
            Assert.That(
                (double)up.MovementHandler.Facing.y,
                Is.EqualTo(1d).Within(0.000001d));
            Assert.AreNotEqual(
                right.MovementHandler.Position,
                up.MovementHandler.Position);
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
            BeginActiveGameplayTick();
            unit.MovementHandler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            unit.MovementHandler.TickUpdate();

            fp2 positionBeforeDeath = unit.MovementHandler.Position;

            unit.ClearForDeath();

            Assert.AreEqual(positionBeforeDeath, unit.MovementHandler.Position);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Velocity);
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

            Assert.IsFalse(snap.Dash.IsActive);
            Assert.IsFalse(snap.ForcedMove.IsActive);
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
            BeginActiveGameplayTick();

            for (int i = 0; i < 5; i++)
            {
                unit.MovementHandler.TickUpdate();
            }

            Assert.AreEqual(fp2.zero, unit.MovementHandler.Position);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Velocity);
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
            Name = "Varus",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = StatTestHelpers.CreateSimplePreset(),
            };

            Unit u1 = world.SpawnUnit(prototype, TeamId.Neutral, 10, 0m, 0m);
            Unit u2 = world2.SpawnUnit(proto2, TeamId.Neutral, 10, 0m, 0m);
            BeginActiveGameplayTick(11);

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
                u1.MovementHandler.Position,
                u2.MovementHandler.Position);
            Assert.AreEqual(
                u1.MovementHandler.Velocity,
                u2.MovementHandler.Velocity);
            Assert.AreEqual(
                u1.MovementHandler.Facing,
                u2.MovementHandler.Facing);
        }

        private void BeginActiveGameplayTick(int tick = 2)
        {
            // D-008: units participate passively on their spawn Tick and may
            // run voluntary movement only on a later Tick.
            tickController.BeginTick(
                tick,
                ExecutionMode.ServerAuthority);
        }
    }
}
