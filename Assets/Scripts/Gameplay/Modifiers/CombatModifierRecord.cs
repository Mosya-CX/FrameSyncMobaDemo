namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Immutable combat modifier record attached to a Unit's CombatModifierSet
    /// (Unit v27.3 §1.10). The Id must be set by the caller before Attach,
    /// using CombatModifierId.Create with the current LogicTick and a stable
    /// modifier key string.
    ///
    /// This slice defines only the Id field. FormulaPatch, PolicyPatch and
    /// CombatModifierMatch will be added when the Combat system design
    /// defines them. Until then, the record holds only its deterministic
    /// identity, which is sufficient for Attach/Detach/Collect/Clear.
    /// </summary>
    public sealed class CombatModifierRecord
    {
        /// <summary>
        /// Deterministic modifier identity. Set before Attach via
        /// CombatModifierId.Create(currentLogicTick, modifierKey).
        /// Immutable after Attach.
        /// </summary>
        public ulong Id;
    }
}