using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the stacking-blight mechanic: Ability damage detonates and
    /// consumes all stacks, deals MaxHealth-percent magic damage (with a
    /// non-hero per-stack cap), grants the caster cooldown reduction on hero
    /// targets, never re-triggers from its own detonation, and the W passive
    /// adds bonus on-hit magic damage plus one blight stack.
    /// </summary>
    [TestFixture]
    public sealed class BlightDetonationAndOnHitPassiveTests
    {
        private const int BlightConfigId = 9001;
        private const int QAbilityId = 10011;

        private SimulationTickContextController controller;
        private UnitWorld world;
        private CombatSystem combat;
        private BuffDefinitionRegistry buffDefs;
        private UnitType caster;
        private UnitType target;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(
                30,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
            UnitPrototype prototype = CreatePrototype(
                UnitKind.Hero,
                (fp)1000);
            caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                30,
                fp.zero,
                fp.zero);
            target = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(2),
                30,
                fp.zero,
                fp.zero);

            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            buffDefs = new BuffDefinitionRegistry();
            world.BuffDefinitions = buffDefs;
            buffDefs.Register(CreateBlightDefinition());
            CombatEvents.TryResolveUnit =
                uid => world.TryGetUnit(
                    uid,
                    out UnitType unit)
                    ? unit
                    : null;

            InstallWOnCaster();
            InstallCooldownProbeOnCaster();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void AbilityHit_DetonatesAndConsumesAllStacks()
        {
            ApplyBlightTwice();
            combat.BeginTick();

            DealAbilityDamage((fp)50);
            combat.SettleActiveRequests();

            // 50 physical + 2 * (1000 * 3%) = 60 magic.
            Assert.AreEqual(
                (double)((fp)890),
                (double)target.StatHandler.CurrentHealth,
                0.01);
            Assert.IsFalse(
                target.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)),
                "All blight stacks must be consumed.");
        }

        [Test]
        public void Detonation_DoesNotRecurse()
        {
            ApplyBlightTwice();
            combat.BeginTick();

            DealAbilityDamage((fp)50);
            combat.SettleActiveRequests();

            // Exactly one detonation: 1000 - 50 - 60.
            Assert.AreEqual(
                (double)((fp)890),
                (double)target.StatHandler.CurrentHealth,
                0.01);
        }

        [Test]
        public void Detonation_NonHero_CapsPerStackDamage()
        {
            UnitType monster = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Monster,
                    (fp)10000,
                    2),
                new TeamId(2),
                30,
                fp.zero,
                fp.zero);
            monster.BuffHandler.Apply(
                new BuffConfigId(BlightConfigId),
                CreateBlightDefinition(),
                caster.UnitUid);
            monster.BuffHandler.Apply(
                new BuffConfigId(BlightConfigId),
                CreateBlightDefinition(),
                caster.UnitUid);
            combat.BeginTick();

            DealAbilityDamageTo(
                monster,
                (fp)10);
            combat.SettleActiveRequests();

            // perStack = 10000 * 3% = 300, capped at 120 -> 2 * 120 = 240.
            Assert.AreEqual(
                (double)((fp)10000 - (fp)10 - (fp)240),
                (double)monster.StatHandler.CurrentHealth,
                0.01);
        }

        [Test]
        public void Detonation_Structure_CapsPerStackDamage()
        {
            UnitType structure = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Structure,
                    (fp)10000,
                    3),
                new TeamId(2),
                30,
                fp.zero,
                fp.zero);
            ApplyBlightTwice(structure);
            combat.BeginTick();

            DealAbilityDamageTo(structure, (fp)10);
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)10000 - (fp)10 - (fp)240),
                (double)structure.StatHandler.CurrentHealth,
                0.01);
            Assert.IsFalse(
                structure.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)));
        }

        [Test]
        public void Detonation_Hero_GrantsCooldownReduction()
        {
            ApplyBlightTwice();
            AbilityRuntime probe = GetRuntime(0);
            probe.StartCooldown(30, 1000);
            int before = probe.CooldownEndsAtTick;

            combat.BeginTick();
            DealAbilityDamage((fp)10);
            combat.SettleActiveRequests();

            Assert.Less(
                probe.CooldownEndsAtTick,
                before,
                "Hero detonation must refund basic-ability cooldown.");
            // 2 stacks * 13% of 1000 ticks = 260.
            Assert.That(
                before - probe.CooldownEndsAtTick,
                Is.InRange(255, 260));
        }

        [Test]
        public void AttackDamage_DoesNotDetonate()
        {
            ApplyBlightTwice();
            combat.BeginTick();

            var request = new DamageRequest
            {
                Header = new CombatRequestHeader
                {
                    SourceUnitUid = caster.UnitUid,
                    TargetUnitUid = target.UnitUid,
                    SourceDescriptor =
                        new SourceDescriptor
                        {
                            SourceType =
                                CombatSourceType.Attack,
                            SourceId = 1,
                            OwnerUnitUid =
                                caster.UnitUid,
                            EmitterUnitUid =
                                caster.UnitUid,
                        },
                    RecipeId = 1,
                },
                DamageType = DamageType.Physical,
                BaseDamage = (fp)20,
            };
            combat.SubmitDamage(request);
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)1000 - (fp)20 - (fp)19),
                (double)target.StatHandler.CurrentHealth,
                0.01,
                "Attack damage plus W on-hit bonus must land, but must not detonate.");
            Assert.IsTrue(
                target.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)),
                "Attack damage must not detonate blight.");
        }

        [Test]
        public void WPassive_OnHitDealsBonusDamageAndAppliesBlight()
        {
            combat.BeginTick();
            caster.AbilityHandler.OnHitDealt(
                new OnHitEventData
                {
                    SourceUid = caster.UnitUid,
                    TargetUid = target.UnitUid,
                    DamageType = DamageType.Physical,
                });
            combat.SettleActiveRequests();

            // level 1: 4 flat + 15% of 100 AD = 19 magic.
            Assert.AreEqual(
                (double)((fp)1000 - (fp)19),
                (double)target.StatHandler.CurrentHealth,
                0.0001);
            Assert.IsTrue(
                target.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)),
                "W passive must apply a blight stack.");
        }

        [Test]
        public void WPassive_OnHit_AppliesToStructure()
        {
            UnitType structure = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(
                    UnitKind.Structure,
                    (fp)1000,
                    4),
                new TeamId(2),
                30,
                fp.zero,
                fp.zero);
            combat.BeginTick();

            caster.AbilityHandler.OnHitDealt(
                new OnHitEventData
                {
                    SourceUid = caster.UnitUid,
                    TargetUid = structure.UnitUid,
                    DamageType = DamageType.Physical,
                });
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)1000 - (fp)19),
                (double)structure.StatHandler.CurrentHealth,
                0.0001);
            Assert.IsTrue(
                structure.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)));
        }

        private void ApplyBlightTwice()
        {
            ApplyBlightTwice(target);
        }

        private void ApplyBlightTwice(UnitType victim)
        {
            victim.BuffHandler.Apply(
                new BuffConfigId(BlightConfigId),
                CreateBlightDefinition(),
                caster.UnitUid);
            victim.BuffHandler.Apply(
                new BuffConfigId(BlightConfigId),
                CreateBlightDefinition(),
                caster.UnitUid);
        }

        private void DealAbilityDamage(fp amount)
        {
            DealAbilityDamageTo(target, amount);
        }

        private void DealAbilityDamageTo(
            UnitType victim,
            fp amount)
        {
            var request = new DamageRequest
            {
                Header = new CombatRequestHeader
                {
                    SourceUnitUid = caster.UnitUid,
                    TargetUnitUid = victim.UnitUid,
                    SourceDescriptor =
                        new SourceDescriptor
                        {
                            SourceType =
                                CombatSourceType.Ability,
                            SourceId = QAbilityId,
                            OwnerUnitUid =
                                caster.UnitUid,
                            EmitterUnitUid =
                                caster.UnitUid,
                        },
                    RecipeId = 10011,
                },
                DamageType = DamageType.Physical,
                BaseDamage = amount,
            };
            combat.SubmitDamage(request);
        }

        private void InstallWOnCaster()
        {
            var model = new ToggleCastModelDef
            {
                Active = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 360000,
                    Def = new DelayStageDef(),
                },
                ResourcePerTick = fp.zero,
            };
            Install(
                new AbilityDef
                {
                    AbilityId = 10012,
                    Name = "TestW",
                    CastModel = model,
                    AimKind = AimKind.None,
                    CostPlan = default,
                    CooldownByLevel = default,
                    PassiveEffect =
                        new OnHitBonusDamagePassiveEffectDef
                        {
                            ListenerMask =
                                AbilityPassiveListenerMask
                                    .OnHitDealt,
                            FlatBonusDamageByLevel =
                                new AbilityLevelValue(
                                    new[]
                                    {
                                        (fp)4,
                                        (fp)13,
                                        (fp)22,
                                        (fp)31,
                                        (fp)40,
                                    }),
                            AttackDamageRatio = (fp)0.15m,
                            AbilityPowerRatio = (fp)0.25m,
                            RecipeId = 10012,
                            ApplyBuffConfigId =
                                new BuffConfigId(
                                    BlightConfigId),
                        },
                },
                1);
        }

        private void InstallCooldownProbeOnCaster()
        {
            var model = new CommitCastModelDef
            {
                Cast = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 1,
                    Def = new DelayStageDef(),
                },
            };
            Install(
                new AbilityDef
                {
                    AbilityId = 10013,
                    Name = "TestCDRProbe",
                    CastModel = model,
                    AimKind = AimKind.None,
                    CostPlan = default,
                    CooldownByLevel =
                        new AbilityLevelValue(
                            new[] { (fp)1000 }),
                },
                0);
        }

        private void Install(
            AbilityDef definition,
            byte slot)
        {
            var runtime = new AbilityRuntime
            {
                Definition = definition,
                Level = 1,
            };
            var slotRuntime = new AbilitySlotRuntime
            {
                SlotIndex = slot,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
            };
            slotRuntime.AddAbility(runtime);
            caster.AbilityHandler.AddSlot(slotRuntime);
        }

        private AbilityRuntime GetRuntime(byte slot) =>
            caster.AbilityHandler.GetActiveRuntime(slot);

        private static BuffDefinition CreateBlightDefinition()
        {
            var definition =
                UnityEngine.ScriptableObject
                    .CreateInstance<BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(BlightConfigId);
            definition.Display =
                new BuffDisplayInfo
                {
                    Name = "Blight",
                };
            definition.Life =
                new BuffLifeRuleConfig
                {
                    DurationSeconds = 6f,
                    RefreshMode =
                        BuffRefreshMode.RefreshToFull,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 3,
                    AddMode = BuffAddMode.Add,
                    ReduceMode = BuffReduceMode.ClearAll,
                };
            definition.Effects =
                new[]
                {
                    new BuffEffectConfig
                    {
                        Effect =
                            new AbilityHitStackDetonationBuffEffect
                            {
                                PercentOfMaxHpPerStackByLevel =
                                    new[]
                                    {
                                        0.03f,
                                        0.035f,
                                        0.04f,
                                        0.045f,
                                        0.05f,
                                    },
                                AbilityPowerRatioPerStack =
                                    (fp)0.00013m,
                                MaxDamagePerStackVsNonHero =
                                    (fp)120,
                                HeroCooldownReductionPercentPerStack =
                                    (fp)0.13m,
                                RecipeId = 10012,
                                SourceAbilitySlot = 1,
                                DetonateSourceAbilityIds =
                                    new[]
                                    {
                                        QAbilityId,
                                        10014,
                                    },
                            },
                    },
                };
            return definition;
        }

        private static UnitPrototype CreatePrototype(
            UnitKind kind,
            fp maxHealth,
            int prototypeId = 1)
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = maxHealth,
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
