namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Project public read-only runtime UID query value (Physics v13.1 section 2.3).
    /// Business sides (Unit, Projectile) convert their authoritative UID into this
    /// common read-only value for physics grid deduplication, stable sorting,
    /// collision PairKey, and logging. Physics never decomposes, allocates or
    /// re-encodes UIDs.
    /// 
    /// This slice wraps the three UnitUid components (SpawnLogicTick,
    /// RuntimeEntityPrefabId, SpawnSequenceInTick) which are sufficient for
    /// Unit entities. Projectile Uid conversion will be added when the
    /// Projectile system is implemented.
    /// </summary>
    public readonly struct RuntimeUidQueryValue :
        System.IEquatable<RuntimeUidQueryValue>,
        System.IComparable<RuntimeUidQueryValue>
    {
        public readonly int SpawnLogicTick;
        public readonly int RuntimeEntityPrefabId;
        public readonly byte SpawnSequenceInTick;

        public RuntimeUidQueryValue(int spawnLogicTick, int runtimeEntityPrefabId, byte spawnSequenceInTick)
        {
            SpawnLogicTick = spawnLogicTick;
            RuntimeEntityPrefabId = runtimeEntityPrefabId;
            SpawnSequenceInTick = spawnSequenceInTick;
        }

        public bool Equals(RuntimeUidQueryValue other)
        {
            return SpawnLogicTick == other.SpawnLogicTick
                && RuntimeEntityPrefabId == other.RuntimeEntityPrefabId
                && SpawnSequenceInTick == other.SpawnSequenceInTick;
        }

        public override bool Equals(object obj) => obj is RuntimeUidQueryValue other && Equals(other);

        public int CompareTo(RuntimeUidQueryValue other)
        {
            int tick = SpawnLogicTick.CompareTo(other.SpawnLogicTick);
            if (tick != 0) return tick;
            int prefab = RuntimeEntityPrefabId.CompareTo(other.RuntimeEntityPrefabId);
            if (prefab != 0) return prefab;
            return SpawnSequenceInTick.CompareTo(other.SpawnSequenceInTick);
        }

        public bool IsValid =>
            SpawnLogicTick > 0 || RuntimeEntityPrefabId > 0 || SpawnSequenceInTick > 0;

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

        public static bool operator ==(RuntimeUidQueryValue left, RuntimeUidQueryValue right) => left.Equals(right);
        public static bool operator !=(RuntimeUidQueryValue left, RuntimeUidQueryValue right) => !left.Equals(right);
    }
}
