using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public readonly struct ProjectileSpawnRequest
    {
        public readonly int ProjectileDefId;
        public readonly UnitUid OwnerUnitUid;
        public readonly TeamId TeamSnapshot;
        public readonly SourceDescriptor Source;
        public readonly OriginActionId OriginActionId;
        public readonly fp2 StartPosition;
        public readonly fp2 Direction;
        /// <summary>Per-instance on-hit damage override; null = use the
        /// ProjectileDef's static OnHitEffects.</summary>
        public readonly ProjectileOnHitDamage[] OnHitDamageOverride;
        /// <summary>Per-instance max lifetime in ticks; 0 = use the
        /// ProjectileDef's MaxLifetimeTicks (drives dynamic cast range).</summary>
        public readonly int MaxLifetimeTicksOverride;
        /// <summary>
        /// Locked homing target (design v19: 跟踪弹体). Only used when the
        /// ProjectileDef has Homing enabled.
        /// </summary>
        public readonly UnitUid TargetUnitUid;

        public ProjectileSpawnRequest(
            int projectileDefId,
            UnitUid ownerUnitUid,
            TeamId teamSnapshot,
            SourceDescriptor source,
            OriginActionId originActionId,
            fp2 startPosition,
            fp2 direction,
            ProjectileOnHitDamage[] onHitDamageOverride = null,
            int maxLifetimeTicksOverride = 0,
            UnitUid targetUnitUid = default)
        {
            ProjectileDefId = projectileDefId;
            OwnerUnitUid = ownerUnitUid;
            TeamSnapshot = teamSnapshot;
            Source = source;
            OriginActionId = originActionId;
            StartPosition = startPosition;
            Direction = direction;
            OnHitDamageOverride = onHitDamageOverride;
            MaxLifetimeTicksOverride = maxLifetimeTicksOverride;
            TargetUnitUid = targetUnitUid;
        }
    }
}
