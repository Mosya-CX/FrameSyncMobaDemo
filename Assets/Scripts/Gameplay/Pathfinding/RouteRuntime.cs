using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Route solution kind (Pathfinding Design v13.1 section 14.6).
    /// </summary>
    public enum RouteKind
    {
        None,
        Direct,
        AStar,
        FlowField,
    }

    /// <summary>
    /// Route runtime state owned by UnitLocomotionAgent.
    /// Survives across Ticks and must be captured/restored for rollback.
    /// </summary>
    public struct RouteRuntime
    {
        public RouteKind Kind;
        public bool NeedRepath;
        public int NextRepathTick;
        public fp2 LastPathTargetPosition;

        /// <summary>
        /// Cell indices for A* path. Populated by AStarPathService.
        /// </summary>
        public int[] AStarPathCellIndices;

        /// <summary>
        /// Flow-field key. Populated by TeamFlowFieldService (deferred).
        /// </summary>
        public int FlowFieldKey;

        /// <summary>
        /// Path-follower state for route tracking.
        /// Captured/restored in LocomotionAgentSnapshot.
        /// </summary>
        public PathFollowerState FollowerState;

        public static readonly RouteRuntime Empty = new RouteRuntime
        {
            Kind = RouteKind.None,
            NeedRepath = false,
            FollowerState = PathFollowerState.Empty,
        };
    }
}
