namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Bitmask filter for Unit.LifeState in RangeQueryService
    /// (Physics v13.1 section 9.5).
    /// </summary>
    public struct UnitLifeStateMask
    {
        private byte mask;

        public static readonly UnitLifeStateMask None = default;
        public static readonly UnitLifeStateMask AliveOnly = new UnitLifeStateMask { mask = 1 << 0 };
        public static readonly UnitLifeStateMask DyingOnly = new UnitLifeStateMask { mask = 1 << 1 };
        public static readonly UnitLifeStateMask DeadOnly = new UnitLifeStateMask { mask = 1 << 2 };
        public static readonly UnitLifeStateMask RespawningOnly = new UnitLifeStateMask { mask = 1 << 3 };
        public static readonly UnitLifeStateMask All = new UnitLifeStateMask { mask = 0xFF };

        public bool Contains(LifeState state)
        {
            int bit = (int)state;
            return bit >= 0 && bit < 8 && (mask & (1 << bit)) != 0;
        }

        public UnitLifeStateMask With(LifeState state)
        {
            int bit = (int)state;
            if (bit >= 0 && bit < 8)
            {
                mask |= (byte)(1 << bit);
            }
            return this;
        }
    }
}
