using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the per-victim combat event log (Combat v13.2 §7.14):
    /// event recording for damage/shield/heal, window pruning and capacity,
    /// last-hit killer resolution, stable assistant resolution, and snapshot
    /// round trips.
    /// </summary>
    [TestFixture]
    public sealed class CombatContributionEventLogTests
    {
        [Test]
        public void LastHit_IsMostRecentDamageEventContributor()
        {
            var victim = new UnitUid(1, 1, 0);
            var first = new UnitUid(2, 10, 0);
            var second = new UnitUid(2, 10, 1);

            var log = new CombatContributionEventLog(victim);
            log.AddEvent(CreateEvent(victim, first, CombatContributionKind.Damage, (fp)90, 3, 1));
            Assert.AreEqual(first, log.LastHitContributorUid);
            log.AddEvent(CreateEvent(victim, second, CombatContributionKind.Damage, (fp)10, 4, 1));
            Assert.AreEqual(second, log.LastHitContributorUid);
            // Shield/Heal events must not change the killer.
            log.AddEvent(CreateEvent(victim, first, CombatContributionKind.Shield, (fp)50, 4, 2));
            log.AddEvent(CreateEvent(victim, first, CombatContributionKind.Heal, (fp)50, 4, 3));
            Assert.AreEqual(second, log.LastHitContributorUid);
        }

        [Test]
        public void PruneExpired_RemovesOldEventsAndClearsLastHitWhenEmpty()
        {
            var victim = new UnitUid(1, 1, 0);
            var contributor = new UnitUid(2, 10, 0);

            var log = new CombatContributionEventLog(victim);
            log.AddEvent(CreateEvent(victim, contributor, CombatContributionKind.Damage, (fp)10, 10, 1));
            log.PruneExpired(10 + CombatContributionEventLog.AssistContributionDurationTicks);
            Assert.AreEqual(1, log.EventCount);
            log.PruneExpired(11 + CombatContributionEventLog.AssistContributionDurationTicks);
            Assert.AreEqual(0, log.EventCount);
            Assert.IsFalse(
                log.LastHitContributorUid.IsValid());
        }

        [Test]
        public void Capacity_DropsOldestEventFirst()
        {
            var victim = new UnitUid(1, 1, 0);
            var a = new UnitUid(2, 10, 0);
            var b = new UnitUid(2, 10, 1);

            var log = new CombatContributionEventLog(victim);
            // Fill beyond capacity: capacity is 256, add 257 events.
            for (int i = 0; i < CombatContributionEventLog.MaxContributionEventsPerVictim + 1; i++)
            {
                log.AddEvent(CreateEvent(
                    victim,
                    i % 2 == 0 ? a : b,
                    CombatContributionKind.Damage,
                    (fp)1,
                    100 + i / 100,
                    (ushort)(i % 100)));
            }
            Assert.AreEqual(
                CombatContributionEventLog.MaxContributionEventsPerVictim,
                log.EventCount);
            // The first (oldest) event must have been dropped.
            Assert.AreEqual(100, log.Events[0].LogicTick);
        }

        [Test]
        public void ResolveAssistants_ExcludesKillerAndSortsAscending()
        {
            UnitWorld world = CreateWorld();
            UnitType victim = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(),
                new TeamId(2),
                5,
                fp.zero,
                fp.zero);
            UnitType killer = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(),
                new TeamId(1),
                5,
                fp.zero,
                fp.zero);
            UnitType assistantA = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(),
                new TeamId(1),
                5,
                fp.zero,
                fp.zero);
            UnitType assistantB = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(),
                new TeamId(1),
                5,
                fp.zero,
                fp.zero);

            var log = new CombatContributionEventLog(
                victim.UnitUid);
            log.AddEvent(CreateEvent(
                victim.UnitUid,
                assistantB.UnitUid,
                CombatContributionKind.Damage,
                (fp)10,
                5,
                1));
            log.AddEvent(CreateEvent(
                victim.UnitUid,
                assistantA.UnitUid,
                CombatContributionKind.Damage,
                (fp)20,
                5,
                2));
            log.AddEvent(CreateEvent(
                victim.UnitUid,
                killer.UnitUid,
                CombatContributionKind.Damage,
                (fp)30,
                5,
                3));

            UnitUid killerUid =
                log.ResolveLastDamageContributor(5);
            Assert.AreEqual(
                killer.UnitUid,
                killerUid);
            UnitUid[] assistants = log.ResolveAssistants(
                5,
                world,
                victim,
                killerUid);
            CollectionAssert.AreEqual(
                new[]
                {
                    assistantA.UnitUid,
                    assistantB.UnitUid,
                },
                assistants);
        }

        [Test]
        public void SnapshotRoundTrip_PreservesEventsAndLastHit()
        {
            var victim = new UnitUid(1, 1, 0);
            var contributor = new UnitUid(2, 10, 0);

            var log = new CombatContributionEventLog(victim);
            log.AddEvent(CreateEvent(
                victim,
                contributor,
                CombatContributionKind.Damage,
                (fp)25,
                7,
                2));
            log.AddEvent(CreateEvent(
                victim,
                contributor,
                CombatContributionKind.Heal,
                (fp)10,
                8,
                1));

            CombatContributionEventLogSnapshot snapshot =
                log.Capture();
            var restored =
                new CombatContributionEventLog(victim);
            restored.Restore(snapshot);

            Assert.AreEqual(
                contributor,
                restored.LastHitContributorUid);
            Assert.AreEqual(2, restored.EventCount);
            Assert.AreEqual(
                CombatContributionKind.Damage,
                restored.Events[0].Kind);
            Assert.AreEqual(7, restored.Events[0].LogicTick);
            Assert.AreEqual(
                CombatContributionKind.Heal,
                restored.Events[1].Kind);
        }

        private static CombatContributionEvent CreateEvent(
            UnitUid victim,
            UnitUid contributor,
            CombatContributionKind kind,
            fp amount,
            int tick,
            ushort sequence)
        {
            return new CombatContributionEvent
            {
                VictimUnitUid = victim,
                ContributorHeroUid = contributor,
                Kind = kind,
                Amount = amount,
                LogicTick = tick,
                SequenceInTick = sequence,
            };
        }

        private static UnitWorld CreateWorld()
        {
            var world = new UnitWorld();
            return world;
        }

        private static UnitPrototype CreatePrototype()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)1000,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                RuntimeEntityPrefabId = 1001,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}
