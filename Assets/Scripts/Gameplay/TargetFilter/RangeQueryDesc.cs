using FrameSyncMoba.Physics;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Input descriptor for a RangeQueryService query
    /// (Physics v13.1 section 9.2).
    /// </summary>
    public struct RangeQueryDesc
    {
        /// <summary>Query shape (Circle or Rect).</summary>
        public PhysicsShape2D Shape;

        /// <summary>World-space position and rotation of the query origin.</summary>
        public PhysicsTransform2D Transform;

        /// <summary>Target filtering criteria.</summary>
        public UnitTargetFilter TargetFilter;

        /// <summary>Result sort order. Default: DistanceThenUid.</summary>
        public RangeQuerySortMode SortMode;

        /// <summary>Maximum results to return. Zero means unlimited.</summary>
        public int MaxResult;
    }
}
