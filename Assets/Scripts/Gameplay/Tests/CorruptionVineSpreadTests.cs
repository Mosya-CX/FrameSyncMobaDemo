using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the Corruption Vines (R) spread semantics:
    /// - spread only infects heroes whose team differs from the original
    ///   R caster (never the caster's own camp, never the caster itself);
    /// - a hero that is already infected (vine or permanent marker) is never
    ///   infected a second time;
    /// - Blight stacks are applied at the configured ticks.
    /// </summary>
    [TestFixture]
    public sealed class CorruptionVineSpreadTests
    {
        private const int BlightConfigId = 9001;
        private const int VineConfigId = 9113;
        private const int TimerConfigId = 9115;
        private const string SpreadTagKey =
            "VarusR.Vine";

        private SimulationTickContextController controller;
        private UnitWorld world;
        private PhysicsWorld physicsWorld;
        private BuffDefinitionRegistry buffDefs;
        private CombatSystem combat;
        private BuffDefinition vineDef;
        private UnitType caster;
        private UnitType ally;
        private UnitType enemyA;
        private UnitType enemyB;
        private UnitType farEnemy;
        private int nextTick = 30;

        [SetUp]
        public void SetUp()
        {
            world = new UnitWorld();
            physicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = (fp)10m,
                },
            };
            world.PhysicsWorld = physicsWorld;
            world.RangeQuery =
                new RangeQueryService(
                    physicsWorld);
            combat =
                new CombatSystem(
                    world,
                    0,
                    0);
            world.CombatSystem = combat;
            world.CrowdControlDefinitions =
                new CrowdControlDefinitionRegistry();
            RegisterRootControl(
                world.CrowdControlDefinitions);

            buffDefs =
                new BuffDefinitionRegistry();
            world.BuffDefinitions = buffDefs;
            buffDefs.Register(
                CreateBlightDefinition());
            vineDef = CreateVineDefinition();
            buffDefs.Register(vineDef);
            buffDefs.Register(
                CreateTimerDefinition());

            UnitPrototype prototype =
                CreatePrototype(
                    UnitKind.Hero,
                    (fp)1000);
            caster = SpawnAt(
                prototype,
                new TeamId(1),
                new fp2((fp)0m, (fp)0m));
            ally = SpawnAt(
                prototype,
                new TeamId(1),
                new fp2((fp)2m, (fp)0m));
            enemyA = SpawnAt(
                prototype,
                new TeamId(2),
                new fp2((fp)4m, (fp)0m));
            enemyB = SpawnAt(
                prototype,
                new TeamId(2),
                new fp2((fp)5m, (fp)0m));
            farEnemy = SpawnAt(
                prototype,
                new TeamId(2),
                new fp2((fp)20m, (fp)0m));
            physicsWorld.BuildUnitFinalGrid();
        }

        [TearDown]
        public void TearDown()
        {
            UnitTestFactory
                .DestroyCreatedObjects();
        }

        [Test]
        public void
            Spread_InfectsOnlyEnemyHeroesOfOriginalCaster()
        {
            ApplyVine(enemyA, caster);

            TickAll(40);

            Assert.IsTrue(
                enemyA.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "First R target keeps the vine buff.");
            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "Nearby enemy hero must be infected.");
            Assert.IsTrue(
                enemyB.HasTag(SpreadTagKey),
                "Infected hero must carry the per-cast invisible tag.");
            Assert.IsFalse(
                caster.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "The caster must never be infected.");
            Assert.IsFalse(
                ally.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "A hero on the caster's team must never be infected.");
            Assert.IsFalse(
                farEnemy.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "An enemy hero outside the spread radius is not infected.");
        }

        [Test]
        public void
            Spread_DoesNotReinfectAnInfectedHero()
        {
            ApplyVine(enemyA, caster);

            TickAll(40);
            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)));

            // Run far enough that both infected units would spread again.
            TickAll(60);

            AssertBuffStackCount(
                enemyA,
                VineConfigId,
                1,
                "enemyA");
            AssertBuffStackCount(
                enemyB,
                VineConfigId,
                1,
                "enemyB");
            Assert.IsFalse(
                caster.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)));
            Assert.IsFalse(
                ally.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)));
        }

        [Test]
        public void
            Spread_RequiresContinuousContactInsideRadius()
        {
            ApplyVine(enemyA, caster);

            // Stay inside for half a second, then leave before the 1s
            // contact requirement is met.
            TickAll(15);
            enemyB.PhysicsEntity.SetLogicPose(
                new fp2(
                    (fp)20m,
                    (fp)0m),
                new fp2(
                    fp.one,
                    fp.zero));
            physicsWorld.BuildUnitFinalGrid();
            TickAll(10);

            Assert.IsFalse(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "Leaving the radius before 1s must cancel the contact.");
            Assert.IsFalse(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        TimerConfigId)),
                "The stale contact timer must be removed outside the radius.");

            // Re-enter: the full 1s must elapse again.
            enemyB.PhysicsEntity.SetLogicPose(
                new fp2(
                    (fp)5m,
                    (fp)0m),
                new fp2(
                    fp.one,
                    fp.zero));
            physicsWorld.BuildUnitFinalGrid();
            TickAll(10);
            Assert.IsFalse(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "10 Ticks after re-entering are not enough (need 30).");

            TickAll(30);
            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "A full second inside again must infect the hero.");
        }

        [Test]
        public void
            Spread_AppliesSameRDamageAndRoot()
        {
            ApplyVine(enemyA, caster);

            TickAll(40);

            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)));
            Assert.AreEqual(
                (double)((fp)1000 - (fp)150),
                (double)enemyB.StatHandler
                    .CurrentHealth,
                0.01,
                "Spread must apply the same 150 base R damage.");
            Assert.GreaterOrEqual(
                enemyB.CrowdControl.Count,
                1,
                "Spread must apply the same 2s root as R.");
        }

        [Test]
        public void
            SecondR_StartsAFreshSpread()
        {
            ApplyVine(enemyA, caster);
            TickAll(40);
            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)));

            // The first R's vine and per-cast tag are gone (6s window
            // expired) - a second R cast may infect the hero again.
            enemyB.BuffHandler.Remove(
                new BuffConfigId(
                    VineConfigId));
            enemyB.RemoveTag(SpreadTagKey);

            ApplyVine(enemyA, caster);
            TickAll(40);

            Assert.IsTrue(
                enemyB.BuffHandler.HasBuff(
                    new BuffConfigId(
                        VineConfigId)),
                "A later R cast must start a fresh spread and may "
                + "re-infect a hero from the previous R.");
        }

        [Test]
        public void
            SecondR_UsesADifferentTagUid()
        {
            ApplyVine(enemyA, caster);
            TickAll(40);
            Assert.IsTrue(
                enemyB.TryGetTag(
                    SpreadTagKey,
                    out UnitTag first));

            enemyB.BuffHandler.Remove(
                new BuffConfigId(
                    VineConfigId));
            enemyB.RemoveTag(SpreadTagKey);
            ApplyVine(enemyA, caster);
            TickAll(40);

            Assert.IsTrue(
                enemyB.TryGetTag(
                    SpreadTagKey,
                    out UnitTag second));
            Assert.AreNotEqual(
                first.Uid,
                second.Uid,
                "Two R casts must carry independent tag Uids.");
        }

        [Test]
        public void
            Blight_StacksAreAppliedAtConfiguredTicks()
        {
            ApplyVine(enemyA, caster);

            TickAll(6);
            AssertBuffStackCount(
                enemyA,
                BlightConfigId,
                1,
                "enemyA at tick 6");

            TickAll(18);
            AssertBuffStackCount(
                enemyA,
                BlightConfigId,
                2,
                "enemyA at tick 24");

            TickAll(18);
            AssertBuffStackCount(
                enemyA,
                BlightConfigId,
                3,
                "enemyA at tick 42");
        }

        private void AssertBuffStackCount(
            UnitType unit,
            int configId,
            int expected,
            string context)
        {
            var id = new BuffConfigId(
                configId);
            Assert.IsTrue(
                unit.BuffHandler.TryGetRuntime(
                    id,
                    out BuffRuntime runtime),
                $"{context}: buff {configId} expected.");
            Assert.AreEqual(
                expected,
                runtime.CurrentStacks,
                $"{context}: buff {configId} stack count.");
        }

        private void ApplyVine(
            UnitType target,
            UnitType source)
        {
            controller =
                new SimulationTickContextController();
            controller.BeginTick(
                ++nextTick,
                ExecutionMode.ServerAuthority);
            try
            {
                target.BuffHandler.Apply(
                    new BuffConfigId(
                        VineConfigId),
                    vineDef,
                    BuffSource.Create(
                        source.UnitUid,
                        BuffSourceType.Ability,
                        0));
            }
            finally
            {
                controller.EndTick();
            }
        }

        private void TickAll(int ticks)
        {
            for (int i = 1; i <= ticks; i++)
            {
                controller.BeginTick(
                    ++nextTick,
                    ExecutionMode
                        .ServerAuthority);
                combat?.BeginTick();
                var units =
                    world.GetAllUnits();
                for (int u = 0;
                     u < units.Count;
                     u++)
                {
                    units[u].TickTags();
                    units[u].BuffHandler
                        .Advance();
                }
                combat?.SettleActiveRequests();
                controller.EndTick();
            }
        }

        private UnitType SpawnAt(
            UnitPrototype prototype,
            TeamId teamId,
            fp2 position)
        {
            UnitType unit =
                UnitTestFactory.SpawnUnit(
                    world,
                    prototype,
                    teamId,
                    30,
                    fp.zero,
                    fp.zero);
            unit.PhysicsEntity
                .SetLogicPose(
                    position,
                    new fp2(
                        fp.one,
                        fp.zero));
            return unit;
        }

        private static BuffDefinition
            CreateVineDefinition()
        {
            var effect =
                new CorruptionVineSpreadBuffEffect
                {
                    BlightBuffConfigId =
                        BlightConfigId,
                    VineBuffConfigId =
                        VineConfigId,
                    SpreadTagKey =
                        SpreadTagKey,
                    SpreadTagTicks = 180,
                    TimerBuffConfigId =
                        TimerConfigId,
                    ContactTicks = 30,
                    SpreadRadius = (fp)5.5m,
                    BlightStackAtTick1 = 6,
                    BlightStackAtTick2 = 24,
                    BlightStackAtTick3 = 42,
                    ElapsedTicksSlot =
                        new BuffStateSlotId(1),
                    CasterUnitUidSlot =
                        new BuffStateSlotId(2),
                };
            var definition =
                ScriptableObject
                    .CreateInstance<
                        BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(
                    VineConfigId);
            definition.Display =
                new BuffDisplayInfo
                {
                    Name = "CorruptionVines",
                };
            definition.Life =
                new BuffLifeRuleConfig
                {
                    DurationSeconds = 60f,
                    Infinite = true,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 1,
                    AddMode =
                        BuffAddMode.Add,
                };
            definition.PeriodicIntervalTicks = 1;
            definition.Effects =
                new[]
                {
                    new BuffEffectConfig
                    {
                        Effect = effect,
                    },
                };
            return definition;
        }

        private static BuffDefinition
            CreateTimerDefinition()
        {
            var definition =
                ScriptableObject
                    .CreateInstance<
                        BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(
                    TimerConfigId);
            definition.Display =
                new BuffDisplayInfo
                {
                    Name = "VineContactTimer",
                };
            definition.Life =
                new BuffLifeRuleConfig
                {
                    DurationSeconds = 60f,
                    Infinite = true,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 1,
                    AddMode =
                        BuffAddMode.Ignore,
                };
            definition.Effects =
                System.Array
                    .Empty<BuffEffectConfig>();
            return definition;
        }

        private static BuffDefinition
            CreateBlightDefinition()
        {
            var definition =
                ScriptableObject
                    .CreateInstance<
                        BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(
                    BlightConfigId);
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
                        BuffRefreshMode
                            .RefreshToFull,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 3,
                    AddMode =
                        BuffAddMode.Add,
                };
            definition.Effects =
                System.Array
                    .Empty<BuffEffectConfig>();
            return definition;
        }

        private static void RegisterRootControl(
            CrowdControlDefinitionRegistry
                registry)
        {
            if (registry == null)
            {
                return;
            }
            var definition =
                ScriptableObject
                    .CreateInstance<
                        CrowdControlDefinition>();
            definition.Configure(
                CrowdControlIds.Root,
                CrowdControlIntensity.Medium,
                CrowdControlDefinition
                    .ControlTagBits.Control |
                CrowdControlDefinition
                    .ControlTagBits.Root,
                CrowdControlDurationRule
                    .DefaultTenacity,
                null,
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId =
                            CrowdControlModuleId
                                .BlockActions,
                        StaticData =
                            (int)(
                                UnitActionBlockMask
                                    .VoluntaryMove |
                                UnitActionBlockMask
                                    .Turn |
                                UnitActionBlockMask
                                    .VoluntaryAttack |
                                UnitActionBlockMask
                                    .AbilityCast |
                                UnitActionBlockMask
                                    .Mobility),
                    },
                });
            registry.Register(definition);
        }

        private static UnitPrototype
            CreatePrototype(
                UnitKind kind,
                fp maxHealth)
        {
            var preset = new StatPreset();
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId =
                        StatId.MaxHealth,
                    BaseValue =
                        maxHealth,
                });
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                RuntimeEntityPrefabId = 1,
                UnitKind = kind,
                UnitSubKindId = 0,
                BaseStats = preset,
            };
        }
    }
}
