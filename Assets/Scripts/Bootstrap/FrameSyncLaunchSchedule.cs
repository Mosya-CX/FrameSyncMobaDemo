using System;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Application scheduling for the wall-clock match launch. It only selects
    /// when deterministic ticks may run and never enters Gameplay state.
    /// </summary>
    public static class FrameSyncLaunchSchedule
    {
        public static long GetClientPredictionLaunchUtcTicks(
            long serverLaunchUtcTicks,
            int tickRate,
            int predictionLeadTicks)
        {
            Validate(tickRate, predictionLeadTicks);
            if (serverLaunchUtcTicks <= 0)
                return 0;
            return checked(
                serverLaunchUtcTicks -
                GetDurationUtcTicks(tickRate, predictionLeadTicks));
        }

        public static bool IsClientPredictionLaunchReached(
            long utcNowTicks,
            long serverLaunchUtcTicks,
            int tickRate,
            int predictionLeadTicks)
        {
            return serverLaunchUtcTicks <= 0 ||
                   utcNowTicks >= GetClientPredictionLaunchUtcTicks(
                       serverLaunchUtcTicks,
                       tickRate,
                       predictionLeadTicks);
        }

        /// <summary>
        /// Exclusive upper bound for LocalSimulationTick. Before the server
        /// launch the client may only pre-run its lead. Afterwards the bound
        /// advances at exactly TickRate.
        /// </summary>
        public static int GetMaximumClientSimulationTickExclusive(
            int startTick,
            long serverLaunchUtcTicks,
            long utcNowTicks,
            int tickRate,
            int predictionLeadTicks)
        {
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startTick));
            Validate(tickRate, predictionLeadTicks);

            long elapsedUtcTicks = serverLaunchUtcTicks <= 0
                ? 0L
                : Math.Max(0L, utcNowTicks - serverLaunchUtcTicks);
            long elapsedSeconds =
                elapsedUtcTicks / TimeSpan.TicksPerSecond;
            long elapsedRemainder =
                elapsedUtcTicks % TimeSpan.TicksPerSecond;
            long elapsedLogicTicks = checked(
                elapsedSeconds * tickRate +
                elapsedRemainder * tickRate /
                TimeSpan.TicksPerSecond);
            return checked(
                startTick +
                (int)elapsedLogicTicks +
                predictionLeadTicks);
        }

        private static long GetDurationUtcTicks(
            int tickRate,
            int logicTicks)
        {
            long seconds = logicTicks / tickRate;
            long remainder = logicTicks % tickRate;
            return checked(
                seconds * TimeSpan.TicksPerSecond +
                remainder * TimeSpan.TicksPerSecond /
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
