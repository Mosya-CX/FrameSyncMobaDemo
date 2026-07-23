using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class DamageContributionTrackerTests
    {
        [Test]
        public void EqualContribution_UsesSmallestUnitUidRegardlessOfInsertionOrder()
        {
            var victim = new UnitUid(1, 1, 0);
            var smaller = new UnitUid(2, 10, 0);
            var larger = new UnitUid(2, 10, 1);

            var first = new DamageContributionTracker(victim);
            first.AddContribution(larger, (fp)50, 3);
            first.AddContribution(smaller, (fp)50, 3);

            var second = new DamageContributionTracker(victim);
            second.AddContribution(smaller, (fp)50, 3);
            second.AddContribution(larger, (fp)50, 3);

            Assert.AreEqual(
                smaller,
                first.GetTopContributor().Value.ContributorHeroUid);
            Assert.AreEqual(
                smaller,
                second.GetTopContributor().Value.ContributorHeroUid);
        }

        [Test]
        public void Assistants_AreReturnedInCanonicalUnitUidOrder()
        {
            var tracker = new DamageContributionTracker(new UnitUid(1, 1, 0));
            var smaller = new UnitUid(2, 10, 0);
            var larger = new UnitUid(2, 10, 1);
            var top = new UnitUid(2, 10, 2);
            tracker.AddContribution(larger, (fp)20, 3);
            tracker.AddContribution(smaller, (fp)10, 3);
            tracker.AddContribution(top, (fp)30, 3);

            CollectionAssert.AreEqual(
                new[] { smaller, larger },
                tracker.GetAssistants());
        }

        [Test]
        public void Contribution_ExpiresOnlyAfterItsInclusiveExpireTick()
        {
            var tracker = new DamageContributionTracker(new UnitUid(1, 1, 0));
            tracker.AddContribution(new UnitUid(2, 10, 0), (fp)10, 10);

            tracker.PruneExpired(10 + DamageContributionTracker.ContributionExpiryTicks);
            Assert.AreEqual(1, tracker.RecordCount);
            tracker.PruneExpired(11 + DamageContributionTracker.ContributionExpiryTicks);
            Assert.AreEqual(0, tracker.RecordCount);
        }
    }
}
