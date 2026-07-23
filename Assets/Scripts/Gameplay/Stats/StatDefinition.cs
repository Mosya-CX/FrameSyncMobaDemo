using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global definition for one StatId (Unit v27.3 section 5.2.2).
    /// Describes boundaries and default values; per-unit values live in StatPreset.
    /// </summary>
    [Serializable]
    public sealed class StatDefinition
    {
        public StatId Id;
        public string DebugName;

        public fp DefaultBaseValue;
        public bool SupportsLevelGrowth;

        public bool HasMinValue;
        public fp MinValue;

        public bool HasMaxValue;
        public fp MaxValue;
    }
}