using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Combat v13.2 — input parameters for the damage formula.
    /// Aggregates source and target stats, modifiers, and base values
    /// needed to compute a DamagePayload.
    /// </summary>
    public readonly struct DamageContext
    {
        /// <summary>Source unit's stat handler.</summary>
        public readonly StatHandler SourceStats;

        /// <summary>Target unit's stat handler.</summary>
        public readonly StatHandler TargetStats;

        /// <summary>Source unit's combat modifier set.</summary>
        public readonly CombatModifierSet SourceMods;

        /// <summary>Target unit's combat modifier set.</summary>
        public readonly CombatModifierSet TargetMods;

        /// <summary>Base damage value before ratios are applied.</summary>
        public readonly fp BaseDamage;

        /// <summary>Ability power scaling ratio.</summary>
        public readonly fp AbilityPowerRatio;

        /// <summary>Attack damage scaling ratio.</summary>
        public readonly fp AttackDamageRatio;

        /// <summary>Whether this damage can critically strike.</summary>
        public readonly bool CanCrit;

        /// <summary>Critical strike multiplier (e.g., 2.0 for 200%).</summary>
        public readonly fp CritMultiplier;

        public DamageContext(
            StatHandler sourceStats,
            StatHandler targetStats,
            CombatModifierSet sourceMods,
            CombatModifierSet targetMods,
            fp baseDamage,
            fp abilityPowerRatio,
            fp attackDamageRatio,
            bool canCrit,
            fp critMultiplier)
        {
            SourceStats = sourceStats;
            TargetStats = targetStats;
            SourceMods = sourceMods;
            TargetMods = targetMods;
            BaseDamage = baseDamage;
            AbilityPowerRatio = abilityPowerRatio;
            AttackDamageRatio = attackDamageRatio;
            CanCrit = canCrit;
            CritMultiplier = critMultiplier;
        }

        /// <summary>
        /// Computes the raw damage before mitigation using the standard formula:
        /// Raw = BaseDamage + AP * ApRatio + AD * AdRatio
        /// </summary>
        public fp ComputeRawDamage()
        {
            fp raw = BaseDamage;

            if (SourceStats != null)
            {
                if (AbilityPowerRatio > fp.zero)
                {
                    fp ap = SourceStats.GetStat(StatId.AbilityPower);
                    raw += ap * AbilityPowerRatio;
                }

                if (AttackDamageRatio > fp.zero)
                {
                    fp ad = SourceStats.GetStat(StatId.AttackDamage);
                    raw += ad * AttackDamageRatio;
                }
            }

            return raw;
        }

        public static readonly DamageContext Empty = default;
    }
}
