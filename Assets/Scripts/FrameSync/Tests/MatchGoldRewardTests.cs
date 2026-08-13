using System;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class MatchGoldRewardTests
    {
        private const int InitialGold = 1500;

        [Test]
        public void GoldAllocation_UsesIntegerAmountContract()
        {
            Assert.That(
                typeof(GoldAllocation).GetField(
                    nameof(GoldAllocation.GoldAmount))
                    ?.FieldType,
                Is.EqualTo(typeof(int)));
        }

        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [TestCase(21)]
        [TestCase(14)]
        public void MinionLastHit_ConfirmsFullConfiguredGold(
            int baseGold)
        {
            var world = new UnitWorld();
            UnitType killer = SpawnHero(
                world,
                801,
                new TeamId(1),
                playerSlot: 0);
            UnitType minion = Spawn(
                world,
                802,
                UnitKind.Minion,
                new TeamId(2),
                baseGold);
            var statistics = new MatchStatisticsRuntime();

            statistics.Consume(
                new[]
                {
                    Death(
                        minion,
                        killer.UnitUid,
                        Array.Empty<UnitUid>()),
                },
                world);

            Assert.That(
                statistics.GoldAllocations.Count,
                Is.EqualTo(1));
            Assert.That(
                statistics.GoldAllocations[0]
                    .ReceiverHeroUid,
                Is.EqualTo(killer.UnitUid));
            Assert.That(
                statistics.GoldAllocations[0]
                    .GoldAmount,
                Is.EqualTo(baseGold));

            GoldIncomeRuntime income = ConfirmAllocations(
                world,
                statistics,
                maxPlayers: 1);
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(0),
                Is.EqualTo(InitialGold + baseGold));
        }

        [Test]
        public void HeroKill_WithTwoAssistants_Splits300As180_60_60()
        {
            var world = new UnitWorld();
            UnitType victim = SpawnHero(
                world,
                811,
                new TeamId(2),
                playerSlot: 3,
                baseGold: 300);
            UnitType killer = SpawnHero(
                world,
                812,
                new TeamId(1),
                playerSlot: 0);
            UnitType assistantA = SpawnHero(
                world,
                813,
                new TeamId(1),
                playerSlot: 1);
            UnitType assistantB = SpawnHero(
                world,
                814,
                new TeamId(1),
                playerSlot: 2);
            var statistics = new MatchStatisticsRuntime();

            statistics.Consume(
                new[]
                {
                    Death(
                        victim,
                        killer.UnitUid,
                        new[]
                        {
                            assistantA.UnitUid,
                            assistantB.UnitUid,
                        }),
                },
                world);

            Assert.That(
                MatchStatisticsRuntime.HeroKillerShareNumerator,
                Is.EqualTo(3));
            Assert.That(
                MatchStatisticsRuntime.HeroKillerShareDenominator,
                Is.EqualTo(5));
            AssertAllocation(
                statistics,
                killer.UnitUid,
                180);
            AssertAllocation(
                statistics,
                assistantA.UnitUid,
                60);
            AssertAllocation(
                statistics,
                assistantB.UnitUid,
                60);
            int allocatedTotal = 0;
            for (int i = 0;
                 i < statistics.GoldAllocations.Count;
                 i++)
            {
                allocatedTotal +=
                    statistics.GoldAllocations[i]
                        .GoldAmount;
            }
            Assert.That(allocatedTotal, Is.EqualTo(300));

            GoldIncomeRuntime income = ConfirmAllocations(
                world,
                statistics,
                maxPlayers: 4);
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(0),
                Is.EqualTo(1680));
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(1),
                Is.EqualTo(1560));
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(2),
                Is.EqualTo(1560));
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(3),
                Is.EqualTo(InitialGold));
        }

        [Test]
        public void HeroKill_WithoutAssistants_ConfirmsFull300ToKiller()
        {
            var world = new UnitWorld();
            UnitType victim = SpawnHero(
                world,
                821,
                new TeamId(2),
                playerSlot: 1,
                baseGold: 300);
            UnitType killer = SpawnHero(
                world,
                822,
                new TeamId(1),
                playerSlot: 0);
            var statistics = new MatchStatisticsRuntime();

            statistics.Consume(
                new[]
                {
                    Death(
                        victim,
                        killer.UnitUid,
                        Array.Empty<UnitUid>()),
                },
                world);

            AssertAllocation(
                statistics,
                killer.UnitUid,
                300);
            GoldIncomeRuntime income = ConfirmAllocations(
                world,
                statistics,
                maxPlayers: 2);
            Assert.That(
                income.GetConfirmedEarnedGoldTotal(0),
                Is.EqualTo(1800));
        }

        private static GoldIncomeRuntime ConfirmAllocations(
            UnitWorld world,
            MatchStatisticsRuntime statistics,
            int maxPlayers)
        {
            var income = new GoldIncomeRuntime();
            income.Initialize(maxPlayers, InitialGold);
            income.BeginTick(0);
            for (int i = 0;
                 i < statistics.GoldAllocations.Count;
                 i++)
            {
                GoldAllocation allocation =
                    statistics.GoldAllocations[i];
                Assert.IsTrue(
                    world.TryGetUnit(
                        allocation.ReceiverHeroUid,
                        out UnitType receiver));
                income.RequestGoldIncome(
                    receiver.ControlledByPlayerSlot,
                    allocation.GoldAmount,
                    GoldIncomeReason.UnitKill);
            }
            income.SealTick(0);
            income.ConfirmAcceptedTick(0);
            return income;
        }

        private static void AssertAllocation(
            MatchStatisticsRuntime statistics,
            UnitUid receiver,
            int expectedAmount)
        {
            int matches = 0;
            for (int i = 0;
                 i < statistics.GoldAllocations.Count;
                 i++)
            {
                GoldAllocation allocation =
                    statistics.GoldAllocations[i];
                if (allocation.ReceiverHeroUid != receiver)
                    continue;
                matches++;
                Assert.That(
                    allocation.GoldAmount,
                    Is.EqualTo(expectedAmount));
            }
            Assert.That(matches, Is.EqualTo(1));
        }

        private static DeathResult Death(
            UnitType victim,
            UnitUid killer,
            UnitUid[] assistants)
        {
            return new DeathResult
            {
                VictimUid = victim.UnitUid,
                KillerHeroUid = killer,
                AssistantHeroUids = assistants,
                DeathSequenceInTick = 1,
                DeathLogicTick = 0,
            };
        }

        private static UnitType SpawnHero(
            UnitWorld world,
            int id,
            TeamId team,
            int playerSlot,
            int baseGold = 0)
        {
            UnitType hero = Spawn(
                world,
                id,
                UnitKind.Hero,
                team,
                baseGold);
            hero.ControlledByPlayerSlot = playerSlot;
            return hero;
        }

        private static UnitType Spawn(
            UnitWorld world,
            int id,
            UnitKind kind,
            TeamId team,
            int baseGold)
        {
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = id,
                RuntimeEntityPrefabId = id,
                UnitKind = kind,
                Loadout = HandlerLoadout.DefaultHero,
                BaseStats =
                    UnitTestFactory.CreateDefaultPreset(),
                BaseGoldValue = baseGold,
            };
            return world.SpawnUnit(
                prototype,
                team,
                currentLogicTick: 0,
                statGrowthC: fp.zero,
                statGrowthD: fp.zero);
        }
    }
}
