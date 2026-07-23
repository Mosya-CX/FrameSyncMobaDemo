using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable runtime identity for one deterministic Unit lifecycle.
    /// </summary>
    public readonly struct UnitUid : IEquatable<UnitUid>, IComparable<UnitUid>
    {
        public readonly int SpawnLogicTick;
        public readonly int RuntimeEntityPrefabId;
        public readonly byte SpawnSequenceInTick;

        public UnitUid(
            int spawnLogicTick,
            int runtimeEntityPrefabId,
            byte spawnSequenceInTick)
        {
            SpawnLogicTick = spawnLogicTick;
            RuntimeEntityPrefabId = runtimeEntityPrefabId;
            SpawnSequenceInTick = spawnSequenceInTick;
        }

        public int CompareTo(UnitUid other)
        {
            int tickComparison = SpawnLogicTick.CompareTo(other.SpawnLogicTick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            int prefabComparison = RuntimeEntityPrefabId.CompareTo(other.RuntimeEntityPrefabId);
            if (prefabComparison != 0)
            {
                return prefabComparison;
            }

            return SpawnSequenceInTick.CompareTo(other.SpawnSequenceInTick);
        }

        /// <summary>
        /// Returns true when this Uid represents a valid spawn identity.
        /// A default/zero Uid is invalid.</summary>
        public bool IsValid() =>
            SpawnLogicTick > 0 || RuntimeEntityPrefabId > 0 || SpawnSequenceInTick > 0;

        public bool Equals(UnitUid other)
        {
            return SpawnLogicTick == other.SpawnLogicTick
                && RuntimeEntityPrefabId == other.RuntimeEntityPrefabId
                && SpawnSequenceInTick == other.SpawnSequenceInTick;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitUid other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + SpawnLogicTick;
                hash = (hash * 31) + RuntimeEntityPrefabId;
                hash = (hash * 31) + SpawnSequenceInTick;
                return hash;
            }
        }

        public static bool operator ==(UnitUid left, UnitUid right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnitUid left, UnitUid right)
        {
            return !left.Equals(right);
        }
    }
}
