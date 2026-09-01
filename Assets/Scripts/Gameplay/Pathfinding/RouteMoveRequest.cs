using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Accept/reject result for a route-move request.
    /// </summary>
    public enum MoveAcceptResult
    {
        Accepted,
        Rejected_NoAgent,
        Rejected_InvalidTarget,
        Rejected_AlreadyActive,
    }

    /// <summary>
    /// Reason for cancelling a movement route.
    /// </summary>
    public enum MoveCancelReason
    {
        UserCommand,
        ControlInterrupt,
        Death,
        Teleport,
        NewRoute,
        AttackStarted,
        AbilityCastStarted,
        TargetLost,
    }

    /// <summary>
    /// Route-move request submitted to UnitLocomotionAgent
    /// (Pathfinding Design v13.1 section 5).
    /// </summary>
    public struct RouteMoveRequest
    {
        public MoveTarget Target;
        public MovePurpose Purpose;
        public RouteKind Kind;
        public fp StopDistance;
        public bool AllowRepath;
        public bool AllowRVO;

        public static RouteMoveRequest ToPosition(fp2 position, fp stopDistance = default) =>
            new RouteMoveRequest
            {
                Target = MoveTarget.FromPosition(position),
                Purpose = MovePurpose.PointMove,
                StopDistance = stopDistance,
                AllowRepath = true,
            };

        public static RouteMoveRequest FollowUnit(
            UnitUid uid,
            fp stopDistance = default,
            MovePurpose purpose =
                MovePurpose.ChaseForAttack) =>
            new RouteMoveRequest
            {
                Target = MoveTarget.FromUnit(uid),
                Purpose = purpose,
                StopDistance = stopDistance,
                AllowRepath = true,
            };
    }
}
