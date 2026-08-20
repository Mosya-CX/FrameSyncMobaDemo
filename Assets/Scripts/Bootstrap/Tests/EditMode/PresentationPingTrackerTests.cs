using NUnit.Framework;

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
    }
}
