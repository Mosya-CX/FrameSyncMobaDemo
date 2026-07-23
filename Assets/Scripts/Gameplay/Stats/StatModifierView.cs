using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Read-only view of a modifier (Unit v27.3 section 5.4.5).
    /// </summary>
    public readonly struct StatModifierView
    {
        public readonly StatId StatId;
        public readonly uint StatSeq;
        public readonly StatModifierOperation Operation;
        public readonly fp Value;

        public StatModifierView(
            StatId statId,
            uint statSeq,
            StatModifierOperation operation,
            fp value)
        {
            StatId = statId;
            StatSeq = statSeq;
            Operation = operation;
            Value = value;
        }
    }
}