using System;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Pure application-layer launch scheduling. Network time is used only
    /// to reach the common launch boundary. After that boundary, pacing uses
    /// a local monotonic clock and real authority backlog may independently
    /// authorize bounded catch-up.
    /// </summary>
    public static class FrameSyncLaunchSchedule
    {
        public const long MillisecondsPerSecond = 1_000L;

        public static long GetClientPredictionLaunchServerTimeMilliseconds(
            long serverLaunchTimeMilliseconds,
            int tickRate,
            int predictionLeadTicks)
        {
            Validate(tickRate, predictionLeadTicks);
            if (serverLaunchTimeMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(serverLaunchTimeMilliseconds));
            return checked(
                serverLaunchTimeMilliseconds -
                GetDurationMilliseconds(tickRate, predictionLeadTicks));
        }

        public static bool IsEndpointLaunchReached(
            long synchronizedServerTimeMilliseconds,
            long serverLaunchTimeMilliseconds,
            int tickRate,
            int predictionLeadTicks,
            bool isServer)
        {
            if (synchronizedServerTimeMilliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(synchronizedServerTimeMilliseconds));
            long endpointLaunch = isServer
                ? serverLaunchTimeMilliseconds
                : GetClientPredictionLaunchServerTimeMilliseconds(
                    serverLaunchTimeMilliseconds,
                    tickRate,
                    predictionLeadTicks);
            return synchronizedServerTimeMilliseconds >= endpointLaunch;
        }

        /// <summary>
        /// Exclusive upper bound for LocalSimulationTick after launch. A late
        /// timestamp never creates backlog. Only locally elapsed monotonic
        /// time or a genuinely received contiguous AuthorityFrame can raise
        /// the bound.
        /// </summary>
        public static int GetMaximumClientSimulationTickExclusive(
            int startTick,
            long launchMonotonicTimeMilliseconds,
            long monotonicNowMilliseconds,
            int tickRate,
            int predictionLeadTicks,
            int latestContiguousReceivedAuthorityFrameTick)
        {
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startTick));
            if (launchMonotonicTimeMilliseconds < 0 ||
                monotonicNowMilliseconds < launchMonotonicTimeMilliseconds)
                throw new ArgumentOutOfRangeException(
                    nameof(monotonicNowMilliseconds));
            Validate(tickRate, predictionLeadTicks);

            long elapsedMilliseconds =
                monotonicNowMilliseconds -
                launchMonotonicTimeMilliseconds;
            long elapsedLogicTicks = checked(
                elapsedMilliseconds * tickRate /
                MillisecondsPerSecond);
            int locallyPacedLimit = checked(
                startTick +
                (int)elapsedLogicTicks +
                predictionLeadTicks);
            int authorityBacklogLimit =
                latestContiguousReceivedAuthorityFrameTick < startTick
                    ? startTick
                    : checked(
                        latestContiguousReceivedAuthorityFrameTick + 1);
            return Math.Max(
                locallyPacedLimit,
                authorityBacklogLimit);
        }

        public static long SecondsToMilliseconds(double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds) ||
                seconds < 0d ||
                seconds > long.MaxValue /
                    (double)MillisecondsPerSecond)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return checked((long)Math.Round(
                seconds * MillisecondsPerSecond,
                MidpointRounding.AwayFromZero));
        }

        private static long GetDurationMilliseconds(
            int tickRate,
            int logicTicks)
        {
            long seconds = logicTicks / tickRate;
            long remainder = logicTicks % tickRate;
            return checked(
                seconds * MillisecondsPerSecond +
                remainder * MillisecondsPerSecond /
                tickRate);
        }

        private static void Validate(
            int tickRate,
            int predictionLeadTicks)
        {
            if (tickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            if (predictionLeadTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(predictionLeadTicks));
        }
    }
}
