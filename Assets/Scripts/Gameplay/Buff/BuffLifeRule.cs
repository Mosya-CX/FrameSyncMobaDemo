namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Controls how a Buff's lifetime advances across Ticks
    /// (BuffSystem v14.2 section 1.4).
    /// </summary>
    public enum BuffLifeRule
    {
        /// <summary>Buff has a fixed duration; removed when RemainingTicks reaches 0.</summary>
        Duration,

        /// <summary>Buff persists indefinitely; only removed by explicit Remove, stack exhaustion, or death.</summary>
        Infinite,
    }
}
