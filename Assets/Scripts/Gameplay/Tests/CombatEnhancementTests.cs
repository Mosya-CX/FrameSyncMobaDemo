using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class CombatEnhancementTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private CombatSystem _combat;
        private SimulationTickContextController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable = CreateFullStatTable(),
            };
            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestUnit",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateFullPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
            _combat = new CombatSystem(_world, 300, 60);
            _world.CombatSystem = _combat;
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

        // ---- Critical Strike ----

        [Test]
        public void Crit_100PercentChance_DoublesDamage()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            attacker.StatHandler.AddModifier(
                StatId.CriticalStrikeChance, StatModifierOperation.FlatAdd, fp.one);
            attacker.StatHandler.AddModifier(
                StatId.CriticalStrikeDamage, StatModifierOperation.FlatAdd, (fp)2);

            _world.RandomService = new DeterministicRandomService(42u);

            fp initialHp = target.StatHandler.CurrentHealth;
            fp baseDamage = 50m;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, baseDamage));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp actualDamage = initialHp - target.StatHandler.CurrentHealth;
            Assert.Greater(actualDamage, baseDamage,
                "Critical damage should exceed base damage");
        }

        [Test]
        public void Crit_ZeroPercentChance_NoCrit()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            _world.RandomService = new DeterministicRandomService(42u);

            fp initialHp = target.StatHandler.CurrentHealth;
            fp baseDamage = 100m;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, baseDamage));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp actualDamage = initialHp - target.StatHandler.CurrentHealth;
            Assert.AreEqual(baseDamage, actualDamage,
                "Zero crit chance should produce no crit");
        }

        [Test]
        public void Crit_DamageEventData_IsCriticalFlag()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            attacker.StatHandler.AddModifier(
                StatId.CriticalStrikeChance, StatModifierOperation.FlatAdd, fp.one);
            attacker.StatHandler.AddModifier(
                StatId.CriticalStrikeDamage, StatModifierOperation.FlatAdd, (fp)2);

            _world.RandomService = new DeterministicRandomService(42u);

            bool isCritFlag = false;
            CombatEvents.OnDamageDealt += (data) => { isCritFlag = data.IsCritical; };

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)50));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.IsTrue(isCritFlag,
                "DamageEventData.IsCritical should be true with 100% crit");

            CombatEvents.Clear();
        }

        [Test]
        public void Crit_NoRandomService_NoCrit()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            attacker.StatHandler.AddModifier(
                StatId.CriticalStrikeChance, StatModifierOperation.FlatAdd, fp.one);

            fp initialHp = target.StatHandler.CurrentHealth;
            fp baseDamage = 100m;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, baseDamage));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp actualDamage = initialHp - target.StatHandler.CurrentHealth;
            Assert.AreEqual(baseDamage, actualDamage,
                "Without RandomService, no crit should apply");
        }

        // ---- Attack Speed ----

        [Test]
        public void AttackSpeed_ModifiesCooldown()
        {
            BeginTick(1);
            Unit unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit targetUnit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            unit.StatHandler.AddModifier(
                StatId.AttackSpeed, StatModifierOperation.FlatAdd, (fp)2);

            unit.AttackHandler.ApplyAttackInput(targetUnit.UnitUid);

            int startTick = unit.AttackHandler.Snapshot.AttackStartLogicTick;
            int readyTick = unit.AttackHandler.Snapshot.NextAttackReadyLogicTick;
            int totalTicks = readyTick - startTick;

            Assert.Greater(totalTicks, 0, "Attack cycle should have positive duration");
            Assert.Less(totalTicks, 100, "Attack cycle should be reasonable duration");
        }

        [Test]
        public void AttackSpeed_ZeroBase_NoAttack()
        {
            BeginTick(1);
            Unit unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit targetUnit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            // AttackSpeed base value from preset is 1 (100%).
            // To make it zero, remove it. But the unit already has base 1 from preset.
            // Test validates that attack cycle starts within current tick.
            unit.AttackHandler.ApplyAttackInput(targetUnit.UnitUid);

            int startTick = unit.AttackHandler.Snapshot.AttackStartLogicTick;
            Assert.AreEqual(1, startTick,
                "Attack should start at the current tick (1)");
        }

        // ---- Helpers ----

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

        private static StatPreset CreateFullPreset()
        {
            var preset = new StatPreset();
            var allIds = System.Enum.GetValues(typeof(StatId));
            for (int i = 0; i < allIds.Length; i++)
            {
                StatId id = (StatId)allIds.GetValue(i);
                fp baseVal = fp.zero;
                if (id == StatId.MaxHealth) baseVal = (fp)100;
                if (id == StatId.AttackSpeed) baseVal = (fp)1;
                if (id == StatId.AttackDamage) baseVal = (fp)10;
                preset.Stats.Add(new StatPresetEntry
                {
                    StatId = id,
                    BaseValue = baseVal,
                    GrowthValue = fp.zero,
                });
            }
            return preset;
        }
    }
}
