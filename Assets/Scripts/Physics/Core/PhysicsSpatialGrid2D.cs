using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Deterministic spatial hash grid for physics range queries
    /// (Physics v13.1 section 7). Entities are inserted into all overlapping
    /// cells. Cross-cell queries deduplicate by RuntimeUidQueryValue and
    /// return results sorted by UidSnapshot.
    /// </summary>
    public sealed class PhysicsSpatialGrid2D
    {
        private readonly fp cellSize;
        private readonly Dictionary<CellKey, List<PhysicsEntity2D>> cells = new Dictionary<CellKey, List<PhysicsEntity2D>>();
        private readonly HashSet<RuntimeUidQueryValue> visitedBuffer = new HashSet<RuntimeUidQueryValue>();

        private struct CellKey : IEquatable<CellKey>
        {
            public readonly int X;
            public readonly int Y;

            public CellKey(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(CellKey other) => X == other.X && Y == other.Y;

            public override bool Equals(object obj) => obj is CellKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 73856093) ^ (Y * 19349663);
                }
            }
        }

        public PhysicsSpatialGrid2D(fp cellSize)
        {
            if (cellSize <= fp.zero)
            {
                throw new ArgumentException("CellSize must be positive.", nameof(cellSize));
            }
            this.cellSize = cellSize;
        }

        public fp CellSize => cellSize;

        public void Clear()
        {
            cells.Clear();
        }

        /// <summary>
        /// Inserts an entity into all cells overlapping its bounds
        /// (Physics v13.1 section 7.5).
        /// </summary>
        public void Insert(PhysicsEntity2D entity, in PhysicsBounds2D bounds)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            int minX = FloorToInt(bounds.Min.x / cellSize);
            int maxX = FloorToInt(bounds.Max.x / cellSize);
            int minY = FloorToInt(bounds.Min.y / cellSize);
            int maxY = FloorToInt(bounds.Max.y / cellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var key = new CellKey(x, y);
                    if (!cells.TryGetValue(key, out List<PhysicsEntity2D> bucket))
                    {
                        bucket = new List<PhysicsEntity2D>();
                        cells[key] = bucket;
                    }
                    bucket.Add(entity);
                }
            }
        }

        /// <summary>
        /// Collects unique candidate entities overlapping the query bounds,
        /// deduplicated by UidSnapshot and sorted by UidSnapshot
        /// (Physics v13.1 section 7.5).
        /// </summary>
        public void CollectCandidates(in PhysicsBounds2D queryBounds, List<PhysicsEntity2D> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            visitedBuffer.Clear();

            int minX = FloorToInt(queryBounds.Min.x / cellSize);
            int maxX = FloorToInt(queryBounds.Max.x / cellSize);
            int minY = FloorToInt(queryBounds.Min.y / cellSize);
            int maxY = FloorToInt(queryBounds.Max.y / cellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var key = new CellKey(x, y);
                    if (!cells.TryGetValue(key, out List<PhysicsEntity2D> bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        PhysicsEntity2D entity = bucket[i];
                        RuntimeUidQueryValue uid = entity.QueryInfo.UidSnapshot;

                        if (!visitedBuffer.Add(uid))
                        {
                            continue;
                        }

                        output.Add(entity);
                    }
                }
            }

            output.Sort(CompareEntitiesByUid);
        }

        private static int CompareEntitiesByUid(PhysicsEntity2D a, PhysicsEntity2D b)
        {
            RuntimeUidQueryValue ua = a.QueryInfo.UidSnapshot;
            RuntimeUidQueryValue ub = b.QueryInfo.UidSnapshot;

            int c = ua.SpawnLogicTick.CompareTo(ub.SpawnLogicTick);
            if (c != 0) return c;
            c = ua.RuntimeEntityPrefabId.CompareTo(ub.RuntimeEntityPrefabId);
            if (c != 0) return c;
            return ua.SpawnSequenceInTick.CompareTo(ub.SpawnSequenceInTick);
        }

        private static int FloorToInt(fp value)
        {
            fp floored = fpmath.floor(value);
            return (int)floored;
        }
    }
}