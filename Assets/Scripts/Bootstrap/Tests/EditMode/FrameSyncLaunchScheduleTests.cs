using System;
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
            long sentUtcTicks = new DateTime(
                2026,
                8,
                14,
                0,
                0,
                0,
                DateTimeKind.Utc).Ticks;
            long serverLaunchUtcTicks = sentUtcTicks +
                5L * TimeSpan.TicksPerSecond;
            long receivedUtcTicks = sentUtcTicks +
                1200L * TimeSpan.TicksPerMillisecond;

            long clientLaunchUtcTicks =
                FrameSyncLaunchSchedule
                    .GetClientPredictionLaunchUtcTicks(
                        serverLaunchUtcTicks,
                        tickRate,
                        leadTicks);
            long remainingUtcTicks =
                clientLaunchUtcTicks - receivedUtcTicks;

            long expectedLeadUtcTicks =
                leadTicks * TimeSpan.TicksPerSecond /
                tickRate;
            Assert.That(
                remainingUtcTicks,
                Is.EqualTo(
                    5L * TimeSpan.TicksPerSecond -
                    1200L * TimeSpan.TicksPerMillisecond -
                    expectedLeadUtcTicks));
        }

        [Test]
        public void WallClockLimit_NeverAllowsRunawayPrediction()
        {
            const int startTick = 3;
            const int tickRate = 30;
            const int leadTicks = 5;
            long launchUtcTicks = new DateTime(
                2026,
                8,
                14,
                0,
                0,
                5,
                DateTimeKind.Utc).Ticks;

            Assert.That(
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        startTick,
                        launchUtcTicks,
                        launchUtcTicks -
                        TimeSpan.TicksPerSecond,
                        tickRate,
                        leadTicks),
                Is.EqualTo(startTick + leadTicks));
            Assert.That(
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        startTick,
                        launchUtcTicks,
                        launchUtcTicks +
                        2L * TimeSpan.TicksPerSecond,
                        tickRate,
                        leadTicks),
                Is.EqualTo(startTick + 60 + leadTicks));
        }

        [Test]
        public void ClientLaunch_OpensExactlyAtLeadBoundary()
        {
            const int tickRate = 30;
            const int leadTicks = 5;
            long serverLaunchUtcTicks =
                DateTime.UtcNow.Ticks +
                5L * TimeSpan.TicksPerSecond;
            long clientLaunchUtcTicks =
                FrameSyncLaunchSchedule
                    .GetClientPredictionLaunchUtcTicks(
                        serverLaunchUtcTicks,
                        tickRate,
                        leadTicks);

            Assert.That(
                FrameSyncLaunchSchedule
                    .IsClientPredictionLaunchReached(
                        clientLaunchUtcTicks - 1,
                        serverLaunchUtcTicks,
                        tickRate,
                        leadTicks),
                Is.False);
            Assert.That(
                FrameSyncLaunchSchedule
                    .IsClientPredictionLaunchReached(
                        clientLaunchUtcTicks,
                        serverLaunchUtcTicks,
                        tickRate,
                        leadTicks),
                Is.True);
        }
    }
}
