using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct ProjectileRuntimeSnapshot
    {
        public ProjectileUid Uid;
        public int DefId;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public fp2 PreviousPosition;
        public fp2 Position;
        public fp2 Velocity;
        public int RemainingLifetimeTicks;
        public bool IsActive;
        public int HitCount;
        public List<UnitUid> HitTargets;
    }

    /// <summary>
    /// A projectile spawn requested but not yet activated this Tick.
    /// (Snapshot Appendix v7.2 §6)
    /// </summary>
    public struct PendingSpawnRecordSnapshot
    {
        public ProjectileUid Uid;
        public int DefId;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public fp2 StartPosition;
        public fp2 Direction;
    }

    public struct ProjectileWorldSnapshot
    {
        public List<PendingSpawnRecordSnapshot> PendingSpawns;
        public List<ProjectileRuntimeSnapshot> ActiveProjectiles;

        public static readonly ProjectileWorldSnapshot Empty = new ProjectileWorldSnapshot
        {
            PendingSpawns = new List<PendingSpawnRecordSnapshot>(),
            ActiveProjectiles = new List<ProjectileRuntimeSnapshot>(),
        };
    }
}
