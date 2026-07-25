using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Configuration for a single lane's target positions.
    /// Used by TeamFlowFieldService to build per-lane cost fields.
    /// </summary>
    public struct LaneTargetConfig
    {
        /// <summary>World-space target positions for this lane (usually behind enemy base).</summary>
        public fp2[] Targets;

        /// <summary>Lane index for priority tie-breaking (section 8.6). Lower = higher priority.</summary>
        public byte LaneIndex;

        public bool IsValid => Targets != null && Targets.Length > 0;
    }
}
