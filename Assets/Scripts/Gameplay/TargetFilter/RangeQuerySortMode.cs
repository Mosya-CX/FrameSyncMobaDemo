namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Determines the sort order of RangeQueryService results
    /// (Physics v13.1 section 9.8).
    /// </summary>
    public enum RangeQuerySortMode
    {
        /// <summary>Sort by PhysicsEntity2D.UidSnapshot (stable tie-break).</summary>
        Uid,

        /// <summary>Sort by Euclidean distance from query origin.</summary>
        Distance,

        /// <summary>Sort by distance, then by UidSnapshot as tie-break (recommended default).</summary>
        DistanceThenUid,
    }
}
