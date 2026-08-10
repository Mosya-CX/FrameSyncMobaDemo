using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the kill-growth passive (P): killing grants attack speed and
    /// derived attack damage / ability power with a hero-victim multiplier,
    /// refreshes the buff duration, and ability cooldowns resolve per level.
    /// </summary>
    [TestFixture]
    public sealed class PassivePAbilityTests
    {
        private const int RevengeBuffConfigId = 9002;
        private const int FixedPassiveAbilityId = 10010;

        private SimulationTickContextController controller;
        private UnitWorld world;
        private BuffDefinitionRegistry buffDefs;
        private UnitType caster;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(
                40,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
            caster = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)0.658m),
                new TeamId(1),
                40,
                fp.zero,
                fp.zero);
            buffDefs = new BuffDefinitionRegistry();
            world.BuffDefinitions = buffDefs;
            buffDefs.Register(
                CreateRevengeDefinition());
            CombatEvents.TryResolveUnit =
                uid => world.TryGetUnit(
                    uid,
                    out UnitType unit)
                    ? unit
                    : null;

            caster.AbilityHandler.SetFixedPassive(
                CreateFixedPassiveDefinition());
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void KillNonHero_GrantsAttackSpeedAndDerivedStats()
        {
            UnitType minion = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Minion,
                    (fp)0.658m,
                    2),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);

            CombatEvents.RaiseUnitKill(
                caster.UnitUid,
                minion.UnitUid);

            Assert.IsTrue(
                caster.BuffHandler.HasBuff(
                    new BuffConfigId(
                        RevengeBuffConfigId)));
            // 10% attack speed at level 1 -> FinalRatioAdd 0.1 (x1.1).
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.1m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001);
            // attack damage = 100 base + 0.1 * 11 = 1.1 flat.
            Assert.AreEqual(
                (double)((fp)100 + (fp)1.1m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackDamage),
                0.001);
            // ability power = 0 base + 1.1 flat.
            Assert.AreEqual(
                (double)((fp)1.1m),
                (double)caster.StatHandler.GetStat(
                    StatId.AbilityPower),
                0.001);
        }

        [Test]
        public void KillHero_AppliesThreeTimesBonus()
        {
            UnitType heroVictim = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)0.658m,
                    2),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);

            CombatEvents.RaiseUnitKill(
                caster.UnitUid,
                heroVictim.UnitUid);

            // 30% attack speed (10% * 3).
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.3m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001);
            // attack damage = 100 + 0.3 * 11 = 103.3.
            Assert.AreEqual(
                (double)((fp)100 + (fp)3.3m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackDamage),
                0.001);
            Assert.AreEqual(
                (double)((fp)3.3m),
                (double)caster.StatHandler.GetStat(
                    StatId.AbilityPower),
                0.001);
        }

        [Test]
        public void Kill_RefreshesBuffDurationToFiveSeconds()
        {
            UnitType minion = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Minion,
                    (fp)0.658m,
                    2),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);

            CombatEvents.RaiseUnitKill(
                caster.UnitUid,
                minion.UnitUid);

            Assert.IsTrue(caster.BuffHandler.GetBuffInfo(
                new BuffConfigId(RevengeBuffConfigId),
                out BuffInfo info));
            Assert.AreEqual(1, info.StackCount);
            Assert.AreEqual(
                150,
                info.RemainingTicks,
                "Level 1 duration is 5 seconds at 30 ticks/sec.");
        }

        [Test]
        public void Cooldown_ResolvesPerAbilityLevel()
        {
            var definition = new AbilityDef
            {
                AbilityId = 10011,
                CooldownByLevel =
                    new AbilityLevelValue(
                        new[]
                        {
                            (fp)480,
                            (fp)450,
                            (fp)420,
                            (fp)390,
                            (fp)360,
                        }),
            };
            Assert.AreEqual(480, definition.GetCooldownTicks(1));
            Assert.AreEqual(450, definition.GetCooldownTicks(2));
            Assert.AreEqual(360, definition.GetCooldownTicks(5));
        }

        [Test]
        public void EmpoweredExpiry_ReappliesOneNormalBuffAfterNonHeroKills()
        {
            UnitType heroVictim = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)0.658m,
                    2),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);
            UnitType minion = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Minion,
                    (fp)0.658m,
                    3),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);

            // Empowered (hero kill).
            CombatEvents.RaiseUnitKill(
                caster.UnitUid,
                heroVictim.UnitUid);
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.3m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001);
            Assert.IsTrue(caster.BuffHandler.GetBuffInfo(
                new BuffConfigId(RevengeBuffConfigId),
                out BuffInfo empoweredInfo));
            int empoweredRemaining =
                empoweredInfo.RemainingTicks;

            // Non-hero kill during empowered: stays empowered, no refresh.
            CombatEvents.RaiseUnitKill(
                caster.UnitUid,
                minion.UnitUid);
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.3m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001,
                "Non-hero kill must not downgrade the empowered buff.");
            Assert.IsTrue(caster.BuffHandler.GetBuffInfo(
                new BuffConfigId(RevengeBuffConfigId),
                out BuffInfo afterMinion));
            Assert.AreEqual(
                empoweredRemaining,
                afterMinion.RemainingTicks,
                "Non-hero kill during empowered must not refresh duration.");

            // Let the empowered buff expire, then a normal buff must appear.
            for (int i = 0; i < empoweredRemaining + 10; i++)
            {
                controller.EndTick();
                controller.BeginTick(
                    100 + i,
                    ExecutionMode.ServerAuthority);
                caster.BuffHandler.Advance();
            }

            Assert.IsTrue(caster.BuffHandler.GetBuffInfo(
                new BuffConfigId(RevengeBuffConfigId),
                out BuffInfo normalInfo),
                "Expired empowered buff must re-apply one normal buff.");
            Assert.AreEqual(
                1,
                normalInfo.StackCount);
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.1m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001,
                "Successor buff must use normal (1x) values.");
        }

        [Test]
        public void AssistHero_AppliesEmpoweredBonusToAssistant()
        {
            UnitType assistant = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)0.658m,
                    4),
                new TeamId(1),
                40,
                fp.zero,
                fp.zero);
            assistant.AbilityHandler.SetFixedPassive(
                CreateFixedPassiveDefinition());
            UnitType heroVictim = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)0.658m,
                    2),
                new TeamId(2),
                40,
                fp.zero,
                fp.zero);

            // The assist event must flow through CombatEvents -> UnitEventBus
            // -> BuffHandler -> KillStatGrowthBuffEffect.OnUnitAssist.
            CombatEvents.RaiseUnitAssist(
                assistant.UnitUid,
                heroVictim.UnitUid);

            Assert.IsTrue(assistant.BuffHandler.HasBuff(
                new BuffConfigId(RevengeBuffConfigId)));
            // 30% attack speed (10% * 3), same as a hero kill.
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.3m),
                (double)assistant.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001,
                "Hero assist must grant the empowered Revenge buff.");
            Assert.AreEqual(
                (double)((fp)100 + (fp)3.3m),
                (double)assistant.StatHandler.GetStat(
                    StatId.AttackDamage),
                0.001);
            Assert.AreEqual(
                (double)((fp)0.658m),
                (double)caster.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001,
                "A unit outside the assistant list must not receive the buff.");
        }

        private static PassiveAbilityDef
            CreateFixedPassiveDefinition()
        {
            return new PassiveAbilityDef
            {
                AbilityId = FixedPassiveAbilityId,
                Name = "Revenge",
                PassiveEffect =
                    new ApplyBuffPassiveEffectDef
                    {
                        ListenerMask =
                            AbilityPassiveListenerMask
                                .UnitKill |
                            AbilityPassiveListenerMask
                                .UnitAssist,
                        BuffConfigId =
                            new BuffConfigId(
                                RevengeBuffConfigId),
                    },
            };
        }

        private static BuffDefinition
            CreateRevengeDefinition()
        {
            var definition =
                UnityEngine.ScriptableObject
                    .CreateInstance<BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(RevengeBuffConfigId);
            definition.Display =
                new BuffDisplayInfo
                {
                    Name = "Revenge",
                };
            definition.Life =
                new BuffLifeRuleConfig
                {
                    DurationSeconds = 5f,
                    RefreshMode =
                        BuffRefreshMode.RefreshToFull,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 1,
                    AddMode = BuffAddMode.Add,
                    ReduceMode = BuffReduceMode.Reduce,
                };
            definition.Effects =
                new[]
                {
                    new BuffEffectConfig
                    {
                        Effect =
                            new KillStatGrowthBuffEffect
                            {
                                AttackSpeedPercentByUnitLevel =
                                    new[]
                                    {
                                        0.10f,
                                        0.15f,
                                        0.20f,
                                    },
                                AttackDamagePerAttackSpeedRatio =
                                    (fp)11,
                                AbilityPowerPerAttackSpeedRatio =
                                    (fp)11,
                                HeroVictimMultiplier = (fp)3,
                                DurationSecondsByUnitLevel =
                                    new[]
                                    {
                                        5f,
                                        7f,
                                        9f,
                                        11f,
                                    },
                                AttackSpeedHandleSlot =
                                    new BuffStateSlotId(1),
                                AttackDamageHandleSlot =
                                    new BuffStateSlotId(2),
                                AbilityPowerHandleSlot =
                                    new BuffStateSlotId(3),
                                IsEmpoweredSlot =
                                    new BuffStateSlotId(4),
                                PendingNormalAfterEmpoweredSlot =
                                    new BuffStateSlotId(5),
                            },
                    },
                };
            return definition;
        }

        private static UnitPrototype CreatePrototype(
            UnitKind kind,
            fp attackSpeed,
            int prototypeId = 1)
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)1000,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = (fp)500,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = (fp)100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = attackSpeed,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AbilityPower,
                BaseValue = fp.zero,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = prototypeId,
                RuntimeEntityPrefabId =
                    prototypeId == 1 ? 1001 : 1002,
                UnitKind = kind,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}
