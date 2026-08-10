namespace FrameSyncMoba.Unit
{
    public struct CombatSnapshot
    {
        public CombatContributionEventLogSnapshot[] ContributionEventLogs;
        public DeferredCombatRequest[] DeferredRequests;

        public static readonly CombatSnapshot Default = new CombatSnapshot
        {
            ContributionEventLogs = System.Array.Empty<CombatContributionEventLogSnapshot>(),
            DeferredRequests = System.Array.Empty<DeferredCombatRequest>(),
        };
    }
}
