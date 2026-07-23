namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Damage type classification (Combat v13.2 section 7).
    /// Physical: reduced by Armor.
    /// Magic: reduced by MagicResistance.
    /// True: bypasses all resistance.
    /// </summary>
    public enum DamageType : byte
    {
        Physical = 0,
        Magic = 1,
        True = 2,
    }
}
