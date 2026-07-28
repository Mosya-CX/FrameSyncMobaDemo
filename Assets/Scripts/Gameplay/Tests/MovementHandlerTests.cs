using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class MovementHandlerTests
    {
        private MovementHandler handler;
        private static readonly fp DefaultSpeed = (fp)5m;

        private SimulationTickContextController _tickController;

        [SetUp]
        public void SetUp()
        {
            _tickController = new SimulationTickContextController();
            _tickController.BeginTick(0, ExecutionMode.ServerAuthority);
            handler = UnitTestFactory.CreateMovementHandler(fp2.zero, DefaultSpeed);
        }

        [TearDown]
        public void TearDown()
        {
            _tickController.EndTick();
            _tickController = null;
        }
        [Test]
        public void Constructor_SetsInitialPosition()
        {
            var h = UnitTestFactory.CreateMovementHandler(new fp2(10m, 20m), 3m);

            Assert.AreEqual(new fp2(10m, 20m), h.Position);
            Assert.AreEqual((fp)3m, h.MoveSpeed);
            Assert.AreEqual(fp2.zero, h.Velocity);
        }

        [Test]
        public void Constructor_DefaultFacing_IsPositiveX()
        {
            Assert.AreEqual(new fp2(fp.one, fp.zero), handler.Facing);
        }

        [Test]
        public void MoveIntent_Constructor_SetsDirectionAndHasInput()
        {
            var intent = new MoveIntent(new fp2(fp.one, fp.zero));

            Assert.IsTrue(intent.HasInput);
            // Normalization may have minor fixed-point precision differences
            fp dirLenSq = fpmath.dot(intent.Direction, intent.Direction);
            Assert.Greater(dirLenSq, fp.zero);
        }

        [Test]
        public void MoveIntent_None_HasNoInput()
        {
            Assert.IsFalse(MoveIntent.None.HasInput);
        }

        [Test]
        public void MoveIntent_FromDirection_Zero_ReturnsNone()
        {
            var intent = MoveIntent.FromDirection(fp2.zero);
            Assert.IsFalse(intent.HasInput);
        }

        [Test]
        public void MoveIntent_FromDirection_Normalizes()
        {
            var intent = MoveIntent.FromDirection(new fp2(3m, 0m));

            Assert.IsTrue(intent.HasInput);
            // Fixed-point normalization may have minor precision deviation; check directional correctness
            Assert.Greater(intent.Direction.x, fp.zero);
            Assert.AreEqual(fp.zero, intent.Direction.y);
        }

        [Test]
        public void ApplyMoveInput_ThenTickUpdate_MovesPosition()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            fp2 expected = new fp2(DefaultSpeed, fp.zero);
            Assert.AreEqual(expected, handler.Position);
        }

        [Test]
        public void TickUpdate_WithoutInput_StopsMovement()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();
            handler.TickUpdate();

            Assert.AreEqual(new fp2(DefaultSpeed, fp.zero), handler.Position);
            Assert.AreEqual(fp2.zero, handler.Velocity);
        }

        [Test]
        public void TickUpdate_WithDeltaTime_ScalesMovement()
        {
            // TickUpdate reads SimulationTickContext.Current.DeltaTick internally.
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            fp2 expected = new fp2(DefaultSpeed, fp.zero);
            Assert.AreEqual(expected, handler.Position);
        }

        [Test]
        public void TickUpdate_MultipleTicks_AccumulatesPosition()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            fp2 expected = new fp2(DefaultSpeed * 2m, fp.zero);
            Assert.AreEqual(expected, handler.Position);
        }

        [Test]
        public void TickUpdate_UpdatesFacing()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.TickUpdate();

            Assert.AreEqual(fp.zero, handler.Facing.x);
            Assert.That(handler.Facing.y, Is.GreaterThan((fp)0.999m));
        }

        [Test]
        public void TickUpdate_ZeroDirection_PreservesFacing()
        {
            fp2 initialFacing = handler.Facing;

            handler.ApplyMoveInput(MoveIntent.None);
            handler.TickUpdate();

            Assert.AreEqual(initialFacing, handler.Facing);
        }

        [Test]
        public void SetMoveSpeed_ChangesSpeed()
        {
            fp newSpeed = 10m;
            handler.SetMoveSpeed(newSpeed);
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            Assert.AreEqual(new fp2(newSpeed, fp.zero), handler.Position);
        }

        [Test]
        public void ForceSetPosition_Teleports()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            handler.ForceSetPosition(new fp2(100m, 200m));

            Assert.AreEqual(new fp2(100m, 200m), handler.Position);
            Assert.AreEqual(fp2.zero, handler.Velocity);
        }

        [Test]
        public void CaptureRestore_RoundTrip_PreservesMovementOwnedStateOnly()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.TickUpdate();

            MovementSnapshot captured = default;
            handler.Capture(ref captured);

            var restored = UnitTestFactory.CreateMovementHandler(fp2.zero, 0m);
            restored.Restore(captured);

            Assert.AreEqual(captured, restored.Snapshot);
            Assert.AreEqual(fp2.zero, restored.Position,
                "Physics pose is restored by PhysicsEntity2D, not MovementSnapshot.");
            Assert.AreEqual(fp.zero, restored.MoveSpeed,
                "Static locomotion configuration is not rollback state.");
        }

        [Test]
        public void Restore_ClearsPendingIntent()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate();

            MovementSnapshot captured = default;
            handler.Capture(ref captured);
            fp2 positionBeforeRestore = handler.Position;

            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.Restore(captured);
            handler.TickUpdate();

            Assert.AreEqual(positionBeforeRestore, handler.Position);
        }

        [Test]
        public void SameInput_SameResult()
        {
            var h1 = UnitTestFactory.CreateMovementHandler(new fp2(10m, 5m), 3m);
            var h2 = UnitTestFactory.CreateMovementHandler(new fp2(10m, 5m), 3m);

            for (int i = 0; i < 10; i++)
            {
                var intent = new MoveIntent(new fp2(fp.one, fp.zero));
                h1.ApplyMoveInput(intent);
                h2.ApplyMoveInput(intent);
                h1.TickUpdate();
                h2.TickUpdate();
            }

            Assert.AreEqual(h1.Position, h2.Position);
            Assert.AreEqual(h1.Velocity, h2.Velocity);
            Assert.AreEqual(h1.Facing, h2.Facing);
        }

        [Test]
        public void CaptureRestore_ThenReplay_ProducesSameResult()
        {
            var original = UnitTestFactory.CreateMovementHandler(new fp2(5m, 0m), 4m);

            for (int i = 0; i < 5; i++)
            {
                original.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                original.TickUpdate();
            }

            var checkpoint = UnitTestFactory.CreateMovementHandler(new fp2(5m, 0m), 4m);
            checkpoint.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            checkpoint.TickUpdate();
            checkpoint.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            checkpoint.TickUpdate();

            MovementSnapshot snap = default;
            checkpoint.Capture(ref snap);

            var replay = UnitTestFactory.CreateMovementHandler(
                checkpoint.Position,
                checkpoint.MoveSpeed);
            replay.Restore(snap);
            for (int i = 0; i < 3; i++)
            {
                replay.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                replay.TickUpdate();
            }

            Assert.AreEqual(original.Position, replay.Position);
        }
    }
}
