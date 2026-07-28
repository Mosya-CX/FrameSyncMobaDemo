using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct ProjectileOnHitDamage
    {
        public fp Amount;
        public DamageType DamageType;
        public fp DamageRatio;
        public int RecipeId;
        public bool IsValid =>
            Amount >= fp.zero &&
            DamageRatio >= fp.zero &&
            RecipeId > 0 &&
            (Amount > fp.zero || DamageRatio > fp.zero);
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

        public void ValidateOrThrow()
        {
            if (DamageEffects != null)
                for (int i = 0; i < DamageEffects.Length; i++)
                    if (!DamageEffects[i].IsValid)
                        throw new InvalidOperationException(
                            $"Projectile damage effect {i} is invalid.");
            if (BuffEffects != null)
                for (int i = 0; i < BuffEffects.Length; i++)
                    if (!BuffEffects[i].IsValid)
                        throw new InvalidOperationException(
                            $"Projectile Buff effect {i} is invalid.");
            if (CCEffects != null)
                for (int i = 0; i < CCEffects.Length; i++)
                    if (!CCEffects[i].IsValid)
                        throw new InvalidOperationException(
                            $"Projectile CC effect {i} is invalid.");
        }
    }
}
