using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class FrameSyncLaunchScheduleTests
    {
        [Test]
        public void ClientWait_SubtractsTransitAndPredictionLead()
        {
            const int tickRate = 30;
            const int leadTicks = 5;
            const long sentServerMs = 10_000L;
            const long serverLaunchMs =
                sentServerMs + 5_000L;
            const long receivedServerMs =
                sentServerMs + 1_200L;

            long clientLaunchMs = FrameSyncLaunchSchedule
                .GetClientPredictionLaunchServerTimeMilliseconds(
                    serverLaunchMs,
                    tickRate,
                    leadTicks);

            Assert.That(
                clientLaunchMs - receivedServerMs,
                Is.EqualTo(
                    5_000L -
                    1_200L -
                    leadTicks * 1_000L /
                    tickRate));
        }

        [Test]
        public void MonotonicPacing_DoesNotInferBacklogFromLaunchTimestamp()
        {
            const int startTick = 3;
            const int tickRate = 30;
            const int leadTicks = 5;

            Assert.That(
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        startTick,
                        100_000L,
                        100_000L,
                        tickRate,
                        leadTicks,
                        -1),
                Is.EqualTo(startTick + leadTicks));
            Assert.That(
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        startTick,
                        100_000L,
                        102_000L,
                        tickRate,
                        leadTicks,
                        -1),
                Is.EqualTo(startTick + 60 + leadTicks));
        }

        [Test]
        public void AuthorityBacklog_AloneMayRaiseCatchUpLimit()
        {
            Assert.That(
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        3,
                        100_000L,
                        100_000L,
                        30,
                        5,
                        42),
                Is.EqualTo(43));
        }

        [Test]
        public void ClientLaunch_OpensExactlyAtLeadBoundary()
        {
            const int tickRate = 30;
            const int leadTicks = 5;
            const long serverLaunchMs = 15_000L;
            long clientLaunchMs = FrameSyncLaunchSchedule
                .GetClientPredictionLaunchServerTimeMilliseconds(
                    serverLaunchMs,
                    tickRate,
                    leadTicks);

            Assert.That(
                FrameSyncLaunchSchedule.IsEndpointLaunchReached(
                    clientLaunchMs - 1,
                    serverLaunchMs,
                    tickRate,
                    leadTicks,
                    false),
                Is.False);
            Assert.That(
                FrameSyncLaunchSchedule.IsEndpointLaunchReached(
                    clientLaunchMs,
                    serverLaunchMs,
                    tickRate,
                    leadTicks,
                    false),
                Is.True);
            Assert.That(
                FrameSyncLaunchSchedule.IsEndpointLaunchReached(
                    serverLaunchMs - 1,
                    serverLaunchMs,
                    tickRate,
                    leadTicks,
                    true),
                Is.False);
        }
    }
}
