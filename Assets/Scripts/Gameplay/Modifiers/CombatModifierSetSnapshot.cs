using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Serializable snapshot of CombatModifierSet cross-Tick state
    /// (Unit v27.3 ��5.9.4). Captures all immutable CombatModifierRecords.
    /// Restore directly replaces �� does not call Attach/Detach/Clear.
    /// </summary>
    public struct CombatModifierSetSnapshot
    {
        /// <summary>
        /// All modifier Ids in strictly increasing canonical order.
        /// </summary>
        public ulong[] Ids;

        /// <summary>
        /// Deep copies of all attached CombatModifierRecords.
        /// Records are duplicated by value (they are immutable after Attach).
        /// </summary>
        public CombatModifierRecord[] Records;
    }
}
