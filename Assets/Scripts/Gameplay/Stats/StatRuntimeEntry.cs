using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime state for one stat (Unit v27.3 section 5.2.4).
    /// Part of StatHandler's formal snapshot state.
    /// </summary>
    internal sealed class StatRuntimeEntry
    {
        public fp LevelBaseValue;
        public fp FinalValue;
        public fp PreviousLogicTickFinalValue;
        public bool Dirty;
        public List<StatModifier> Modifiers = new List<StatModifier>();
    }
}