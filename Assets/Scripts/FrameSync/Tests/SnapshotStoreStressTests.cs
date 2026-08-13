using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    /// <summary>
    /// Stress-tests the rollback SnapshotStore ring mapping: after long
    /// prediction runs, base advances and repeated rollback/replay cycles,
    /// every stored key must still map back to the snapshot captured for
    /// exactly that Tick (SnapshotTick == tick + 1).
    /// </summary>
    [TestFixture]
    public sealed class SnapshotStoreStressTests
    {
        [Test]
        public void RingMapping_SurvivesAdvancesDiscardsAndRestores()
        {
            var store = new SnapshotStore(512);

            // Phase 1: long prediction run.
            for (int t = 0; t < 260; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());

            // Phase 2: authority frames accepted in batches.
            for (int b = 10; b <= 170; b += 10)
                store.AdvanceBase(b);

            // Phase 3: rollback at 171, anchor is key 170.
            store.DiscardFromTick(171);
            Assert.That(
                store.TryGet(170, out var anchor),
                Is.True);
            Assert.That(anchor.SnapshotTick, Is.EqualTo(171));

            // Phase 4: replay 171..175, accept 171, rollback at 174.
            for (int t = 171; t <= 175; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());
            store.AdvanceBase(172);
            store.DiscardFromTick(174);
            Assert.That(
                store.TryGet(173, out var a173),
                Is.True);
            Assert.That(a173.SnapshotTick, Is.EqualTo(174));

            // Phase 5: replay forward and verify every key after accept.
            for (int t = 174; t <= 200; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());
            store.AdvanceBase(180);
            for (int t = 180; t <= 200; t++)
            {
                Assert.That(
                    store.TryGet(t, out var snapshot),
                    Is.True,
                    $"missing key {t}");
                Assert.That(
                    snapshot.SnapshotTick,
                    Is.EqualTo(t + 1),
                    $"mapping mismatch at key {t}");
            }
        }

        [Test]
        public void RingMapping_RepeatedRollbacks_NeverReturnStaleSlots()
        {
            var store = new SnapshotStore(512);
            int tick = 0;
            for (int round = 0; round < 40; round++)
            {
                int run = 8 + round % 6;
                for (int i = 0; i < run; i++)
                    store.Store(tick++, GameplaySnapshot.CreateEmpty());

                // Accept a few, then roll back two ticks and re-store.
                int accept = 3 + round % 4;
                if (accept < tick)
                    store.AdvanceBase(accept);
                store.DiscardFromTick(tick - 1);
                for (int i = 0; i < 2; i++)
                    store.Store(tick++, GameplaySnapshot.CreateEmpty());
            }

            // Verify the most recent stored key maps exactly.
            int last = tick - 1;
            Assert.That(
                store.TryGet(last, out var recent),
                Is.True,
                $"missing latest key {last}");
            Assert.That(
                recent.SnapshotTick,
                Is.EqualTo(last + 1));
            Assert.That(
                store.TryGet(last - 1, out var previous),
                Is.True,
                $"missing key {last - 1}");
            Assert.That(
                previous.SnapshotTick,
                Is.EqualTo(last));
        }
    }
}
