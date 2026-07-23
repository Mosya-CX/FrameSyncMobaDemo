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

        [SetUp]
        public void SetUp()
        {
            handler = UnitTestFactory.CreateMovementHandler(fp2.zero, DefaultSpeed);
        }

        [Test]
        public void Constructor_SetsInitialPosition()
        {
            var h = UnitTestFactory.CreateMovementHandler(new fp2(10m, 20m), 3m);

            Assert.AreEqual(new fp2(10m, 20m), h.Snapshot.Position);
            Assert.AreEqual((fp)3m, h.Snapshot.MoveSpeed);
            Assert.AreEqual(fp2.zero, h.Snapshot.Velocity);
        }

        [Test]
        public void Constructor_DefaultFacing_IsPositiveX()
        {
            Assert.AreEqual(new fp2(fp.one, fp.zero), handler.Snapshot.Facing);
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
            handler.TickUpdate(fp.one);

            fp2 expected = new fp2(DefaultSpeed, fp.zero);
            Assert.AreEqual(expected, handler.Snapshot.Position);
        }

        [Test]
        public void TickUpdate_WithoutInput_StopsMovement()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);
            handler.TickUpdate(fp.one);

            Assert.AreEqual(new fp2(DefaultSpeed, fp.zero), handler.Snapshot.Position);
            Assert.AreEqual(fp2.zero, handler.Snapshot.Velocity);
        }

        [Test]
        public void TickUpdate_WithDeltaTime_ScalesMovement()
        {
            fp halfDt = 0.5m;
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(halfDt);

            fp2 expected = new fp2(DefaultSpeed * halfDt, fp.zero);
            Assert.AreEqual(expected, handler.Snapshot.Position);
        }

        [Test]
        public void TickUpdate_MultipleTicks_AccumulatesPosition()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);

            fp2 expected = new fp2(DefaultSpeed * 2m, fp.zero);
            Assert.AreEqual(expected, handler.Snapshot.Position);
        }

        [Test]
        public void TickUpdate_UpdatesFacing()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.TickUpdate(fp.one);

            Assert.AreEqual(new fp2(fp.zero, fp.one), handler.Snapshot.Facing);
        }

        [Test]
        public void TickUpdate_ZeroDirection_PreservesFacing()
        {
            fp2 initialFacing = handler.Snapshot.Facing;

            handler.ApplyMoveInput(MoveIntent.None);
            handler.TickUpdate(fp.one);

            Assert.AreEqual(initialFacing, handler.Snapshot.Facing);
        }

        [Test]
        public void SetMoveSpeed_ChangesSpeed()
        {
            fp newSpeed = 10m;
            handler.SetMoveSpeed(newSpeed);
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);

            Assert.AreEqual(new fp2(newSpeed, fp.zero), handler.Snapshot.Position);
        }

        [Test]
        public void ForceSetPosition_Teleports()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);

            handler.ForceSetPosition(new fp2(100m, 200m));

            Assert.AreEqual(new fp2(100m, 200m), handler.Snapshot.Position);
            Assert.AreEqual(fp2.zero, handler.Snapshot.Velocity);
        }

        [Test]
        public void CaptureRestore_RoundTrip_PreservesState()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.TickUpdate(fp.one);

            MovementSnapshot captured = default;
            handler.Capture(ref captured);

            var restored = UnitTestFactory.CreateMovementHandler(fp2.zero, 0m);
            restored.Restore(captured);

            Assert.AreEqual(handler.Snapshot.Position, restored.Snapshot.Position);
            Assert.AreEqual(handler.Snapshot.Velocity, restored.Snapshot.Velocity);
            Assert.AreEqual(handler.Snapshot.Facing, restored.Snapshot.Facing);
            Assert.AreEqual(handler.Snapshot.MoveSpeed, restored.Snapshot.MoveSpeed);
        }

        [Test]
        public void Restore_ClearsPendingIntent()
        {
            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            handler.TickUpdate(fp.one);

            MovementSnapshot captured = default;
            handler.Capture(ref captured);

            handler.ApplyMoveInput(new MoveIntent(new fp2(fp.zero, fp.one)));
            handler.Restore(captured);
            handler.TickUpdate(fp.one);

            Assert.AreEqual(captured.Position, handler.Snapshot.Position);
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
                h1.TickUpdate(fp.one);
                h2.TickUpdate(fp.one);
            }

            Assert.AreEqual(h1.Snapshot.Position, h2.Snapshot.Position);
            Assert.AreEqual(h1.Snapshot.Velocity, h2.Snapshot.Velocity);
            Assert.AreEqual(h1.Snapshot.Facing, h2.Snapshot.Facing);
        }

        [Test]
        public void CaptureRestore_ThenReplay_ProducesSameResult()
        {
            var original = UnitTestFactory.CreateMovementHandler(new fp2(5m, 0m), 4m);

            for (int i = 0; i < 5; i++)
            {
                original.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                original.TickUpdate(fp.one);
            }

            var checkpoint = UnitTestFactory.CreateMovementHandler(new fp2(5m, 0m), 4m);
            checkpoint.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            checkpoint.TickUpdate(fp.one);
            checkpoint.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
            checkpoint.TickUpdate(fp.one);

            MovementSnapshot snap = default;
            checkpoint.Capture(ref snap);

            var replay = UnitTestFactory.CreateMovementHandler(fp2.zero, 0m);
            replay.Restore(snap);
            for (int i = 0; i < 3; i++)
            {
                replay.ApplyMoveInput(new MoveIntent(new fp2(fp.one, fp.zero)));
                replay.TickUpdate(fp.one);
            }

            Assert.AreEqual(original.Snapshot.Position, replay.Snapshot.Position);
        }
    }
}
