using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// One stat's base and growth values for a unit prototype (Unit v27.3 section 5.2.3).
    /// </summary>
    [Serializable]
    public struct StatPresetEntry
    {
        public StatId StatId;

        /// <summary>Level-1 base value.</summary>
        public fp BaseValue;

        /// <summary>Per-level growth value; 0 when no growth.</summary>
        public fp GrowthValue;
    }
}