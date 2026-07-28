using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Combat v13.2 — the computed result of a single damage instance,
    /// including all modifiers applied. Used as the output of the damage
    /// formula computation within CombatSystem.
    /// </summary>
    public readonly struct DamagePayload
    {
        /// <summary>Final damage amount after all modifiers and reductions.</summary>
        public readonly int FinalDamage;

        /// <summary>Type of damage dealt.</summary>
        public readonly DamageType Type;

        /// <summary>Unit that originated the damage.</summary>
        public readonly UnitUid SourceUnit;

        /// <summary>Unit receiving the damage.</summary>
        public readonly UnitUid TargetUnit;

        /// <summary>Ability ID that caused this damage (0 = attack).</summary>
        public readonly int AbilityId;

        /// <summary>Stable sequence number within the current Tick.</summary>
        public readonly int SequenceInTick;

        /// <summary>Whether this damage was a critical strike.</summary>
        public readonly bool IsCritical;

        /// <summary>Raw damage before mitigation.</summary>
        public readonly int RawDamage;

        /// <summary>Amount absorbed by shields.</summary>
        public readonly int ShieldAbsorbed;

        /// <summary>Amount mitigated by armor/resistance.</summary>
        public readonly int DamageMitigated;

        /// <summary>Whether this damage resulted in a kill.</summary>
        public readonly bool IsLethal;

        public DamagePayload(
            int finalDamage,
            DamageType type,
            UnitUid sourceUnit,
            UnitUid targetUnit,
            int abilityId,
            int sequenceInTick,
            bool isCritical,
            int rawDamage,
            int shieldAbsorbed,
            int damageMitigated,
            bool isLethal)
        {
            FinalDamage = finalDamage;
            Type = type;
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            AbilityId = abilityId;
            SequenceInTick = sequenceInTick;
            IsCritical = isCritical;
            RawDamage = rawDamage;
            ShieldAbsorbed = shieldAbsorbed;
            DamageMitigated = damageMitigated;
            IsLethal = isLethal;
        }

        public static readonly DamagePayload None = default;

        public bool IsValid => FinalDamage > 0 && SourceUnit.IsValid() && TargetUnit.IsValid();
    }
}
