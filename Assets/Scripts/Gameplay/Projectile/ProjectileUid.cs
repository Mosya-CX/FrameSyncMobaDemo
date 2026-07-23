using System;

namespace FrameSyncMoba.Unit
{
    public readonly struct ProjectileUid : IEquatable<ProjectileUid>, IComparable<ProjectileUid>
    {
        public readonly int SpawnLogicTick;
        public readonly int RuntimeEntityPrefabId;
        public readonly byte SpawnSequenceInTick;

        public ProjectileUid(int spawnLogicTick, int runtimeEntityPrefabId, byte spawnSequenceInTick)
        {
            SpawnLogicTick = spawnLogicTick;
            RuntimeEntityPrefabId = runtimeEntityPrefabId;
            SpawnSequenceInTick = spawnSequenceInTick;
        }

        public bool IsValid => SpawnLogicTick >= 0 && RuntimeEntityPrefabId > 0;
        public static readonly ProjectileUid Invalid = default;

        public bool Equals(ProjectileUid other) =>
            SpawnLogicTick == other.SpawnLogicTick &&
            RuntimeEntityPrefabId == other.RuntimeEntityPrefabId &&
            SpawnSequenceInTick == other.SpawnSequenceInTick;
        public override bool Equals(object obj) => obj is ProjectileUid other && Equals(other);
        public override int GetHashCode() =>
            SpawnLogicTick ^ RuntimeEntityPrefabId ^ SpawnSequenceInTick;
        public int CompareTo(ProjectileUid other)
        {
            int cmp = SpawnLogicTick.CompareTo(other.SpawnLogicTick);
            if (cmp != 0) return cmp;
            cmp = RuntimeEntityPrefabId.CompareTo(other.RuntimeEntityPrefabId);
            if (cmp != 0) return cmp;
            return SpawnSequenceInTick.CompareTo(other.SpawnSequenceInTick);
        }
        public static bool operator ==(ProjectileUid a, ProjectileUid b) => a.Equals(b);
        public static bool operator !=(ProjectileUid a, ProjectileUid b) => !a.Equals(b);
        public override string ToString() =>
            $"Projectile({SpawnLogicTick},{RuntimeEntityPrefabId},{SpawnSequenceInTick})";
    }
}
