using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// End-to-end assist flow (Combat v13.2 7.14 / 14.6): damage contributions
    /// against a hero victim resolve a stable AssistantHeroUids list; the
    /// assist event is raised exactly once per assistant and is forwarded to
    /// the assistant's BuffHandler, which grants the empowered Revenge buff.
    /// </summary>
    [TestFixture]
    public sealed class AssistEventIntegrationTests
    {
        private const int RevengeBuffConfigId = 9002;

        private SimulationTickContextController controller;
        private UnitWorld world;
        private CombatSystem combat;
        private int assistEventCount;
        private UnitUid lastKillerUid;
        private readonly System.Collections.Generic.List<UnitUid>
            assistUids =
                new System.Collections.Generic.List<UnitUid>();

        [SetUp]
        public void SetUp()
        {
            assistEventCount = 0;
            assistUids.Clear();
            lastKillerUid = default;
            controller = new SimulationTickContextController();
            controller.BeginTick(
                50,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
            UnitPrototype prototype = CreatePrototype();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                50,
                fp.zero,
                fp.zero);
            UnitType assistant = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                50,
                fp.zero,
                fp.zero);
            UnitType victim = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(2),
                50,
                fp.zero,
                fp.zero);

            Caster = caster;
            Assistant = assistant;
            Victim = victim;

            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            world.BuffDefinitions = new BuffDefinitionRegistry();
            world.BuffDefinitions.Register(
                CreateRevengeDefinition());
            CombatEvents.TryResolveUnit =
                uid => world.TryGetUnit(
                    uid,
                    out UnitType unit)
                    ? unit
                    : null;
            CombatEvents.OnUnitAssist +=
                (assistantUid, victimUid) =>
                {
                    assistEventCount++;
                    assistUids.Add(assistantUid);
                };
            CombatEvents.OnUnitDeath +=
                (victimUid, killerUid) =>
                    lastKillerUid = killerUid;

            // The assistant carries the Revenge buff so the assist event can
            // upgrade it.
            assistant.AbilityHandler.SetFixedPassive(
                CreateFixedPassiveDefinition());
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        public UnitType Caster { get; private set; }
        public UnitType Assistant { get; private set; }
        public UnitType Victim { get; private set; }

        [Test]
        public void HeroDeath_RaisesAssistOnce_AndEmpowersAssistantRevenge()
        {
            combat.BeginTick();
            // Assistant contributes 300, caster contributes 800 (fatal);
            // caster is the killer, assistant the single assistant.
            combat.SubmitDamage(CreateDamage(
                Assistant.UnitUid,
                Victim.UnitUid,
                (fp)300));
            combat.SubmitDamage(CreateDamage(
                Caster.UnitUid,
                Victim.UnitUid,
                (fp)400));
            combat.SubmitDamage(CreateDamage(
                Caster.UnitUid,
                Victim.UnitUid,
                (fp)400));
            combat.SettleActiveRequests();
            combat.EndTick();

            Assert.AreEqual(
                LifeState.Dead,
                Victim.LifeState);
            Assert.AreEqual(
                1,
                assistEventCount,
                "The assist event must be raised exactly once per assistant.");

            // The assist must have flowed to the assistant's BuffHandler.
            Assert.IsTrue(Assistant.BuffHandler.HasBuff(
                new BuffConfigId(RevengeBuffConfigId)));
            Assert.AreEqual(
                (double)((fp)0.658m * (fp)1.3m),
                (double)Assistant.StatHandler.GetStat(
                    StatId.AttackSpeed),
                0.001,
                "Hero assist must grant the empowered Revenge buff.");
        }

        [Test]
        public void Killer_IsHighestEffectiveLifeDamageContributorInLethalBatch()
        {
            Caster.AbilityHandler.SetFixedPassive(
                CreateFixedPassiveDefinition());
            combat.BeginTick();
            // Assistant contributes the greater share of effective life
            // damage in the lethal batch. Submission order is not killer
            // authority under D-049.
            combat.SubmitDamage(CreateDamage(
                Assistant.UnitUid,
                Victim.UnitUid,
                (fp)700));
            combat.SubmitDamage(CreateDamage(
                Caster.UnitUid,
                Victim.UnitUid,
                (fp)400));
            combat.SettleActiveRequests();
            combat.EndTick();

            Assert.AreEqual(
                LifeState.Dead,
                Victim.LifeState);
            Assert.AreEqual(
                1,
                combat.DeathResults.Count,
                "Exactly one DeathResult must be produced.");
            Assert.AreEqual(
                Assistant.UnitUid,
                lastKillerUid,
                "The killer must have the greatest effective life damage in the lethal batch.");
            Assert.AreEqual(
                1,
                assistEventCount,
                "assist uids: " +
                string.Join(
                    ",",
                    assistUids.ConvertAll(Describe)) +
                " killer=" + Describe(lastKillerUid) +
                " caster=" + Describe(Caster.UnitUid) +
                " assistant=" + Describe(Assistant.UnitUid));
            Assert.IsTrue(Caster.BuffHandler.HasBuff(
                new BuffConfigId(RevengeBuffConfigId)));
        }

        private static DamageRequest CreateDamage(
            UnitUid source,
            UnitUid target,
            fp amount)
        {
            return new DamageRequest
            {
                Header = new CombatRequestHeader
                {
                    SourceUnitUid = source,
                    TargetUnitUid = target,
                    SourceDescriptor =
                        new SourceDescriptor
                        {
                            SourceType =
                                CombatSourceType.Ability,
                            SourceId = 10011,
                            OwnerUnitUid = source,
                            EmitterUnitUid = source,
                        },
                    RecipeId = 1,
                },
                DamageType = DamageType.Physical,
                BaseDamage = amount,
            };
        }

        private static string Describe(UnitUid uid) =>
            $"{uid.SpawnLogicTick}:{uid.RuntimeEntityPrefabId}:{uid.SpawnSequenceInTick}";

        private static PassiveAbilityDef
            CreateFixedPassiveDefinition()
        {
            return new PassiveAbilityDef
            {
                AbilityId = 10010,
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

        private static UnitPrototype CreatePrototype()
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
                BaseValue = (fp)0.658m,
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
