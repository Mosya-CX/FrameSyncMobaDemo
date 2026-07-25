using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// RVO solver configuration (static, not in snapshot).
    /// (Pathfinding Design v13.1 section 10.7)
    /// </summary>
    public struct RVOConfig
    {
        public fp NeighborSearchRadius;
        public int MaxNeighbors;
        public fp TimeHorizon;
        public int SampleCount;

        public static readonly RVOConfig Default = new RVOConfig
        {
            NeighborSearchRadius = (fp)3.0m,
            MaxNeighbors = 16,
            TimeHorizon = (fp)1.0m,
            SampleCount = 64,
        };
    }
}
