namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Bitmask filter for UnitKind in RangeQueryService
    /// (Physics v13.1 section 9.4).
    /// </summary>
    [System.Serializable]
    public struct UnitKindMask
    {
        [UnityEngine.SerializeField]
        private uint mask;

        public static readonly UnitKindMask None = default;
        public static readonly UnitKindMask Hero = new UnitKindMask { mask = 1u << (int)UnitKind.Hero };
        public static readonly UnitKindMask Minion = new UnitKindMask { mask = 1u << (int)UnitKind.Minion };
        public static readonly UnitKindMask Monster = new UnitKindMask { mask = 1u << (int)UnitKind.Monster };
        public static readonly UnitKindMask Structure = new UnitKindMask { mask = 1u << (int)UnitKind.Structure };
        public static readonly UnitKindMask All = new UnitKindMask { mask = uint.MaxValue };

        public bool IsEmpty => mask == 0u;

        public bool Contains(UnitKind kind)
        {
            int bit = (int)kind;
            return bit >= 0 && bit < 32 && (mask & (1u << bit)) != 0;
        }

        public UnitKindMask With(UnitKind kind)
        {
            int bit = (int)kind;
            if (bit >= 0 && bit < 32)
            {
                mask |= 1u << bit;
            }
            return this;
        }
    }
}
