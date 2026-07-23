namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Handle returned by StatHandler.AddModifier (Unit v27.3 section 5.4.3).
    /// Locates a modifier by (StatId, StatSeq) and prevents stale-pool misuse
    /// via OwnerUnitUid.
    /// </summary>
    public readonly struct StatModifierHandle
    {
        public readonly UnitUid OwnerUnitUid;
        public readonly StatId StatId;
        public readonly uint StatSeq;

        public StatModifierHandle(UnitUid ownerUnitUid, StatId statId, uint statSeq)
        {
            OwnerUnitUid = ownerUnitUid;
            StatId = statId;
            StatSeq = statSeq;
        }

        public bool IsValid => StatSeq != 0;
    }
}