using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Slice 2-4 validation: static-layout blackboard, config-driven lifecycle
    /// and event reactions, apply-flow gaps, ClearForDespawn split, BuffInfo.
    /// </summary>
    public sealed class BuffReactionAndInfoTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private CombatSystem _combat;
        private BuffDefinitionRegistry _buffDefs;
        private SimulationTickContextController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller =
                new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable =
                    CreateFullStatTable(),
            };
            _prototype = CreatePrototype(1);
            _combat =
                new CombatSystem(_world, 300, 60);
            _world.CombatSystem = _combat;
            _buffDefs = new BuffDefinitionRegistry();
            _world.BuffDefinitions = _buffDefs;
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller.IsTickActive)
                _controller.EndTick();
        }

        private void BeginTick(int tick)
        {
            if (_controller.IsTickActive)
                _controller.EndTick();
            _controller.BeginTick(
                tick,
                ExecutionMode.ServerAuthority);
        }

        private Unit Spawn(int id)
        {
            return _world.SpawnUnit(
                CreatePrototype(id),
                TeamId.Neutral,
                id,
                0m,
                0m);
        }

        [Test]
        public void Blackboard_StaticLayout_ReadWriteResetAndRoundTrip()
        {
            var layout = new BuffBlackboardLayout
            {
                Slots = new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(1),
                        Kind = BuffValueKind.Int,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(2),
                        Kind = BuffValueKind.Bool,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(3),
                        Kind = BuffValueKind.Fp,
                    },
                },
            };
            var blackboard = new BuffBlackboard();
            blackboard.Initialize(layout);

            Assert.That(blackboard.SlotCount, Is.EqualTo(3));
            blackboard.WriteInt(new BuffStateSlotId(1), 7);
            blackboard.WriteBool(new BuffStateSlotId(2), true);
            blackboard.WriteFp(new BuffStateSlotId(3), (fp)1.5m);
            Assert.That(
                blackboard.ReadIntOrDefault(new BuffStateSlotId(1)),
                Is.EqualTo(7));
            Assert.That(
                blackboard.ReadBoolOrDefault(new BuffStateSlotId(2)),
                Is.True);
            Assert.That(
                blackboard.ReadFpOrDefault(new BuffStateSlotId(3)),
                Is.EqualTo((fp)1.5m));

            BuffBlackboardSnapshot snapshot =
                blackboard.Capture();
            var restored = new BuffBlackboard();
            restored.Initialize(layout);
            restored.Restore(snapshot);
            Assert.That(
                restored.ReadIntOrDefault(new BuffStateSlotId(1)),
                Is.EqualTo(7));
            Assert.That(
                restored.ReadBoolOrDefault(new BuffStateSlotId(2)),
                Is.True);

            blackboard.Reset();
            Assert.That(
                blackboard.ReadIntOrDefault(new BuffStateSlotId(1)),
                Is.EqualTo(0));
        }

        [Test]
        public void FirstApply_FiresStackChanged_ZeroToInitial()
        {
            BeginTick(1);
            Unit target = Spawn(1);
            fp hp = target.StatHandler.CurrentHealth;

            var def = CreateBuffDef(
                6001,
                lifecycle: new BuffLifecycleReactions
                {
                    StackChanged = new[]
                    {
                        new BuffStackChangedReactionGroup
                        {
                            MinStack = 1,
                            MaxStack = int.MaxValue,
                            Actions = new[]
                            {
                                new BuffDealDamageActionConfig
                                {
                                    DamageAmount = (fp)10m,
                                },
                            },
                        },
                    },
                });
            InCombatTick(
                () => RegisterAndApply(
                    target,
                    def));

            Assert.Less(
                target.StatHandler.CurrentHealth,
                hp,
                "StackChanged 0 -> initial must fire once on first Apply.");
        }

        [Test]
        public void Reapply_FiresReapplied_AndStackChangedOnChange()
        {
            BeginTick(1);
            Unit target = Spawn(1);

            var def = CreateBuffDef(
                6002,
                stackAdd: true,
                lifecycle: new BuffLifecycleReactions
                {
                    Reapplied = new[]
                    {
                        Group(
                            new BuffDealDamageActionConfig
                            {
                                DamageAmount = (fp)3m,
                            }),
                    },
                    StackChanged = new[]
                    {
                        new BuffStackChangedReactionGroup
                        {
                            MinStack = 2,
                            MaxStack = int.MaxValue,
                            Actions = new[]
                            {
                                new BuffDealDamageActionConfig
                                {
                                    DamageAmount = (fp)4m,
                                },
                            },
                        },
                    },
                });
            fp hpAfterFirst = fp.zero;
            InCombatTick(
                () =>
                {
                    RegisterAndApply(
                        target,
                        def);
                    hpAfterFirst =
                        target.StatHandler
                            .CurrentHealth;
                    RegisterAndApply(
                        target,
                        def);
                });

            Assert.Less(
                target.StatHandler.CurrentHealth,
                hpAfterFirst,
                "Reapplied must fire even when duration refreshes; stack 1->2 must fire StackChanged.");
        }

        [Test]
        public void PeriodicReaction_FiresOnIntervalSlot()
        {
            BeginTick(1);
            Unit target = Spawn(1);
            fp hp = target.StatHandler.CurrentHealth;

            var def = CreateBuffDef(
                6003,
                lifecycle: new BuffLifecycleReactions
                {
                    Periodic = new[]
                    {
                        new BuffPeriodicReactionGroup
                        {
                            IntervalSeconds = 1f,
                            TriggerImmediately = false,
                            NextTriggerTickSlot =
                                new BuffStateSlotId(6201),
                            Actions = new[]
                            {
                                new BuffDealDamageActionConfig
                                {
                                    DamageAmount = (fp)7m,
                                },
                            },
                        },
                    },
                });
            RegisterAndApply(target, def);

            for (int tick = 2; tick <= 32; tick++)
            {
                BeginTick(tick);
                _combat.BeginTick();
                target.BuffHandler.Advance();
                _combat.SettleActiveRequests();
                _combat.EndTick();
            }

            Assert.Less(
                target.StatHandler.CurrentHealth,
                hp,
                "Periodic reaction must fire after one second of ticks.");
        }

        [Test]
        public void EventReactions_AbilityCastAndLevelUp_Fire()
        {
            BeginTick(1);
            Unit target = Spawn(1);
            fp hp = target.StatHandler.CurrentHealth;

            var def = CreateBuffDef(
                6004,
                events: new BuffEventReactions
                {
                    AbilityCast = new[]
                    {
                        Group(
                            new BuffDealDamageActionConfig
                            {
                                DamageAmount = (fp)2m,
                            }),
                    },
                    LevelUp = new[]
                    {
                        Group(
                            new BuffDealDamageActionConfig
                            {
                                DamageAmount = (fp)3m,
                            }),
                    },
                });
            InCombatTick(
                () => RegisterAndApply(
                    target,
                    def));

            InCombatTick(
                () =>
                {
                    target.BuffHandler.OnAbilityCast(
                        new AbilityCastEventData(
                            target.UnitUid,
                            1,
                            0,
                            SimulationTickContext
                                .Current.Tick));
                    target.BuffHandler.OnLevelUp(
                        1,
                        2);
                });

            Assert.Less(
                target.StatHandler.CurrentHealth,
                hp - (fp)4m,
                "AbilityCast and LevelUp event reactions must both fire.");
        }

        [Test]
        public void RemovedReaction_FiresOnRemove_ButNotOnDespawn()
        {
            BeginTick(1);
            Unit target = Spawn(1);

            var damageDef = CreateBuffDef(
                6005,
                lifecycle: new BuffLifecycleReactions
                {
                    Removed = new[]
                    {
                        Group(
                            new BuffDealDamageActionConfig
                            {
                                DamageAmount = (fp)5m,
                            }),
                    },
                });
            fp hpAfterApply = fp.zero;
            InCombatTick(
                () =>
                {
                    RegisterAndApply(
                        target,
                        damageDef);
                    hpAfterApply =
                        target.StatHandler
                            .CurrentHealth;
                    target.BuffHandler.Remove(
                        damageDef.ConfigId);
                });
            Assert.Less(
                target.StatHandler.CurrentHealth,
                hpAfterApply,
                "Removed reaction must fire on Remove.");

            var statDef = CreateBuffDef(
                6006,
                effects: new BuffEffect[]
                {
                    new StatModifierBuffEffect
                    {
                        StatId = StatId.AttackDamage,
                        Operation =
                            StatModifierOperation.FlatAdd,
                        BaseValue = (fp)20m,
                        HandleSlot =
                            new BuffStateSlotId(6301),
                    },
                },
                lifecycle: new BuffLifecycleReactions
                {
                    Removed = new[]
                    {
                        Group(
                            new BuffDealDamageActionConfig
                            {
                                DamageAmount = (fp)9m,
                            }),
                    },
                });
            fp adBeforeBuff =
                target.StatHandler.GetStat(
                    StatId.AttackDamage);
            InCombatTick(
                () => RegisterAndApply(
                    target,
                    statDef));
            fp adWithBuff =
                target.StatHandler.GetStat(
                    StatId.AttackDamage);
            Assert.AreEqual(
                adBeforeBuff + (fp)20m,
                adWithBuff);
            fp hpBeforeDespawn =
                target.StatHandler.CurrentHealth;
            InCombatTick(
                () => target.BuffHandler
                    .ClearForDespawn(
                        UnitDespawnReason
                            .MatchCleanup));

            Assert.AreEqual(
                adBeforeBuff,
                target.StatHandler.GetStat(
                    StatId.AttackDamage),
                "Despawn must release the stat handle.");
            Assert.AreEqual(
                hpBeforeDespawn,
                target.StatHandler.CurrentHealth,
                "Despawn must NOT fire the Removed reaction.");
        }

        [Test]
        public void BuffInfo_ExposesReadOnlyFields_AndTagQuery()
        {
            BeginTick(1);
            Unit target = Spawn(1);
            fp adBeforeBuff =
                target.StatHandler.GetStat(
                    StatId.AttackDamage);
            var def = CreateBuffDef(
                6007,
                durationTicks: 90,
                tag: 7);
            def.Display = new BuffDisplayInfo
            {
                Name = "TestBuff",
                Description = "Reaction test buff",
            };
            RegisterAndApply(target, def);

            Assert.IsTrue(
                target.BuffHandler.GetBuffInfo(
                    def.ConfigId,
                    out BuffInfo info));
            Assert.That(info.Name, Is.EqualTo("TestBuff"));
            Assert.That(info.StackCount, Is.EqualTo(1));
            Assert.That(info.MaxStacks, Is.EqualTo(1));
            Assert.That(info.DurationTicks, Is.EqualTo(90));
            Assert.That(
                info.TimeProgress,
                Is.InRange(0f, 1f));
            Assert.That(info.Infinite, Is.False);

            var byTag = new List<BuffInfo>();
            target.BuffHandler.GetBuffInfosByTag(
                7,
                byTag);
            Assert.That(byTag.Count, Is.EqualTo(1));
            Assert.That(
                target.BuffHandler.GetAllBuffInfos()
                    .Count,
                Is.EqualTo(1));
        }

        [Test]
        public void StatModifierEffect_ValuePerStack_UpdatesOnStackChange()
        {
            BeginTick(1);
            Unit target = Spawn(1);
            fp adBeforeBuff =
                target.StatHandler.GetStat(
                    StatId.AttackDamage);
            var def = CreateBuffDef(
                6008,
                stackAdd: true,
                effects: new BuffEffect[]
                {
                    new StatModifierBuffEffect
                    {
                        StatId = StatId.AttackDamage,
                        Operation =
                            StatModifierOperation.FlatAdd,
                        BaseValue = (fp)10m,
                        ValuePerStack = (fp)5m,
                        HandleSlot =
                            new BuffStateSlotId(6401),
                    },
                });
            InCombatTick(
                () => RegisterAndApply(
                    target,
                    def));
            Assert.AreEqual(
                adBeforeBuff + (fp)10m,
                target.StatHandler.GetStat(
                    StatId.AttackDamage),
                "One stack applies BaseValue only.");

            InCombatTick(
                () => RegisterAndApply(
                    target,
                    def));
            Assert.AreEqual(
                adBeforeBuff + (fp)15m,
                target.StatHandler.GetStat(
                    StatId.AttackDamage),
                "Two stacks apply BaseValue + one ValuePerStack.");
        }

        // ---- Helpers ----

        private void RegisterAndApply(
            Unit target,
            BuffDefinition def)
        {
            target.BuffHandler.DefinitionRegistry =
                _buffDefs;
            if (!_buffDefs.TryGet(
                    def.ConfigId,
                    out _))
                _buffDefs.Register(def);
            target.BuffHandler.Apply(
                def.ConfigId,
                def,
                target.UnitUid);
        }

        private void InCombatTick(
            System.Action action)
        {
            _combat.BeginTick();
            action();
            _combat.SettleActiveRequests();
            _combat.EndTick();
        }

        private static BuffReactionGroup Group(
            BuffReactionActionConfig action)
        {
            return new BuffReactionGroup
            {
                Actions = new[]
                {
                    action,
                },
            };
        }

        private static BuffDefinition CreateBuffDef(
            int configId,
            int intervalTicks = 0,
            int durationTicks = 60,
            bool stackAdd = false,
            byte tag = 0,
            BuffEffect[] effects = null,
            BuffLifecycleReactions lifecycle = null,
            BuffEventReactions events = null)
        {
            var def =
                ScriptableObject
                    .CreateInstance<BuffDefinition>();
            def.ConfigId = new BuffConfigId(configId);
            def.Life = new BuffLifeRuleConfig
            {
                Infinite = false,
                DurationSeconds =
                    durationTicks /
                    30f,
                RefreshMode =
                    BuffRefreshMode.RefreshToFull,
            };
            def.Stack = new BuffStackRuleConfig
            {
                MaxStacks = stackAdd ? 5 : 1,
                AddMode = stackAdd
                    ? BuffAddMode.Add
                    : BuffAddMode.Ignore,
                ReduceMode = BuffReduceMode.Reduce,
            };
            def.PeriodicIntervalTicks =
                intervalTicks;
            def.InitialStacks = 1;
            def.Tags = tag != 0
                ? new BuffTagSet
                {
                    TagIds = new[] { tag },
                }
                : null;
            BuffEffect[] arr =
                effects ??
                System.Array.Empty<BuffEffect>();
            var configs =
                new BuffEffectConfig[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                configs[i] = new BuffEffectConfig
                {
                    Effect = arr[i],
                };
            def.Effects = configs;
            def.LifecycleReactions = lifecycle;
            def.EventReactions = events;
            return def;
        }

        private static UnitPrototype CreatePrototype(int id)
        {
            return new UnitPrototype
            {
                UnitPrototypeId = id,
                Name = "TestUnit_" + id.ToString(),
                RuntimeEntityPrefabId = 100 + id,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateFullStatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
        }

        private static StatDefinitionTable
            CreateFullStatTable()
        {
            var table = new StatDefinitionTable();
            var allIds =
                System.Enum.GetValues(typeof(StatId));
            for (int i = 0; i < allIds.Length; i++)
            {
                table.Add(new StatDefinition
                {
                    Id = (StatId)allIds.GetValue(i),
                    DebugName =
                        allIds.GetValue(i).ToString(),
                    DefaultBaseValue = fp.zero,
                    SupportsLevelGrowth = true,
                });
            }
            return table;
        }

        private static StatPreset CreateFullStatPreset()
        {
            var preset = new StatPreset();
            var allIds =
                System.Enum.GetValues(typeof(StatId));
            for (int i = 0; i < allIds.Length; i++)
            {
                preset.Stats.Add(
                    new StatPresetEntry
                    {
                        StatId =
                            (StatId)allIds.GetValue(i),
                        BaseValue =
                            (StatId)allIds.GetValue(i) ==
                                StatId.MaxHealth
                                ? (fp)100
                                : fp.zero,
                        GrowthValue = fp.zero,
                    });
            }
            return preset;
        }
    }
}
