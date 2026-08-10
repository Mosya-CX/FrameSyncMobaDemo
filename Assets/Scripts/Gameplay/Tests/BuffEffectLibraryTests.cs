using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class BuffEffectLibraryTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private CombatSystem _combat;
        private BuffDefinitionRegistry _buffDefs;
        private SimulationTickContextController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable = CreateFullStatTable(),
            };
            _prototype = CreatePrototype(1);
            _combat = new CombatSystem(_world, 300, 60);
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
            _controller.BeginTick(tick, ExecutionMode.ServerAuthority);
        }

        // ---- PeriodicDamageBuffEffect ----

        [Test]
        public void PeriodicDamage_DealsDamageEachInterval()
        {
            BeginTick(1);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHp = target.StatHandler.CurrentHealth;

            var effect = new PeriodicDamageBuffEffect
            {
                DamagePerTick = 10m,
                DamageType = DamageType.Physical,
            };
            var def = CreateBuffDef(1001, 2, 30, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            for (int tick = 2; tick <= 5; tick++)
            {
                _combat.BeginTick();
                target.BuffHandler.Advance();
                _combat.SettleActiveRequests();
                _combat.EndTick();
            }

            fp afterHp = target.StatHandler.CurrentHealth;
            Assert.Less(afterHp, initialHp, "Should have taken periodic damage");
        }

        [Test]
        public void PeriodicDamage_NoDamageWithoutCombatSystem()
        {
            BeginTick(1);
            _world.CombatSystem = null;
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHp = target.StatHandler.CurrentHealth;

            var effect = new PeriodicDamageBuffEffect
            {
                DamagePerTick = 10m,
                DamageType = DamageType.Physical,
            };
            var def = CreateBuffDef(1002, 1, 10, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            target.BuffHandler.Advance();

            Assert.AreEqual(initialHp, target.StatHandler.CurrentHealth,
                "Should not deal damage without CombatSystem");
        }

        // ---- HealOverTimeBuffEffect ----

        [Test]
        public void HealOverTime_RestoresHealthEachInterval()
        {
            BeginTick(1);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            target.StatHandler.SetCurrentHealth((fp)50);
            fp initialHp = target.StatHandler.CurrentHealth;

            var effect = new HealOverTimeBuffEffect { HealPerTick = 10m };
            var def = CreateBuffDef(2001, 2, 20, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            for (int tick = 2; tick <= 5; tick++)
            {
                _combat.BeginTick();
                target.BuffHandler.Advance();
                _combat.SettleActiveRequests();
                _combat.EndTick();
            }

            fp afterHp = target.StatHandler.CurrentHealth;
            Assert.Greater(afterHp, initialHp, "Health should increase from heal ticks");
        }

        // ---- ShieldOverTimeBuffEffect ----

        [Test]
        public void ShieldOverTime_GrantsShieldEachInterval()
        {
            BeginTick(1);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            var effect = new ShieldOverTimeBuffEffect
            {
                ShieldPerTick = 10m,
                ShieldType = ShieldType.Magic,
                ShieldDurationTicks = 60,
            };
            var def = CreateBuffDef(3001, 1, 20, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            _combat.BeginTick();
            target.BuffHandler.Advance();
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp totalShield = target.StatHandler.CurrentShield;
            Assert.Greater(totalShield, fp.zero, "Should have shield after tick");
        }

        // ---- OnKillStatBuffEffect ----

        [Test]
        public void OnKillStat_GrantsStatOnKill()
        {
            BeginTick(1);
            Unit owner = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialAD = owner.StatHandler.GetStat(StatId.AttackDamage);

            var effect = new OnKillStatBuffEffect
            {
                StatId = StatId.AttackDamage,
                Operation = StatModifierOperation.FlatAdd,
                ValuePerStack = 5m,
                MaxStacks = 5,
                DurationTicks = 300,
                StackCountSlot = new BuffStateSlotId(5101),
                HandleSlot = new BuffStateSlotId(5102),
            };
            var def = CreateBuffDef(5001, lifeRule: BuffLifeRule.Infinite,
                effects: new BuffEffect[] { effect });
            owner.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            owner.BuffHandler.Apply(def.ConfigId, def, owner.UnitUid);

            Unit victim = _world.SpawnUnit(CreatePrototype(2), TeamId.Neutral, 2, 0m, 0m);
            owner.BuffHandler.OnUnitKill(victim);

            fp afterAD = owner.StatHandler.GetStat(StatId.AttackDamage);
            Assert.AreEqual(initialAD + 5m, afterAD,
                "Attack damage should increase after one kill");
        }

        [Test]
        public void OnKillStat_StacksUpToMax()
        {
            BeginTick(1);
            Unit owner = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialAD = owner.StatHandler.GetStat(StatId.AttackDamage);

            var effect = new OnKillStatBuffEffect
            {
                StatId = StatId.AttackDamage,
                Operation = StatModifierOperation.FlatAdd,
                ValuePerStack = 5m,
                MaxStacks = 2,
                DurationTicks = 300,
                StackCountSlot = new BuffStateSlotId(5103),
                HandleSlot = new BuffStateSlotId(5104),
            };
            var def = CreateBuffDef(5002, lifeRule: BuffLifeRule.Infinite,
                effects: new BuffEffect[] { effect });
            owner.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            owner.BuffHandler.Apply(def.ConfigId, def, owner.UnitUid);

            Unit v1 = _world.SpawnUnit(CreatePrototype(2), TeamId.Neutral, 2, 0m, 0m);
            owner.BuffHandler.OnUnitKill(v1);
            Assert.AreEqual(initialAD + 5m, owner.StatHandler.GetStat(StatId.AttackDamage));

            Unit v2 = _world.SpawnUnit(CreatePrototype(3), TeamId.Neutral, 3, 0m, 0m);
            owner.BuffHandler.OnUnitKill(v2);
            Assert.AreEqual(initialAD + 10m, owner.StatHandler.GetStat(StatId.AttackDamage));

            Unit v3 = _world.SpawnUnit(CreatePrototype(4), TeamId.Neutral, 4, 0m, 0m);
            owner.BuffHandler.OnUnitKill(v3);
            Assert.AreEqual(initialAD + 10m, owner.StatHandler.GetStat(StatId.AttackDamage),
                "Should cap at MaxStacks=2");
        }

        // ---- Helpers ----

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

        private static BuffDefinition CreateBuffDef(
            int configId,
            int intervalTicks = 0,
            int durationTicks = 60,
            BuffLifeRule lifeRule = BuffLifeRule.Duration,
            BuffEffect[] effects = null)
        {
            var def =
                ScriptableObject
                    .CreateInstance<BuffDefinition>();
            def.ConfigId = new BuffConfigId(configId);
            def.Life = new BuffLifeRuleConfig
            {
                Infinite =
                    lifeRule == BuffLifeRule.Infinite,
                DurationSeconds =
                    durationTicks /
                    (float)BuffTickConverter.TickRate,
                RefreshMode =
                    BuffRefreshMode.RefreshToFull,
            };
            def.Stack = new BuffStackRuleConfig
            {
                MaxStacks = 1,
                AddMode = BuffAddMode.Ignore,
                ReduceMode = BuffReduceMode.Reduce,
            };
            def.PeriodicIntervalTicks =
                intervalTicks;
            def.InitialStacks = 1;
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
            return def;
        }

        private static StatDefinitionTable CreateFullStatTable()
        {
            var table = new StatDefinitionTable();
            var allIds = System.Enum.GetValues(typeof(StatId));
            for (int i = 0; i < allIds.Length; i++)
            {
                table.Add(new StatDefinition
                {
                    Id = (StatId)allIds.GetValue(i),
                    DebugName = allIds.GetValue(i).ToString(),
                    DefaultBaseValue = fp.zero,
                    SupportsLevelGrowth = true,
                });
            }
            return table;
        }

        private static StatPreset CreateFullStatPreset()
        {
            var preset = new StatPreset();
            var allIds = System.Enum.GetValues(typeof(StatId));
            for (int i = 0; i < allIds.Length; i++)
            {
                preset.Stats.Add(new StatPresetEntry
                {
                    StatId = (StatId)allIds.GetValue(i),
                    BaseValue = (StatId)allIds.GetValue(i) == StatId.MaxHealth
                        ? (fp)100
                        : fp.zero,
                    GrowthValue = fp.zero,
                });
            }
            return preset;
        }
    }
}
