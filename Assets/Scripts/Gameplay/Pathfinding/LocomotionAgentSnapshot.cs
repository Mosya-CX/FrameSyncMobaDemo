namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Snapshot for UnitLocomotionAgent cross-tick state
    /// (Pathfinding Design v13.1 section 15.3).
    /// </summary>
    public struct LocomotionAgentSnapshot
    {
        public bool HasActiveTask;
        public MovementTask Task;
        public RouteRuntime Route;
        public PathFollowerState FollowerState;

        public static readonly LocomotionAgentSnapshot Empty = new LocomotionAgentSnapshot
        {
            HasActiveTask = false,
            Task = MovementTask.None,
            Route = RouteRuntime.Empty,
            FollowerState = PathFollowerState.Empty,
        };
    }
}
