using System;
using NUnit.Framework;

namespace FrameSyncMoba.Deterministic.Tests
{
    public sealed class SimulationTickContextTests
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

        [Test]
        public void Current_OutsideActiveTick_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _ = SimulationTickContext.Current);
        }

        [Test]
        public void ExecutionMode_ValuesAreStable()
        {
            Assert.That((int)ExecutionMode.ServerAuthority, Is.EqualTo(0));
            Assert.That((int)ExecutionMode.ClientPrediction, Is.EqualTo(1));
            Assert.That((int)ExecutionMode.ClientReplay, Is.EqualTo(2));
        }

        [TestCase(ExecutionMode.ServerAuthority)]
        [TestCase(ExecutionMode.ClientPrediction)]
        [TestCase(ExecutionMode.ClientReplay)]
        public void BeginTick_PublishesImmutableRequestedContext(ExecutionMode executionMode)
        {
            controller.BeginTick(42, executionMode);

            SimulationTickContext context = SimulationTickContext.Current;

            Assert.That(context.Tick, Is.EqualTo(42));
            Assert.That(context.DeltaTick, Is.EqualTo(1));
            Assert.That(context.ExecutionMode, Is.EqualTo(executionMode));
            Assert.That(controller.IsTickActive, Is.True);
        }

        [Test]
        public void BeginTick_WhenAlreadyActive_RejectsNestedTickWithoutReplacingCurrent()
        {
            controller.BeginTick(7, ExecutionMode.ServerAuthority);
            SimulationTickContext original = SimulationTickContext.Current;

            Assert.Throws<InvalidOperationException>(
                () => controller.BeginTick(8, ExecutionMode.ClientReplay));

            Assert.That(SimulationTickContext.Current, Is.EqualTo(original));
        }

        [Test]
        public void SecondController_CannotReplaceActiveContext()
        {
            controller.BeginTick(12, ExecutionMode.ClientPrediction);
            var secondController = new SimulationTickContextController();

            Assert.Throws<InvalidOperationException>(
                () => secondController.BeginTick(99, ExecutionMode.ServerAuthority));

            Assert.That(SimulationTickContext.Current.Tick, Is.EqualTo(12));
            Assert.That(secondController.IsTickActive, Is.False);
        }

        [Test]
        public void EndTick_ClearsCurrent()
        {
            controller.BeginTick(3, ExecutionMode.ClientReplay);

            controller.EndTick();

            Assert.That(controller.IsTickActive, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = SimulationTickContext.Current);
        }

        [Test]
        public void EndTick_WithoutOwnership_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => controller.EndTick());
        }
    }
}
