using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Configuration for PhysicsWorld spatial grid (Physics v13.1 section 7.1).
    /// </summary>
    public sealed class PhysicsWorldSettings
    {
        /// <summary>
        /// Cell size for the spatial hash grid. Must be positive.
        /// Default 10 units.
        /// </summary>
        public fp GridCellSize = 10m;
    }
}