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
        public SourceDescriptor Source;
        public OriginActionId OriginActionId;
        public fp2 PreviousPosition;
        public fp2 Position;
        public fp2 Velocity;
        public int RemainingLifetimeTicks;
        public bool IsActive;
        public bool EndRequested;
        public ProjectileEndReason EndReason;
        public int TotalHitCount;
        public int RemainingPierceCount;
        public int RemainingBounceCount;
        public int NextQueryLogicTick;
        public ProjectileHitRecord[] HitRecords;
        public ProjectileOnHitDamage[] OnHitDamageOverride;
        public UnitUid TargetUnitUid;
    }

    public struct PendingSpawnRecordSnapshot
    {
        public ProjectileUid Uid;
        public int DefId;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public SourceDescriptor Source;
        public OriginActionId OriginActionId;
        public fp2 StartPosition;
        public fp2 Direction;
        public ProjectileOnHitDamage[] OnHitDamageOverride;
        public int MaxLifetimeTicksOverride;
        public UnitUid TargetUnitUid;
    }

    public struct ProjectileWorldSnapshot
    {
        public PendingSpawnRecordSnapshot[] PendingSpawns;
        public ProjectileRuntimeSnapshot[] ActiveProjectiles;

        public static readonly ProjectileWorldSnapshot Empty =
            new ProjectileWorldSnapshot
            {
                PendingSpawns =
                    Array.Empty<PendingSpawnRecordSnapshot>(),
                ActiveProjectiles =
                    Array.Empty<ProjectileRuntimeSnapshot>(),
            };
    }
}
