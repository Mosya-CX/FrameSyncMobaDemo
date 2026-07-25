using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Per-unit RVO solver output for one tick.
    /// Tick-local — does NOT enter cross-tick snapshot.
    /// (Pathfinding Design v13.1 section 14.8)
    /// </summary>
    public struct RvoResult
    {
        public UnitUid UnitUid;
        public fp2 FinalVelocity;

        public bool HasResult => FinalVelocity.x != fp.zero || FinalVelocity.y != fp.zero;
    }
}
