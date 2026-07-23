using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class CombatSystemTests
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
                StatDefinitionTable = CreateCombatStatTable()
            };
            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestUnit",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateCombatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
            _combat = new CombatSystem(_world, 300, 60);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller.IsTickActive)
                _controller.EndTick();
        }

        private void BeginTick(int tick)
        {
            _controller.BeginTick(tick, ExecutionMode.ServerAuthority);
        }

        [Test]
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 100m,
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }

        [Test]
        public void DamageFormula_ArmorReducesDamage()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            // Target has 30 armor → reduction = 100/(100+30) ≈ 0.769
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 100m,
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp damageTaken = initialHealth - target.StatHandler.CurrentHealth;

            // With 30 armor, damage should be ~77
            Assert.Greater(damageTaken, 50m);
            Assert.Less(damageTaken, 100m);
        }

        [Test]
        public void ZeroArmor_FullDamageApplied()
        {
            BeginTick(1);
            var proto = CreateZeroArmorProto();
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(proto, TeamId.Neutral, 1, 0m, 0m);

            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 100m,
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp damageTaken = initialHealth - target.StatHandler.CurrentHealth;
            Assert.AreEqual((fp)100m, damageTaken);
        }

        [Test]
        public void FatalDamage_CompletesFormalDeathSettlement()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.AreEqual(LifeState.Alive, target.LifeState);

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 5000m, // Massive overkill
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(LifeState.Dead, target.LifeState);
        }

        [Test]
        public void FormalDeathResult_UsesFatalSourceAndUidSortedAssistants()
        {
            BeginTick(1);
            TeamId attackers = new TeamId(1);
            TeamId defenders = new TeamId(2);
            Unit smallerAssistant = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit largerAssistant = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit killer = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit victim = _world.SpawnUnit(
                _prototype, defenders, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = largerAssistant.UnitUid,
                TargetUnitUid = victim.UnitUid,
                BaseDamage = 50m,
                DamageType = DamageType.True,
            });
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = smallerAssistant.UnitUid,
                TargetUnitUid = victim.UnitUid,
                BaseDamage = 350m,
                DamageType = DamageType.True,
            });
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = killer.UnitUid,
                TargetUnitUid = victim.UnitUid,
                BaseDamage = 100m,
                DamageType = DamageType.True,
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(1, _combat.DeathResults.Count);
            DeathResult result = _combat.DeathResults[0];
            Assert.AreEqual(killer.UnitUid, result.KillerHeroUid);
            CollectionAssert.AreEqual(
                new[] { smallerAssistant.UnitUid, largerAssistant.UnitUid },
                result.AssistantHeroUids);
        }

        [Test]
        public void DeadUnit_DoesNotTakeDamage()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            // Transition target to Dead via proper lifecycle
            _world.RequestEnterDying(target);
            _world.ConfirmUnitDeath(target);
            Assert.AreEqual(LifeState.Dead, target.LifeState);

            fp healthBefore = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 100m,
            });
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(healthBefore, target.StatHandler.CurrentHealth);
        }

        [Test]
        public void InvalidRequest_IsIgnored()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(DamageRequest.None);
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.Pass();
        }

        [Test]
        public void CalculatePhysicalDamage_NegativeArmor_ClampedToZero()
        {
            fp damage = CombatSystem.CalculateResistedDamage(100m, -10m);
            Assert.AreEqual((fp)100m, damage);
        }

        [Test]
        public void CalculatePhysicalDamage_HighArmor_ReducedHeavily()
        {
            fp damage = CombatSystem.CalculateResistedDamage(100m, 200m);
            // 100 * (100 / 300) ≈ 33.3
            Assert.Greater(damage, 10m);
            Assert.Less(damage, 50m);
        }

        [Test]
        public void BeginTick_ClearsActiveQueue()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(new DamageRequest
            {
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = target.UnitUid,
                BaseDamage = 100m,
            });

            // BeginTick again clears without settling
            _combat.BeginTick();
            fp healthBefore = target.StatHandler.CurrentHealth;

            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(healthBefore, target.StatHandler.CurrentHealth);
        }

        private static StatDefinitionTable CreateCombatStatTable()
        {
            var table = new StatDefinitionTable();
            table.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.Armor,
                DebugName = "Armor",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = false,
                HasMinValue = true,
                MinValue = 0m,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackSpeed,
                DebugName = "AS",
                DefaultBaseValue = 0.625m,
                SupportsLevelGrowth = false,
            });
            return table;
        }

        private static StatPreset CreateCombatPreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 30m,
                GrowthValue = 0m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 0.625m,
                GrowthValue = 0m,
            });
            return preset;
        }

        private UnitPrototype CreateZeroArmorProto()
        {
            var proto = new UnitPrototype
            {
                UnitPrototypeId = 2,
                Name = "ZeroArmor",
                RuntimeEntityPrefabId = 101,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = new StatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 0m,
                GrowthValue = 0m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 0.625m,
                GrowthValue = 0m,
            });
            return proto;
        }
    }
}
