namespace FrameSyncMoba.Unit
{
    public struct DeferredCombatRequestBuffer
    {
        public DeferredCombatRequest[] Records;
        public static readonly DeferredCombatRequestBuffer Empty = new DeferredCombatRequestBuffer
        {
            Records = System.Array.Empty<DeferredCombatRequest>(),
        };
        public bool IsEmpty => Records == null || Records.Length == 0;
    }

    public struct PendingDyingRecord
    {
        public UnitUid UnitUid;
        public int PendingLogicTick;
        public static readonly PendingDyingRecord Invalid = default;
        public bool IsValid => UnitUid.IsValid();
    }
}
