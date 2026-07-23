using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Frame-to-frame stat change query result (Unit v27.3 section 5.5.2).
    /// </summary>
    public readonly struct StatChange
    {
        public readonly bool Changed;
        public readonly fp Delta;

        public StatChange(bool changed, fp delta)
        {
            Changed = changed;
            Delta = delta;
        }
    }
}