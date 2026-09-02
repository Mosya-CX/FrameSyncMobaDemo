using FrameSyncMoba.Deterministic;
using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class CommandTargetTickResolverAdaptiveTests
    {
        [Test]
        public void AvailableTiming_AddsEstimatedArrivalAndSlackLowerBound()
        {
            var timing = new FakeTimingProvider(
                new CommandNetworkTiming(22, 4, 1));
            var resolver = new CommandTargetTickResolver(
                () => 20,
                () => 19,
                1,
                12,
                timing);

            Assert.AreEqual(
                27,
                resolver.ResolveTargetTick(out int buildTick));
            Assert.AreEqual(20, buildTick);
        }

        [Test]
        public void UnavailableTiming_FallsBackToStaticFormalFormula()
        {
            var timing = new FakeTimingProvider(
                new CommandNetworkTiming(30, 5, 1),
                false);
            var resolver = new CommandTargetTickResolver(
                () => 20,
                () => 23,
                3,
                12,
                timing);

            Assert.AreEqual(
                26,
                resolver.ResolveTargetTick(out int buildTick));
            Assert.AreEqual(20, buildTick);
        }

        [Test]
        public void InvalidInitialLocalTick_DoesNotHitEmptyCache()
        {
            var resolver = new CommandTargetTickResolver(
                () => -1,
                () => -1,
                1,
                12);

            Assert.Throws<DeterministicSimulationException>(
                () => resolver.ResolveTargetTick(out _));
        }

        [Test]
        public void SameBuildTick_ReusesOneTimingDecision()
        {
            int localTick = 20;
            var timing = new FakeTimingProvider(
                new CommandNetworkTiming(22, 4, 1));
            var resolver = new CommandTargetTickResolver(
                () => localTick,
                () => 19,
                1,
                12,
                timing);

            Assert.AreEqual(27, resolver.ResolveTargetTick(out _));
            timing.Value = new CommandNetworkTiming(25, 4, 1);
            Assert.AreEqual(27, resolver.ResolveTargetTick(out _));
            Assert.AreEqual(1, timing.CallCount);

            localTick = 21;
            Assert.AreEqual(30, resolver.ResolveTargetTick(out _));
            Assert.AreEqual(2, timing.CallCount);
        }

        [Test]
        public void AdaptiveTimingBeyondFutureWindow_UsesLatestLegalTick()
        {
            var timing = new FakeTimingProvider(
                new CommandNetworkTiming(30, 5, 1));
            var resolver = new CommandTargetTickResolver(
                () => 20,
                () => 19,
                1,
                12,
                timing);

            Assert.AreEqual(
                32,
                resolver.ResolveTargetTick(out int buildTick));
            Assert.AreEqual(20, buildTick);
        }

        [Test]
        public void StaticLowerBeyondEstimatedCeiling_RemainsAuthoritative()
        {
            var timing = new FakeTimingProvider(
                new CommandNetworkTiming(10, 1, 0));
            var resolver = new CommandTargetTickResolver(
                () => 20,
                () => 23,
                3,
                12,
                timing);

            Assert.AreEqual(
                26,
                resolver.ResolveTargetTick(out int buildTick));
            Assert.AreEqual(20, buildTick);
        }

        private sealed class FakeTimingProvider :
            ICommandNetworkTimingProvider
        {
            private readonly bool available;

            public CommandNetworkTiming Value { get; set; }
            public int CallCount { get; private set; }

            public FakeTimingProvider(
                CommandNetworkTiming value,
                bool available = true)
            {
                Value = value;
                this.available = available;
            }

            public bool TryGetCommandNetworkTiming(
                out CommandNetworkTiming timing)
            {
                CallCount++;
                timing = Value;
                return available;
            }
        }
    }
}
