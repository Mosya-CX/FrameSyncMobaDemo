using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Movement request purpose (Pathfinding Design v13.1 section 5).
    /// </summary>
    public enum MovePurpose
    {
        PointMove = 0,
        ChaseForAttack = 1,
        ChaseForCast = 2,
        LaneAdvance = 3,
        ReturnToCamp = 4,
        ControlMove = 5,

        // Compatibility aliases for snapshots/tests authored before the
        // formal v13.1 purpose names were applied.
        MoveToPosition = PointMove,
        FollowTarget = ChaseForAttack,
        Flee = ControlMove,
        MoveToLane = LaneAdvance,
    }

    /// <summary>
    /// Current state of a movement task.
    /// </summary>
    public enum MovementTaskState
    {
        Idle,
        Active,
        Completed,
        Cancelled,
    }

    /// <summary>
    /// Movement target: either a world position or a unit to follow.
    /// </summary>
    public struct MoveTarget
    {
        public fp2? Position;
        public UnitUid? TargetUid;

        public bool HasTarget => Position.HasValue || TargetUid.HasValue;

        public static MoveTarget FromPosition(fp2 position) => new MoveTarget { Position = position };

        public static MoveTarget FromUnit(UnitUid uid) => new MoveTarget { TargetUid = uid };

        public static readonly MoveTarget None = default;
    }

    /// <summary>
    /// Active movement task held by UnitLocomotionAgent
    /// (Pathfinding Design v13.1 section 14.5).
    /// </summary>
    public struct MovementTask
    {
        public MovePurpose Purpose;
        public MoveTarget Target;
        public fp StopDistance;
        public bool AllowRVO;
        public bool AllowRepath;
        public MovementTaskState State;

        public static readonly MovementTask None = default;
    }
}
