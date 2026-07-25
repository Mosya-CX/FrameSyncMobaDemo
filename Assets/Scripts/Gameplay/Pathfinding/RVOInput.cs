using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Per-unit input to the RVO solver for one tick.
    /// Tick-local — does NOT enter cross-tick snapshot.
    /// (Pathfinding Design v13.1 section 10.4)
    /// </summary>
    public struct RVOInput
    {
        public UnitUid SelfUid;
        public fp2 Position;         // Pre-move position from PhysicsEntity2D
        public fp2 DesiredVelocity;  // From LocomotionResult
        public fp Radius;            // From PhysicsEntity2D.Shape
        public fp MaxSpeed;
    }
}
