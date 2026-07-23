using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Per-tick locomotion evaluation status
    /// (Pathfinding Design v13.1 section 14.7).
    /// </summary>
    public enum RouteEvaluationStatus
    {
        Idle,
        Moving,
        Reached,
        Blocked,
        NoRoute,
        TargetLost,
        Cancelled,
    }

    /// <summary>
    /// Single-tick output from UnitLocomotionAgent.Evaluate().
    /// Tick-local value — does NOT enter cross-tick snapshot.
    /// Consumed by MovementHandler in the same Tick.
    /// </summary>
    public struct LocomotionResult
    {
        public UnitUid UnitUid;
        public bool HasMovement;
        public fp2 DesiredDirection;
        public fp DesiredSpeed;
        public RouteEvaluationStatus Status;

        public static LocomotionResult Idle(UnitUid uid) => new LocomotionResult
        {
            UnitUid = uid,
            Status = RouteEvaluationStatus.Idle,
        };

        public static LocomotionResult Direct(UnitUid uid, fp2 direction, fp speed) => new LocomotionResult
        {
            UnitUid = uid,
            HasMovement = true,
            DesiredDirection = direction,
            DesiredSpeed = speed,
            Status = RouteEvaluationStatus.Moving,
        };
    }
}
