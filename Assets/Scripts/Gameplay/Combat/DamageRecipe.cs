using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Combat v13.2 â€?serializable damage formula definition.
    /// Used by StageDefs and BuffEffects to author data-driven damage.
    /// Optional field on DamageRequest.
    /// </summary>
    [Serializable]
    public struct DamageRecipe
    {
        /// <summary>Type of damage dealt.</summary>
        public DamageType Type;

        /// <summary>Base damage value.</summary>
        public fp BaseValue;

        /// <summary>Ability power ratio (e.g., 0.7 = 70% AP scaling).</summary>
        public fp ApRatio;

        /// <summary>Attack damage ratio.</summary>
        public fp AdRatio;

        /// <summary>Ratio of target's max health as bonus damage.</summary>
        public fp TargetMaxHealthRatio;

        /// <summary>Ratio of target's missing health as bonus damage.</summary>
        public fp TargetMissingHealthRatio;

        /// <summary>Whether this damage can critically strike.</summary>
        public bool CanCrit;

        /// <summary>Critical strike damage multiplier (1.0 = no bonus).</summary>
        [Min(1f)]
        public fp CritMultiplier;

        /// <summary>
        /// Converts this recipe to a DamageContext for formula computation.
        /// </summary>
        public DamageContext ToContext(
            StatHandler sourceStats,
            StatHandler targetStats,
            CombatModifierSet sourceMods,
            CombatModifierSet targetMods)
        {
            fp baseDamage = BaseValue;

            // Add health-ratio bonuses
            if (targetStats != null)
            {
                if (TargetMaxHealthRatio > fp.zero)
                {
                    fp maxHp = targetStats.GetStat(StatId.MaxHealth);
                    baseDamage += maxHp * TargetMaxHealthRatio;
                }

                if (TargetMissingHealthRatio > fp.zero)
                {
                    fp maxHp = targetStats.GetStat(StatId.MaxHealth);
                    fp currentHp = targetStats.GetStat(StatId.MaxHealth); // CurrentHealth
                    fp missingHp = maxHp - currentHp;
                    if (missingHp > fp.zero)
                        baseDamage += missingHp * TargetMissingHealthRatio;
                }
            }

            return new DamageContext(
                sourceStats,
                targetStats,
                sourceMods,
                targetMods,
                baseDamage,
                ApRatio,
                AdRatio,
                CanCrit,
                CritMultiplier);
        }

        public static readonly DamageRecipe Default = new DamageRecipe
        {
            Type = DamageType.Physical,
            BaseValue = fp.zero,
            ApRatio = fp.zero,
            AdRatio = fp.one,
            TargetMaxHealthRatio = fp.zero,
            TargetMissingHealthRatio = fp.zero,
            CanCrit = true,
            CritMultiplier = (fp)2,
        };
    }
}
