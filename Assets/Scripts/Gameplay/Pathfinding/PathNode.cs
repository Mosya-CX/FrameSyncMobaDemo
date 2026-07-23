using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// A* search node. Used as the value type stored inside the indexed
    /// min-heap open-set and the closed-set lookup.
    /// Stable ordering: lower FCost first; tie-break by (CellX, CellY)
    /// so that insertion order within the same Tick never changes output.
    /// </summary>
    public struct PathNode : IComparable<PathNode>
    {
        public int CellX;
        public int CellY;
        public fp GCost;
        public fp HCost;
        public int ParentIndex;
        public bool Closed;

        public readonly fp FCost => GCost + HCost;

        public PathNode(int cellX, int cellY, fp gCost, fp hCost, int parentIndex)
        {
            CellX = cellX;
            CellY = cellY;
            GCost = gCost;
            HCost = hCost;
            ParentIndex = parentIndex;
            Closed = false;
        }

        public int CompareTo(PathNode other)
        {
            fp fSelf = FCost;
            fp fOther = other.FCost;
            if (fSelf < fOther) return -1;
            if (fSelf > fOther) return 1;
            if (CellX != other.CellX) return CellX.CompareTo(other.CellX);
            return CellY.CompareTo(other.CellY);
        }

        public static readonly PathNode Invalid = new PathNode
        {
            CellX = -1,
            CellY = -1,
            ParentIndex = -1,
        };
    }
}

