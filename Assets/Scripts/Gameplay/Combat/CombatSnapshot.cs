namespace FrameSyncMoba.Unit
{
    public struct CombatSnapshot
    {
        public DamageContributionTrackerSnapshot[] ContributionTrackers;
        public DeferredCombatRequest[] DeferredRequests;

        public static readonly CombatSnapshot Default = new CombatSnapshot
        {
            ContributionTrackers = System.Array.Empty<DamageContributionTrackerSnapshot>(),
            DeferredRequests = System.Array.Empty<DeferredCombatRequest>(),
        };
    }

    public struct DamageContributionTrackerSnapshot
    {
        public UnitUid VictimUnitUid;
        public DamageContributionRecordSnapshot[] Records;
    }

    public struct DamageContributionRecordSnapshot
    {
        public UnitUid ContributorHeroUid;
        public int LastContributionLogicTick;
        public Unity.Mathematics.FixedPoint.fp ContributionValue;
        public int ExpireLogicTick;
    }
}
