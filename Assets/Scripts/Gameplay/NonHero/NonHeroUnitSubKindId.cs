namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable UnitSubKindId values used by generic non-hero targeting rules.
    /// UnitKind remains the broad classification authority.
    /// </summary>
    public static class NonHeroUnitSubKindId
    {
        public const ushort Unspecified = 0;
        public const ushort MeleeMinion = 1;
        public const ushort RangedMinion = 2;
        public const ushort SiegeMinion = 3;
        public const ushort SuperMinion = 4;
    }
}
