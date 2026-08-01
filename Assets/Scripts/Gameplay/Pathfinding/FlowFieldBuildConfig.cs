namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Flow-field build configuration weights.
    /// (Pathfinding Design v13.1 section 8.7)
    /// </summary>
    public struct FlowFieldBuildConfig
    {
        /// <summary>Weight for cost-drop magnitude. Higher = prefer steeper descent. Default 100.</summary>
        public int CostDropWeight;

        /// <summary>Weight for wall-tangent alignment. Default 20.</summary>
        public int WallAlignWeight;

        /// <summary>Weight for forward consistency with the owned lane skeleton. Default 40.</summary>
        public int SmoothWeight;

        /// <summary>Weight for distance-scaled pull toward the lane skeleton. Default 40.</summary>
        public int LaneWeight;

        /// <summary>
        /// Offline OwnerLane penalty per cell of distance from a lane skeleton.
        /// It is intentionally independent from the softer direction potential.
        /// </summary>
        public int OwnershipWeight;

        public static readonly FlowFieldBuildConfig Default = new FlowFieldBuildConfig
        {
            CostDropWeight = 100,
            WallAlignWeight = 20,
            SmoothWeight = 40,
            LaneWeight = 40,
            OwnershipWeight = 1000,
        };
    }
}
