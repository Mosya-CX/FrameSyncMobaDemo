using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic CombatModifierRecord.Id generator (Unit v27.3 §1.10).
    /// Id = (CreationLogicTick &lt;&lt; 32) | DeterministicHash32(modifierKey).
    /// </summary>
    public static class CombatModifierId
    {
        /// <summary>
        /// Creates a deterministic ulong ModifierId from the current LogicTick
        /// and a stable modifier key string.
        /// </summary>
        public static ulong Create(int currentLogicTick, string modifierKey)
        {
            uint keyHash = DeterministicHash32.Utf8(modifierKey);
            return ((ulong)(uint)currentLogicTick << 32) | keyHash;
        }
    }
}