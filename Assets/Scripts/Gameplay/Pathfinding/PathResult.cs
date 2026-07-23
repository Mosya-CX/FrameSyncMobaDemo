namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Outcome of an A* or flow-field search.
    /// </summary>
    public enum PathStatus
    {
        Success,
        InvalidStart,
        InvalidEnd,
        EndBlocked,
        NoPath,
        MaxIterationReached,
        SystemNotReady,
    }

    /// <summary>
    /// Path search result containing status and cell-index route.
    /// Cell indices are in PathGridMap2D coordinate space.
    /// </summary>
    public struct PathResult
    {
        public bool Success;
        public PathStatus Status;
        public int[] PathCellIndices;

        public static PathResult Ok(int[] indices) => new PathResult
        {
            Success = true,
            Status = PathStatus.Success,
            PathCellIndices = indices,
        };

        public static PathResult Failed(PathStatus status) => new PathResult
        {
            Status = status,
            PathCellIndices = null,
        };

        public static readonly PathResult Empty = default;
    }
}
