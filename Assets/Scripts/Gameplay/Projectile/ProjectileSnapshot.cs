using System;
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
        public UnitUid[] HitTargets;
    }

    /// <summary>
    /// A projectile spawn requested but not yet activated this Tick.
    /// (Snapshot Appendix v7.2 section 6)
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
        public PendingSpawnRecordSnapshot[] PendingSpawns;
        public ProjectileRuntimeSnapshot[] ActiveProjectiles;

        public static readonly ProjectileWorldSnapshot Empty = new ProjectileWorldSnapshot
        {
            PendingSpawns = Array.Empty<PendingSpawnRecordSnapshot>(),
            ActiveProjectiles = Array.Empty<ProjectileRuntimeSnapshot>(),
        };
    }
}
