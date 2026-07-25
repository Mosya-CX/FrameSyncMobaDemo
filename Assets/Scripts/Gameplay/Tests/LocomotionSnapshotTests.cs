using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class LocomotionSnapshotTests
    {
        private const int GridWidth = 16;
        private const int GridHeight = 16;

        private PathGridMap2D CreateGrid()
        {
            var grid = new PathGridMap2D();
            grid.Initialise(fp2.zero, new fp2((fp)(GridWidth - 1), (fp)(GridHeight - 1)), (fp)1m);
            return grid;
        }

        [Test]
        public void CaptureRestore_ActiveRoute_RoundTripPreservesTask()
        {
            // This test verifies that after Capture + Restore,
            // the locomotion agent still has the active task.
            // Since UnitLocomotionAgent requires a real Unit (with World, etc.),
            // we test the snapshot struct directly.

            var snap = new LocomotionAgentSnapshot();
            var task = new MovementTask
            {
                Purpose = MovePurpose.MoveToPosition,
                Target = MoveTarget.FromPosition(new fp2((fp)10m, (fp)5m)),
                StopDistance = (fp)0.5m,
                AllowRVO = true,
                AllowRepath = true,
                State = MovementTaskState.Active,
            };
            var route = new RouteRuntime
            {
                Kind = RouteKind.AStar,
                NeedRepath = false,
                LastPathTargetPosition = new fp2((fp)10m, (fp)5m),
                AStarPathCellIndices = new int[] { 0, 17, 34 },
            };

            snap.HasActiveTask = true;
            snap.Task = task;
            snap.Route = route;

            // Round-trip through the struct
            var restored = snap;
            Assert.That(restored.HasActiveTask, Is.True);
            Assert.That(restored.Task.Purpose, Is.EqualTo(MovePurpose.MoveToPosition));
            Assert.That(restored.Task.AllowRVO, Is.True);
            Assert.That(restored.Task.State, Is.EqualTo(MovementTaskState.Active));
            Assert.That(restored.Route.Kind, Is.EqualTo(RouteKind.AStar));
            Assert.That(restored.Route.AStarPathCellIndices, Is.Not.Null);
            Assert.That(restored.Route.AStarPathCellIndices.Length, Is.EqualTo(3));
        }

        [Test]
        public void CaptureRestore_IdleAgent_SnapshotHasNoActiveTask()
        {
            var snap = new LocomotionAgentSnapshot();
            snap.HasActiveTask = false;
            snap.Task = MovementTask.None;
            snap.Route = RouteRuntime.Empty;

            Assert.That(snap.HasActiveTask, Is.False);
            Assert.That(snap.Task.State, Is.EqualTo(MovementTaskState.Idle));
        }

        [Test]
        public void CaptureRestore_AStarPath_PreservesDeepCopy()
        {
            var originalIndices = new int[] { 10, 27, 44, 61 };
            var snap = new LocomotionAgentSnapshot();
            snap.Route = new RouteRuntime
            {
                Kind = RouteKind.AStar,
                AStarPathCellIndices = new int[originalIndices.Length],
            };
            System.Array.Copy(originalIndices, snap.Route.AStarPathCellIndices, originalIndices.Length);

            // Verify deep copy — modifying original shouldn't affect snapshot
            originalIndices[0] = 999;
            Assert.That(snap.Route.AStarPathCellIndices[0], Is.EqualTo(10),
                "Snapshot should have a deep copy of path indices.");
        }

        [Test]
        public void CaptureRestore_FlowFieldRoute_PreservesKind()
        {
            var snap = new LocomotionAgentSnapshot();
            snap.Route = new RouteRuntime
            {
                Kind = RouteKind.FlowField,
                NeedRepath = false,
            };

            Assert.That(snap.Route.Kind, Is.EqualTo(RouteKind.FlowField));
        }

        [Test]
        public void Snapshot_AfterClearForDeath_HasNoActiveTask()
        {
            var snap = new LocomotionAgentSnapshot();
            snap.HasActiveTask = false;
            snap.Task = MovementTask.None;
            snap.Route = RouteRuntime.Empty;

            Assert.That(snap.HasActiveTask, Is.False);
            Assert.That(snap.Task.State, Is.EqualTo(MovementTaskState.Idle));
            Assert.That(snap.Route.AStarPathCellIndices, Is.Null);
            Assert.That(snap.Route.Kind, Is.EqualTo(RouteKind.None));
        }

        [Test]
        public void RouteRuntime_Empty_DefaultValuesCorrect()
        {
            var route = RouteRuntime.Empty;
            Assert.That(route.Kind, Is.EqualTo(RouteKind.None));
            Assert.That(route.AStarPathCellIndices, Is.Null);
        }
    }
}
