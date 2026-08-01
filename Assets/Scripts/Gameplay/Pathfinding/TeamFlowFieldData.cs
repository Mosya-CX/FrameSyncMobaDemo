using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Baked flow field data for one team + radius class combination.
    /// NOT in Gameplay snapshot — static configuration (Design v13.1 section 8.9).
    /// (Pathfinding Design v13.1 section 8.3)
    /// </summary>
    [Serializable]
    public struct TeamFlowFieldData
    {
        public FlowFieldKey Key;

        /// <summary>Final integrated cost per cell. INF for unwalkable/unreachable.</summary>
        public int[] Cost;

        /// <summary>Lane index owning each cell (section 8.6). 255 = none.</summary>
        public byte[] OwnerLane;

        /// <summary>Flat index of the next descending cell. -1 for sink/none.</summary>
        public int[] NextCell;

        /// <summary>Dir8 cast to byte per cell. 0 = None.</summary>
        public byte[] DirectionCode;

        public int Width;
        public int Height;
        public int CellCount;

        public bool IsValid => Cost != null && Cost.Length > 0 && Cost.Length == CellCount;

        public static readonly TeamFlowFieldData Empty = new TeamFlowFieldData();
    }
}
