using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct ProjectileOnHitDamage
    {
        public int Amount;
        public DamageType DamageType;
        public fp DamageRatio;
        public static readonly ProjectileOnHitDamage None = default;
    }

    public struct ProjectileOnHitBuff
    {
        public BuffConfigId BuffId;
        public int DurationTicks;
        public bool IsValid => BuffId.IsValid && DurationTicks > 0;
    }

    public struct ProjectileOnHitCC
    {
        public CrowdControlType CCType;
        public int DurationTicks;
        public bool IsValid => DurationTicks > 0;
    }

    public struct ProjectileOnHitEffects
    {
        public ProjectileOnHitDamage[] DamageEffects;
        public ProjectileOnHitBuff[] BuffEffects;
        public ProjectileOnHitCC[] CCEffects;
        public bool HasAnyEffect =>
            (DamageEffects != null && DamageEffects.Length > 0) ||
            (BuffEffects != null && BuffEffects.Length > 0) ||
            (CCEffects != null && CCEffects.Length > 0);
        public static readonly ProjectileOnHitEffects Empty = default;
    }
}
