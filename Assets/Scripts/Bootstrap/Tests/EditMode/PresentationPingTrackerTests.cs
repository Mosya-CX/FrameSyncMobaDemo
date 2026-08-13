using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class PresentationPingTrackerTests
    {
        [Test]
        public void TryBegin_UsesConfiguredHalfSecondCadence()
        {
            var tracker = new PresentationPingTracker(0.5d);

            Assert.IsTrue(tracker.TryBegin(10d, out uint first));
            Assert.AreEqual(1u, first);
            Assert.IsFalse(tracker.TryBegin(10.499d, out _));
            Assert.IsTrue(tracker.TryBegin(10.5d, out uint second));
            Assert.AreEqual(2u, second);
        }

        [Test]
        public void TryComplete_IgnoresStaleReplyAndMeasuresLatestRoundTrip()
        {
            var tracker = new PresentationPingTracker(0.5d);
            Assert.IsTrue(tracker.TryBegin(1d, out uint first));
            Assert.IsTrue(tracker.TryBegin(1.5d, out uint second));

            Assert.IsFalse(tracker.TryComplete(first, 1.6d));
            Assert.AreEqual(-1, tracker.LatestRoundTripMilliseconds);
            Assert.IsTrue(tracker.TryComplete(second, 1.625d));
            Assert.AreEqual(125, tracker.LatestRoundTripMilliseconds);
        }
    }
}
