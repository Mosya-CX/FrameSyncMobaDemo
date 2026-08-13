using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies ability skill-point allocation rules (design v15.2 1.12):
    /// slot max ranks, per-rank unit-level requirements, the ultimate gate
    /// (level 6 to learn rank 1, max 3 ranks), and the IsUltimate flag.
    /// </summary>
    [TestFixture]
    public sealed class SkillPointAllocationTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;

        [SetUp]
        public void SetUp()
        {
            controller =
                new SimulationTickContextController();
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
            UnitTestFactory
                .DestroyCreatedObjects();
        }

        private static AbilityRuntime MakeRuntime(
            int abilityId,
            bool ultimate = false) =>
            new AbilityRuntime
            {
                Definition = new AbilityDef
                {
                    AbilityId = abilityId,
                    IsUltimate = ultimate,
                },
            };

        private UnitType SpawnHero(
            ushort initialLevel = 1)
        {
            var preset =
                UnitTestFactory
                    .CreateDefaultPreset();
            preset.LevelExperience =
                new LevelExperienceConfig
                {
                    CanLevelUp = true,
                    InitialLevel = initialLevel,
                    MaxLevel = 18,
                    RequiredExperiencePerLevel =
                        new List<int>
                        {
                            100,
                            150,
                            200,
                            250,
                            300,
                        },
                };
            var prototype =
                new UnitPrototype
                {
                    UnitPrototypeId = 100,
            Name = "SkillVarus",
                    RuntimeEntityPrefabId = 101,
                    UnitKind = UnitKind.Hero,
                    UnitSubKindId = 0,
                    BaseStats = preset,
                    BaseGoldValue = 300,
                    BaseExperienceValue = 100,
                };
            return UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                1,
                fp.zero,
                fp.zero);
        }

        [Test]
        public void NormalSlot_AllocatesUpToMaxAndStops()
        {
            // Level 3 satisfies both rank requirements [1, 3].
            UnitType hero = SpawnHero(3);
            var handler = hero.AbilityHandler;
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 0,
                MaxAllocatedPoints = 2,
                RequiredUnitLevelByRank =
                    new[] { 1, 3 },
                ActiveAbilityId = 11,
            };
            slot.AddAbility(
                MakeRuntime(11));
            handler.AddSlot(slot);
            handler.GrantSkillPoint();
            handler.GrantSkillPoint();

            Assert.IsTrue(
                handler.CanAllocateSkillPoint(0));
            Assert.IsTrue(
                handler.TryAllocateSkillPoint(0));
            Assert.AreEqual(1, handler.GetAbilityLevel(0));
            Assert.IsTrue(
                handler.TryAllocateSkillPoint(0));
            Assert.AreEqual(2, handler.GetAbilityLevel(0));
            Assert.IsFalse(
                handler.CanAllocateSkillPoint(0));
            Assert.IsFalse(
                handler.TryAllocateSkillPoint(0));
            Assert.AreEqual(2, handler.GetAbilityLevel(0));
        }

        [Test]
        public void Ultimate_RequiresLevelSixAndMaxThreeRanks()
        {
            UnitType hero = SpawnHero(1);
            var handler = hero.AbilityHandler;
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 1,
                MaxAllocatedPoints = 3,
                RequiredUnitLevelByRank =
                    new[] { 6, 11, 16 },
                ActiveAbilityId = 14,
            };
            slot.AddAbility(
                MakeRuntime(14, ultimate: true));
            handler.AddSlot(slot);
            handler.GrantSkillPoint();

            Assert.IsTrue(
                handler.IsUltimateSlot(1));
            // Hero level 1 cannot learn the ultimate (needs level 6).
            Assert.IsFalse(
                handler.CanAllocateSkillPoint(1));
            Assert.IsFalse(
                handler.TryAllocateSkillPoint(1));
            Assert.AreEqual(0, handler.GetAbilityLevel(1));
        }

        [Test]
        public void Ultimate_UnlocksAtLevelSix()
        {
            UnitType hero = SpawnHero(6);
            var handler = hero.AbilityHandler;
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 1,
                MaxAllocatedPoints = 3,
                RequiredUnitLevelByRank =
                    new[] { 6, 11, 16 },
                ActiveAbilityId = 14,
            };
            slot.AddAbility(
                MakeRuntime(14, ultimate: true));
            handler.AddSlot(slot);
            handler.GrantSkillPoint();

            Assert.AreEqual(6, hero.StatHandler.Level);
            Assert.IsTrue(
                handler.CanAllocateSkillPoint(1));
            Assert.IsTrue(
                handler.TryAllocateSkillPoint(1));
            Assert.AreEqual(1, handler.GetAbilityLevel(1));
        }

        [Test]
        public void
            GrantExperience_GrantsExactlyOneSkillPointPerLevel()
        {
            // Regression: skill points used to be issued twice per level-up
            // (once inside StatHandler.AddExperience and again inside
            // UnitWorld.GrantExperience), so a hero reached level 2 with an
            // extra pending point.
            UnitType hero = SpawnHero(1);
            var handler = hero.AbilityHandler;
            byte before = handler.PendingSkillPoints;

            // RequiredExperiencePerLevel[0] == 100 -> exactly one level-up.
            world.GrantExperience(
                hero.UnitUid,
                100);

            Assert.AreEqual(
                before + 1,
                handler.PendingSkillPoints);
            Assert.AreEqual(
                2,
                hero.StatHandler.Level);
        }
    }
}
