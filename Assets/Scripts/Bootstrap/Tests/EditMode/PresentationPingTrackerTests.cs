using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class PresentationPingTrackerTests
    {
        [Test]
        public void TryBegin_UsesConfiguredHalfSecondCadence()
        {
            var tracker = new PresentationPingTracker(500);

            Assert.IsTrue(tracker.TryBegin(10_000, out uint first));
            Assert.AreEqual(1u, first);
            Assert.IsFalse(tracker.TryBegin(10_499, out _));
            Assert.IsTrue(tracker.TryBegin(10_500, out uint second));
            Assert.AreEqual(2u, second);
        }

        [Test]
        public void TryComplete_IgnoresStaleReplyAndMeasuresLatestRoundTrip()
        {
            var tracker = new PresentationPingTracker(500);
            Assert.IsTrue(tracker.TryBegin(1_000, out uint first));
            Assert.IsTrue(tracker.TryBegin(1_500, out uint second));

            Assert.IsFalse(tracker.TryComplete(first, 1_600));
            Assert.AreEqual(-1, tracker.LatestRoundTripMilliseconds);
            Assert.IsTrue(tracker.TryComplete(second, 1_625));
            Assert.AreEqual(125, tracker.LatestRoundTripMilliseconds);
        }

        [Test]
        public void CompletedSamples_UseIntegerSmoothedRttAndVariation()
        {
            var tracker = new PresentationPingTracker(500);

            Complete(tracker, 0, 80, 97);
            Assert.AreEqual(80, tracker.SmoothedRoundTripMilliseconds);
            Assert.AreEqual(40, tracker.RoundTripVariationMilliseconds);

            Complete(tracker, 500, 100, 98);
            Assert.AreEqual(83, tracker.SmoothedRoundTripMilliseconds);
            Assert.AreEqual(35, tracker.RoundTripVariationMilliseconds);

            Complete(tracker, 1000, 70, 99);
            Assert.AreEqual(81, tracker.SmoothedRoundTripMilliseconds);
            Assert.AreEqual(30, tracker.RoundTripVariationMilliseconds);

            Complete(tracker, 1500, 90, 100);
            Assert.AreEqual(82, tracker.SmoothedRoundTripMilliseconds);
            Assert.AreEqual(25, tracker.RoundTripVariationMilliseconds);
            Assert.AreEqual(4, tracker.CompletedSampleCount);
        }

        [Test]
        public void CommandTiming_RequiresFreshMinimumSamplesAndCeilsTicks()
        {
            var tracker = new PresentationPingTracker(500);
            Complete(tracker, 0, 80, 97);
            Complete(tracker, 500, 100, 98);
            Complete(tracker, 1000, 70, 99);

            Assert.IsFalse(tracker.TryBuildCommandNetworkTiming(
                1070, 50, 4, 3000, 10, 10, 2, 1, out _));

            Complete(tracker, 1500, 90, 100);
            Assert.IsTrue(tracker.TryBuildCommandNetworkTiming(
                1590,
                50,
                4,
                3000,
                10,
                10,
                2,
                1,
                out FrameSyncMoba.FrameSync.CommandNetworkTiming timing));
            Assert.AreEqual(103, timing.EstimatedServerTickNow);
            Assert.AreEqual(6, timing.NetworkBudgetTicks);
            Assert.AreEqual(1, timing.DesiredServerSlackTicks);

            Assert.IsFalse(tracker.TryBuildCommandNetworkTiming(
                4591, 50, 4, 3000, 10, 10, 2, 1, out _));
        }

        [Test]
        public void FiveSecondLaunchBarrier_WarmsFirstCommandTiming()
        {
            var tracker = new PresentationPingTracker(500);
            tracker.ScheduleServerGameplayActivation(5000);
            for (int i = 0; i < 10; i++)
            {
                Complete(
                    tracker,
                    i * 500L,
                    80,
                    100);
            }

            Assert.AreEqual(10, tracker.CompletedSampleCount);
            Assert.IsTrue(tracker.TryBuildCommandNetworkTiming(
                5000,
                50,
                4,
                3000,
                10,
                10,
                2,
                1,
                out FrameSyncMoba.FrameSync.CommandNetworkTiming timing));
            Assert.AreEqual(102, timing.EstimatedServerTickNow);

            Assert.IsTrue(tracker.TryBuildCommandNetworkTiming(
                5100,
                50,
                4,
                3000,
                10,
                10,
                2,
                1,
                out timing));
            Assert.AreEqual(107, timing.EstimatedServerTickNow);
        }

        [Test]
        public void ActivationScheduledBeforeBridgeBind_IsCachedSafely()
        {
            var root = new GameObject("TimingBridgeLifecycleTest");
            try
            {
                FrameSyncNetworkBridge bridge =
                    root.AddComponent<FrameSyncNetworkBridge>();
                Assert.DoesNotThrow(() =>
                    bridge.ScheduleServerGameplayActivation(5000));
                Assert.DoesNotThrow(() =>
                    bridge.ScheduleServerGameplayActivation(5000));
                Assert.Throws<System.InvalidOperationException>(() =>
                    bridge.ScheduleServerGameplayActivation(5001));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Complete(
            PresentationPingTracker tracker,
            long sentAt,
            int roundTripMilliseconds,
            int serverTick)
        {
            Assert.IsTrue(tracker.TryBegin(sentAt, out uint sequence));
            Assert.IsTrue(tracker.TryComplete(
                sequence,
                sentAt + roundTripMilliseconds,
                serverTick));
        }
    }
}
