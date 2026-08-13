using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class ProjectileDef
    {
        public int DefId;
        public int RuntimeEntityPrefabId;
        public fp Speed;
        public fp Acceleration;
        /// <summary>
        /// Homing projectile (design v19: 跟踪弹体): when set, the projectile
        /// steers toward its locked TargetUnitUid each Tick and falls back to
        /// straight-line motion when the target is missing.
        /// </summary>
        public bool Homing;
        public int MaxLifetimeTicks;
        public fp HitRadius;
        public ProjectileTargetFilter TargetFilter =
            ProjectileTargetFilter.DefaultEnemy;
        public ProjectileHitPolicy HitPolicy =
            ProjectileHitPolicy.DefaultSingleHit;
        public ProjectileOnHitEffects OnHitEffects = ProjectileOnHitEffects.Empty;
        public ProjectileAoEConfig AoE = ProjectileAoEConfig.None;
        public ProjectileContainmentZone ContainmentZone;
        public bool IsValid =>
            DefId > 0 &&
            RuntimeEntityPrefabId > 0 &&
            MaxLifetimeTicks > 0 &&
            HitRadius >= fp.zero;

        public void ValidateOrThrow()
        {
            if (!IsValid)
                throw new InvalidOperationException(
                    $"ProjectileDef {DefId} has invalid identity, lifetime or radius.");
            TargetFilter.ValidateOrThrow();
            HitPolicy.ValidateOrThrow();
            OnHitEffects.ValidateOrThrow();
        }
    }
}
