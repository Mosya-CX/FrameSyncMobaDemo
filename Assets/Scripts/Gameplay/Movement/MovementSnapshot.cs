using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct MovementSnapshot
    {
        public fp2 Position;
        public fp2 Velocity;
        public fp2 Facing;
        public fp MoveSpeed;
        public bool IsMoving;
        public fp2 TargetDirection;

        /// <summary>
        /// Current waypoint index for path-following mode.
        /// -1 when no path is active.
        /// </summary>
        public int CurrentWaypointIndex;

        /// <summary>
        /// Shallow-copied path cell indices for rollback restore.
        /// null when no path-following is active.
        /// </summary>
        public System.Collections.Generic.List<int> SnapshotPathCellIndices;

        public static readonly MovementSnapshot Default = new MovementSnapshot
        {
            Position = fp2.zero,
            Velocity = fp2.zero,
            Facing = new fp2(fp.one, fp.zero),
            MoveSpeed = fp.one,
            IsMoving = false,
            TargetDirection = fp2.zero,
            CurrentWaypointIndex = -1,
            SnapshotPathCellIndices = null,
        };
    }
}
