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

        /// <summary>Weight for direction consistency with nearby cells. Default 10.</summary>
        public int SmoothWeight;

        /// <summary>Weight for lane-skeleton bonus. Default 5.</summary>
        public int LaneWeight;

        public static readonly FlowFieldBuildConfig Default = new FlowFieldBuildConfig
        {
            CostDropWeight = 100,
            WallAlignWeight = 20,
            SmoothWeight = 10,
            LaneWeight = 5,
        };
    }
}
