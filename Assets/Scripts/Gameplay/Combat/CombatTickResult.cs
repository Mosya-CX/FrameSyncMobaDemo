namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic per-Tick output produced by CombatSystem after settlement
    /// (Combat v13.2 section 1.5).
    ///
    /// Consumed by MatchStatisticsRuntime and CombatGoldIncomeProducer.
    /// Not stored as cross-Tick state; re-generated on rollback/replay.
    /// </summary>
    public struct CombatTickResult
    {
        public int LogicTick;
        public DeathResult[] Deaths;

        public static readonly CombatTickResult None = new CombatTickResult
        {
            Deaths = System.Array.Empty<DeathResult>(),
        };
    }

    /// <summary>
    /// Formal death result produced when a unit is confirmed dead
    /// (Combat v13.2 section 11).
    /// </summary>
    public struct DeathResult
    {
        public UnitUid VictimUid;
        public UnitUid KillerHeroUid;
        public UnitUid[] AssistantHeroUids;
        public ushort DeathSequenceInTick;
        public int DeathLogicTick;
    }

    /// <summary>
    /// Stable Gold allocation derived from formal death results by
    /// MatchStatisticsRuntime, then consumed by GoldIncomeRuntime.
    /// </summary>
    public struct GoldAllocation
    {
        public UnitUid ReceiverHeroUid;
        public int GoldAmount;
        public ushort DeathSequenceInTick;
    }
}
