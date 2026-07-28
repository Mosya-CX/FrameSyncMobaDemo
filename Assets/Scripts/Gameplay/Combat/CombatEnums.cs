namespace FrameSyncMoba.Unit
{
    [System.Flags]
    public enum SourceTypeMask : byte
    {
        None = 0,
        Attack = 1 << 0,
        Ability = 1 << 1,
        Buff = 1 << 2,
        Equipment = 1 << 3,
        AttackEffect = 1 << 4,
        System = 1 << 5,
        All = Attack | Ability | Buff |
              Equipment | AttackEffect | System,
    }

    [System.Flags]
    public enum DamageTypeMask : byte
    {
        None = 0,
        Physical = 1 << 0,
        Magic = 1 << 1,
        True = 1 << 2,
        All = Physical | Magic | True,
    }

    /// <summary>
    /// Top-level category of a combat action (Combat v13.2 section 1.1).
    /// </summary>
    public enum CombatDomain : byte
    {
        Damage = 0,
        Heal = 1,
        Shield = 2,
    }

    /// <summary>
    /// Whether a combat modifier applies to outgoing or incoming actions
    /// (Combat v13.2 section 2.3).
    /// </summary>
    public enum CombatModifierScope : byte
    {
        Outgoing = 0,
        Incoming = 1,
    }

    /// <summary>
    /// Named slot in the combat formula pipeline where a patch can be inserted
    /// (Combat v13.2 section 2.4).
    /// </summary>
    public enum CombatFormulaSlot : byte
    {
        CoreValue = 0,
        PreDefenseValue = 1,
        DefenseInput = 2,
        PostDefenseValue = 3,
        FinalValue = 4,
        DerivedValue = 5,
    }

    /// <summary>
    /// Arithmetic operation applied to a formula slot value
    /// (Combat v13.2 section 2.4).
    /// </summary>
    public enum CombatModifierOperation : byte
    {
        Add = 0,
        Multiply = 1,
        ClampMin = 2,
        ClampMax = 3,
    }

    /// <summary>
    /// Source kind for a value reference used in combat formulas
    /// (Combat v13.2 section 2.5).
    /// </summary>
    public enum CombatValueRefKind : byte
    {
        BaseValue = 0,
        CurrentSlotValue = 1,
        SourceStat = 2,
        TargetStat = 3,
    }

    public enum CombatPolicyKind : byte
    {
        ForceCrit = 0,
        ForbidCrit = 1,
        IgnoreAllShield = 2,
        IgnorePhysicalShield = 3,
        IgnoreMagicShield = 4,
    }
}
