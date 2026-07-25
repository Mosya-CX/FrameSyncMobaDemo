using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// 8-direction code for flow-field direction storage
    /// (Pathfinding Design v13.1 section 8.3).
    /// Cast to byte for compact array storage in TeamFlowFieldData.
    /// </summary>
    public enum Dir8 : byte
    {
        None = 0,
        N  = 1, NE = 2, E  = 3, SE = 4,
        S  = 5, SW = 6, W  = 7, NW = 8,
    }

    public static class Dir8Helper
    {
        // Offsets: index matches Dir8 enum value
        private static readonly (int dx, int dy)[] Offsets = new (int, int)[]
        {
            ( 0,  0), // None
            ( 0, -1), // N
            ( 1, -1), // NE
            ( 1,  0), // E
            ( 1,  1), // SE
            ( 0,  1), // S
            (-1,  1), // SW
            (-1,  0), // W
            (-1, -1), // NW
        };

        // Normalized fp2 direction vectors
        private static readonly fp2[] DirectionVectors = new fp2[]
        {
            fp2.zero,                              // None
            new fp2(fp.zero, -fp.one),             // N
            new fp2((fp)0.7071m, -(fp)0.7071m),    // NE
            new fp2(fp.one, fp.zero),              // E
            new fp2((fp)0.7071m, (fp)0.7071m),     // SE
            new fp2(fp.zero, fp.one),              // S
            new fp2(-(fp)0.7071m, (fp)0.7071m),    // SW
            new fp2(-fp.one, fp.zero),             // W
            new fp2(-(fp)0.7071m, -(fp)0.7071m),   // NW
        };

        /// <summary>Convert Dir8 to normalized fp2 direction vector (section 8.8).</summary>
        public static fp2 ToFP2(Dir8 d) => DirectionVectors[(int)d];

        /// <summary>Delta (dx, dy) for this direction.</summary>
        public static (int dx, int dy) Delta(Dir8 d) => Offsets[(int)d];

        /// <summary>True if this is a diagonal direction.</summary>
        public static bool IsDiagonal(Dir8 d) => d == Dir8.NE || d == Dir8.SE || d == Dir8.SW || d == Dir8.NW;

        /// <summary>
        /// Derive Dir8 from the difference between two flat cell indices.
        /// Used to convert NextCell direction into a Dir8 code.
        /// Returns None if cells are not adjacent (8-neighborhood).
        /// </summary>
        public static Dir8 FromCellDelta(int fromCell, int toCell, int gridWidth)
        {
            int fromX = fromCell % gridWidth;
            int fromY = fromCell / gridWidth;
            int toX = toCell % gridWidth;
            int toY = toCell / gridWidth;
            int dx = toX - fromX;
            int dy = toY - fromY;

            for (int i = 1; i < Offsets.Length; i++)
            {
                if (Offsets[i].dx == dx && Offsets[i].dy == dy)
                    return (Dir8)i;
            }
            return Dir8.None;
        }

        /// <summary>
        /// Tie-breaker priority for section 8.7 DirTieBreaker.
        /// Lower value = higher priority. N > NE > E > SE > S > SW > W > NW.
        /// </summary>
        public static int Priority(Dir8 d) => (int)d;
    }
}
