using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffDef
    {
        public BuffConfigId ConfigId;
        public BuffLifeRule LifeRule = BuffLifeRule.Duration;
        public BuffStackRule StackRule = BuffStackRule.RefreshDuration;
        public int DurationTicks;
        public int MaxStacks = 1;
        public int InitialStacks = 1;
        public int PeriodicIntervalTicks;
        public BuffEffect[] Effects;

        /// <summary>Dispel priority (0 = highest, 255 = lowest). Used when MaxBuffs is reached.</summary>
        public byte Priority;

        /// <summary>Optional tag for mass dispel (RemoveBuffsByTag). 0 = no tag.</summary>
        public byte Tag;

        public bool IsValid => ConfigId.IsValid;
    }
}
