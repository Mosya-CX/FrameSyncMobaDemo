namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Handle returned by CombatModifierSet.Attach (Unit v27.3 §1.10).
    /// Only the Runtime that created the attachment holds this handle.
    /// Not used as CombatSystem formula data.
    /// </summary>
    public readonly struct CombatModifierHandle
    {
        public readonly UnitUid OwnerUnitUid;
        public readonly ulong ModifierId;

        public CombatModifierHandle(UnitUid ownerUnitUid, ulong modifierId)
        {
            OwnerUnitUid = ownerUnitUid;
            ModifierId = modifierId;
        }

        public bool IsValid => ModifierId != 0;
    }
}