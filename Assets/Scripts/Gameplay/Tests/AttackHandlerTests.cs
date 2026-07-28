using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class AttackHandlerTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private UnitPrototype prototype;
        private Unit attacker;
        private Unit target;
        private CombatSystem combat;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);
            world = new UnitWorld
            {
                StatDefinitionTable = CreateStatTable(),
                AttackSequenceResetIntervalTicks = 3,
            };
            prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestAttacker",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                BaseStats = CreateStatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
                Loadout = HandlerLoadout.DefaultHero,
            };

            attacker = world.SpawnUnit(
                prototype, new TeamId(1), 10, fp.zero, fp.zero);
            target = world.SpawnUnit(
                prototype, new TeamId(2), 10, fp.zero, fp.zero);
            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            combat.BeginTick();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void BeginAndCancel_DoNotConsumeSequence()
        {
            Assert.AreEqual(
                AttackPlanStatus.Ready,
                attacker.AttackHandler.GetAttackPlanStatus(target.UnitUid));

            attacker.AttackHandler.BeginAttack(target.UnitUid);

            Assert.AreEqual(target.UnitUid, attacker.AttackHandler.CurrentTargetUid);
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);

            attacker.AttackHandler.CancelBeforeCommit();

            Assert.IsFalse(attacker.AttackHandler.CurrentTargetUid.IsValid());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
        }

        [Test]
        public void SuccessfulImpact_SubmitsCombatAndConsumesOneSequence()
        {
            int onHitCount = 0;
            OnHitEventData onHit = default;
            CombatEvents.OnHitDealt += data =>
            {
                onHitCount++;
                onHit = data;
            };

            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);

            attacker.AttackHandler.TickUpdate();
            attacker.AttackHandler.TickUpdate();

            Assert.IsTrue(attacker.AttackHandler.ImpactCommitted);
            Assert.AreEqual(1, attacker.AttackHandler.AttackSequenceIndex);
            combat.SettleActiveRequests();
            Assert.AreEqual(1, combat.DamageProcessed);
            Assert.AreEqual(1, onHitCount);
            Assert.AreEqual(attacker.UnitUid, onHit.SourceUid);
            Assert.AreEqual(target.UnitUid, onHit.TargetUid);
        }

        [Test]
        public void MissingCombatOutput_CancelsWithoutConsumingSequence()
        {
            world.CombatSystem = null;
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);

            attacker.AttackHandler.TickUpdate();

            Assert.IsFalse(attacker.AttackHandler.ImpactCommitted);
            Assert.IsFalse(attacker.AttackHandler.CurrentTargetUid.IsValid());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
        }

        [Test]
        public void SuccessfulSequence_WrapsFromMaxValueToZero()
        {
            AdvanceTo(11);
            var state = new AttackSnapshot
            {
                CurrentTargetUid = target.UnitUid,
                AttackStartLogicTick = 10,
                ImpactLogicTick = 11,
                NextAttackReadyLogicTick = 11,
                AttackSequenceIndex = byte.MaxValue,
                LastSuccessfulAttackLogicTick = -1,
                ResolvedAttackDurationTicks = 1,
                ResolvedWindupTicks = 1,
            };
            attacker.AttackHandler.Restore(state);

            Assert.IsTrue(attacker.AttackHandler.CommitAttack());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
            combat.SettleActiveRequests();
        }

        [Test]
        public void SequenceReset_IsLazyAndOccursBeforeNextBegin()
        {
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);
            Assert.IsTrue(attacker.AttackHandler.CommitAttack());
            combat.SettleActiveRequests();
            AttackSnapshot committed = Capture(attacker.AttackHandler);
            Assert.AreEqual(1, committed.AttackSequenceIndex);

            AdvanceTo(System.Math.Max(
                committed.NextAttackReadyLogicTick,
                committed.LastSuccessfulAttackLogicTick + 3));
            attacker.AttackHandler.BeginAttack(target.UnitUid);

            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
            Assert.IsFalse(attacker.AttackHandler.ImpactCommitted);
        }

        [Test]
        public void CaptureRestore_PreservesResolvedTimeline()
        {
            attacker.AttackHandler.WindupRatio = (fp)1 / (fp)4;
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot captured = Capture(attacker.AttackHandler);

            AttackHandler restored = UnitTestFactory.CreateAttackHandler(attacker);
            restored.Restore(captured);
            AttackSnapshot roundTrip = Capture(restored);

            Assert.AreEqual(captured.CurrentTargetUid, roundTrip.CurrentTargetUid);
            Assert.AreEqual(
                captured.ResolvedAttackDurationTicks,
                roundTrip.ResolvedAttackDurationTicks);
            Assert.AreEqual(
                captured.ResolvedWindupTicks,
                roundTrip.ResolvedWindupTicks);
            Assert.AreEqual(captured.ImpactLogicTick, roundTrip.ImpactLogicTick);
            Assert.AreEqual(
                captured.NextAttackReadyLogicTick,
                roundTrip.NextAttackReadyLogicTick);
        }

        [Test]
        public void Resolve_RejectsMissingActiveTarget()
        {
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            world.UnregisterUnit(target);

            Assert.Throws<DeterministicSimulationException>(
                () => attacker.AttackHandler.Resolve(default));
        }

        private void AdvanceTo(int tick)
        {
            controller.EndTick();
            controller.BeginTick(tick, ExecutionMode.ServerAuthority);
            combat.BeginTick();
        }

        private static AttackSnapshot Capture(AttackHandler handler)
        {
            AttackSnapshot state = default;
            handler.Capture(ref state);
            return state;
        }

        private static StatDefinitionTable CreateStatTable()
        {
            var table = new StatDefinitionTable();
            AddDefinition(table, StatId.AttackDamage);
            AddDefinition(table, StatId.MaxHealth);
            AddDefinition(table, StatId.AttackSpeed);
            AddDefinition(table, StatId.AttackRange);
            AddDefinition(table, StatId.Armor);
            return table;
        }

        private static void AddDefinition(
            StatDefinitionTable table,
            StatId id)
        {
            table.Add(new StatDefinition
            {
                Id = id,
                DebugName = id.ToString(),
                DefaultBaseValue = fp.zero,
                SupportsLevelGrowth = true,
            });
        }

        private static StatPreset CreateStatPreset()
        {
            var preset = new StatPreset();
            AddStat(preset, StatId.AttackDamage, (fp)100);
            AddStat(preset, StatId.MaxHealth, (fp)500);
            AddStat(preset, StatId.AttackSpeed, (fp)30);
            AddStat(preset, StatId.AttackRange, (fp)2);
            AddStat(preset, StatId.Armor, fp.zero);
            return preset;
        }

        private static void AddStat(
            StatPreset preset,
            StatId id,
            fp value)
        {
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = id,
                BaseValue = value,
                GrowthValue = fp.zero,
            });
        }
    }
}
