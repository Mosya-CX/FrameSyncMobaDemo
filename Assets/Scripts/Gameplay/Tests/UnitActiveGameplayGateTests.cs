using System;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitActiveGameplayGateTests
    {
        private SimulationTickContextController controller;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
        }

        [TestCase(ExecutionMode.ServerAuthority)]
        [TestCase(ExecutionMode.ClientPrediction)]
        [TestCase(ExecutionMode.ClientReplay)]
        public void TickEqualToSpawnLogicTick_IsInactive(ExecutionMode executionMode)
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(1000, executionMode);

            Assert.That(unit.CanRunActiveGameplayThisTick, Is.False);
        }

        [TestCase(ExecutionMode.ServerAuthority)]
        [TestCase(ExecutionMode.ClientPrediction)]
        [TestCase(ExecutionMode.ClientReplay)]
        public void TickOneGreaterThanSpawnLogicTick_IsActive(ExecutionMode executionMode)
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(1001, executionMode);

            Assert.That(unit.CanRunActiveGameplayThisTick, Is.True);
        }

        [Test]
        public void TickBeforeSpawnLogicTick_IsInactive()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(999, ExecutionMode.ServerAuthority);

            Assert.That(unit.CanRunActiveGameplayThisTick, Is.False);
        }

        [Test]
        public void TickFarAfterSpawnLogicTick_RemainsActive()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(5000, ExecutionMode.ClientPrediction);

            Assert.That(unit.CanRunActiveGameplayThisTick, Is.True);
        }

        [Test]
        public void AllThreeExecutionModes_AgreeForSameTickAndUid()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 42,
                runtimeEntityPrefabId: 7,
                spawnSequenceInTick: 3), UnitKind.Minion, 0, TeamId.Neutral);

            bool server = Evaluate(ExecutionMode.ServerAuthority, 43, unit);
            bool prediction = Evaluate(ExecutionMode.ClientPrediction, 43, unit);
            bool replay = Evaluate(ExecutionMode.ClientReplay, 43, unit);

            Assert.That(server, Is.True);
            Assert.That(prediction, Is.True);
            Assert.That(replay, Is.True);
        }

        [Test]
        public void RepeatedReads_DoNotMutateUnitOrContext()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(1001, ExecutionMode.ServerAuthority);

            bool first = unit.CanRunActiveGameplayThisTick;
            bool second = unit.CanRunActiveGameplayThisTick;
            bool third = unit.CanRunActiveGameplayThisTick;

            Assert.That(new[] { first, second, third }, Is.All.True);
            Assert.That(SimulationTickContext.Current.Tick, Is.EqualTo(1001));
            Assert.That(unit.UnitUid.SpawnLogicTick, Is.EqualTo(1000));
        }

        [Test]
        public void OutsideActiveTick_ThrowsThroughContextOwnership()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);

            Assert.Throws<InvalidOperationException>(
                () => _ = unit.CanRunActiveGameplayThisTick);
        }

        [Test]
        public void DifferentSpawnLogicTicksAtSameCurrentTick_RespectIndividualGates()
        {
            var spawnTickUnit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            var earlierUnit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 999,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(1000, ExecutionMode.ServerAuthority);

            Assert.That(spawnTickUnit.CanRunActiveGameplayThisTick, Is.False);
            Assert.That(earlierUnit.CanRunActiveGameplayThisTick, Is.True);
        }

        private bool Evaluate(ExecutionMode executionMode, int tick, Unit unit)
        {
            controller.BeginTick(tick, executionMode);
            try
            {
                return unit.CanRunActiveGameplayThisTick;
            }
            finally
            {
                controller.EndTick();
            }
        }
    }
}