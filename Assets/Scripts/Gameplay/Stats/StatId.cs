namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable attribute identity (Unit v27.3 section 5.2.1).
    /// </summary>
    public enum StatId : ushort
    {
        MaxHealth = 0,
        HealthRegeneration = 2,

        MaxCastResource = 3,
        CastResourceRegeneration = 4,

        AttackDamage = 5,
        AbilityPower = 6,

        Armor = 7,
        MagicResistance = 8,

        AttackSpeed = 9,
        AttackRange = 10,
        MoveSpeed = 11,
        CastRangeBonus = 12,
        CooldownReduction = 13,

        CriticalStrikeChance = 14,
        CriticalStrikeDamage = 15,

        ArmorPenetrationRatio = 16,
        FlatArmorPenetration = 17,
        MagicPenetrationRatio = 18,
        FlatMagicPenetration = 19,

        LifeSteal = 20,
        Omnivamp = 21,
        HealPower = 22,
        ShieldPower = 23,
        Tenacity = 24,
        HealingReceivedRatio = 25,
    }
}
