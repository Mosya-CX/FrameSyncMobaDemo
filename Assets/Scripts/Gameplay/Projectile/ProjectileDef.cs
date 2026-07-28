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
        public int MaxLifetimeTicks;
        public fp HitRadius;
        public ProjectileTargetFilter TargetFilter =
            ProjectileTargetFilter.DefaultEnemy;
        public ProjectileHitPolicy HitPolicy =
            ProjectileHitPolicy.DefaultSingleHit;
        public ProjectileOnHitEffects OnHitEffects = ProjectileOnHitEffects.Empty;
        public ProjectileAoEConfig AoE = ProjectileAoEConfig.None;
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
