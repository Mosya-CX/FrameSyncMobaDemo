using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class AttackHandlerTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private SimulationTickContextController _controller;
        private Unit _attacker;
        private Unit _target;

        [SetUp]
        public void SetUp()
        {
            _controller = new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable = CreateAttackStatTable()
            };
            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "Attacker",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateAttackPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
        }

        [TearDown]
        public void TearDown()
        {
            _controller.EndTick();
        }

        private void BeginTick(int tick)
        {
            _controller.BeginTick(tick, ExecutionMode.ServerAuthority);
        }

        [Test]
        public void SpawnUnit_CreatesAttackHandler()
        {
            BeginTick(1);
            var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.AttackHandler);
            Assert.IsFalse(unit.AttackHandler.ImpactCommitted);
            Assert.AreEqual(default(UnitUid), unit.AttackHandler.CurrentTargetUid);
        }

        [Test]
        public void ApplyAttackInput_SetsTargetAndCycle()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);

            Assert.AreEqual(_target.UnitUid, _attacker.AttackHandler.CurrentTargetUid);
            Assert.IsFalse(_attacker.AttackHandler.ImpactCommitted);
            Assert.Greater(_attacker.AttackHandler.AttackSequenceIndex, 0);
        }

        [Test]
        public void ApplyAttackInput_CannotStartWhenCooldownActive()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            byte firstSeq = _attacker.AttackHandler.AttackSequenceIndex;

            // Same tick: cannot start another attack (cooldown from first)
            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            Assert.AreEqual(firstSeq, _attacker.AttackHandler.AttackSequenceIndex);
        }

        [Test]
        public void TickUpdate_ProducesDamageRequestOnImpact()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            // Formal attack timing clamps windup to at least one Logic Tick.
            _attacker.AttackHandler.WindupRatio = fp.zero;

            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            _controller.EndTick();
            BeginTick(11);
            var damage = _attacker.AttackHandler.TickUpdate();

            Assert.IsTrue(damage.HasValue);
            Assert.AreEqual(_attacker.UnitUid, damage.Value.SourceUnitUid);
            Assert.AreEqual(_target.UnitUid, damage.Value.TargetUnitUid);
            Assert.Greater(damage.Value.BaseDamage, fp.zero);
        }

        [Test]
        public void TickUpdate_OnlyCommitsOnce()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.WindupRatio = fp.zero;
            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            _controller.EndTick();
            BeginTick(11);

            var first = _attacker.AttackHandler.TickUpdate();
            Assert.IsTrue(first.HasValue);

            // Second call in same cycle should return null
            var second = _attacker.AttackHandler.TickUpdate();
            Assert.IsFalse(second.HasValue);
        }

        [Test]
        public void CaptureRestore_RoundTrip_PreservesState()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);

            AttackSnapshot captured = default;
            _attacker.AttackHandler.Capture(ref captured);

            var restored = UnitTestFactory.CreateAttackHandler(_attacker);
            restored.Restore(captured);

            Assert.AreEqual(
                _attacker.AttackHandler.CurrentTargetUid,
                restored.CurrentTargetUid);
            Assert.AreEqual(
                _attacker.AttackHandler.AttackSequenceIndex,
                restored.AttackSequenceIndex);
        }

        [Test]
        public void Resolve_RejectsMissingTargetReference()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(
                _prototype, new TeamId(1), 10, 0m, 0m);
            _target = _world.SpawnUnit(
                _prototype, new TeamId(2), 10, 0m, 0m);
            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            _world.UnregisterUnit(_target);

            Assert.Throws<DeterministicSimulationException>(
                () => _attacker.AttackHandler.Resolve(default));
        }

        [Test]
        public void Restore_ClearsImpactFlag()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.WindupRatio = fp.zero;
            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            _controller.EndTick();
            BeginTick(11);
            _attacker.AttackHandler.TickUpdate();

            AttackSnapshot snapped = default;
            _attacker.AttackHandler.Capture(ref snapped);
            Assert.IsTrue(snapped.ImpactCommitted);

            _attacker.AttackHandler.Restore(AttackSnapshot.Default);
            Assert.IsFalse(_attacker.AttackHandler.ImpactCommitted);
        }

        [Test]
        public void Death_CancelsAttack()
        {
            BeginTick(10);
            _attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            _target = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            _attacker.AttackHandler.ApplyAttackInput(_target.UnitUid);
            Assert.IsTrue(_attacker.AttackHandler.CurrentTargetUid.IsValid());

            _attacker.ClearForDeath();

            Assert.IsFalse(_attacker.AttackHandler.CurrentTargetUid.IsValid());
            Assert.IsFalse(_attacker.AttackHandler.ImpactCommitted);
        }

        [Test]
        public void PoolReset_PreservesAttackComponentAndClearsRuntimeState()
        {
            BeginTick(1);
            var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            Assert.IsNotNull(unit.AttackHandler);

            unit.ResetForPool();
            Assert.IsNotNull(unit.AttackHandler);
            Assert.AreEqual(AttackSnapshot.Default.CurrentTargetUid, unit.AttackHandler.CurrentTargetUid);
        }

        [Test]
        public void Deterministic_SameInputSameResult()
        {
            BeginTick(10);
            var a1 = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            var a2 = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            var t1 = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
            var t2 = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);

            a1.AttackHandler.WindupRatio = new fp(20) / new fp(100);
            a2.AttackHandler.WindupRatio = new fp(20) / new fp(100);

            a1.AttackHandler.ApplyAttackInput(t1.UnitUid);
            a2.AttackHandler.ApplyAttackInput(t2.UnitUid);

            // Both should produce identical attack timing
            Assert.AreEqual(
                a1.AttackHandler.AttackSequenceIndex,
                a2.AttackHandler.AttackSequenceIndex);
        }

        private static StatDefinitionTable CreateAttackStatTable()
        {
            var table = new StatDefinitionTable();
            table.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackSpeed,
                DebugName = "AS",
                DefaultBaseValue = 0.625m,
                SupportsLevelGrowth = false,
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
            return table;
        }

        private static StatPreset CreateAttackPreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 0.625m,
                GrowthValue = 0m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 30m,
                GrowthValue = 0m,
            });
            return preset;
        }
    }
}
