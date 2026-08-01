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

        /// <summary>
        /// Ordered lane centerline used by the offline builder as a deterministic
        /// low-cost corridor. Runtime flow lookup does not read these points.
        /// </summary>
        public fp2[] GuidePoints;

        /// <summary>Half-width of the preferred lane corridor in world units.</summary>
        public fp GuideHalfWidth;

        /// <summary>
        /// Quadratic potential weight for distance from the lane centerline.
        /// The builder adds only potential increases to an edge, producing a
        /// gradual pull instead of forcing every off-lane cell straight inward.
        /// </summary>
        public int GuideCostPerCell;

        /// <summary>
        /// Additional quadratic potential weight outside the preferred corridor.
        /// Zero disables the outside-corridor contribution.
        /// </summary>
        public int OffGuideCostPerCell;

        /// <summary>Lane index for priority tie-breaking (section 8.6). Lower = higher priority.</summary>
        public byte LaneIndex;

        public bool IsValid => Targets != null && Targets.Length > 0;
    }
}
