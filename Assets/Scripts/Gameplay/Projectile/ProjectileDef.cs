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
        public int MaxHitCount = 1;
        public bool DestroyOnFirstHit = true;
        public ProjectileOnHitEffects OnHitEffects = ProjectileOnHitEffects.Empty;
        public ProjectileAoEConfig AoE = ProjectileAoEConfig.None;
        public bool IsValid => DefId != 0 && RuntimeEntityPrefabId > 0;
    }
}
