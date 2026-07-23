using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Internal modifier record created by StatHandler (Unit v27.3 section 5.4.2).
    /// Does not duplicate StatId — it lives in the containing StatRuntimeEntry.
    /// </summary>
    public struct StatModifier
    {
        public uint StatSeq;
        public StatModifierOperation Operation;
        public fp Value;
    }
}
