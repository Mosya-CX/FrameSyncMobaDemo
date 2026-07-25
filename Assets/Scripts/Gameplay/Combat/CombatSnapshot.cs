using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public struct CombatSnapshot
    {
        public System.Collections.Generic.List<DamageContributionTrackerSnapshot> ContributionTrackers;
        public System.Collections.Generic.List<DeferredCombatRequest> DeferredRequests;

        public static readonly CombatSnapshot Default = new CombatSnapshot
        {
            ContributionTrackers = new System.Collections.Generic.List<DamageContributionTrackerSnapshot>(),
            DeferredRequests = new System.Collections.Generic.List<DeferredCombatRequest>(),
        };
    }

    public struct DamageContributionTrackerSnapshot
    {
        public UnitUid VictimUnitUid;
        public System.Collections.Generic.List<DamageContributionRecordSnapshot> Records;
    }

    public struct DamageContributionRecordSnapshot
    {
        public UnitUid ContributorHeroUid;
        public int LastContributionLogicTick;
        public Unity.Mathematics.FixedPoint.fp ContributionValue;
        public int ExpireLogicTick;
    }
}
