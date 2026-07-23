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

        public bool IsValid => ConfigId.IsValid;
    }
}
