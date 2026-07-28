using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Owns the formal local Command target-Tick formula from FrameSync v10.2.
    /// PlayerInput supplies intent only and never chooses a Tick itself.
    /// </summary>
    public sealed class CommandTargetTickResolver
    {
        private readonly Func<int> localSimulationTickProvider;
        private readonly Func<int> latestSynchronizedServerTickProvider;
        private readonly int minCommandLeadTicks;
        private readonly int maxFutureCommandTicks;

        public CommandTargetTickResolver(
            Func<int> localSimulationTickProvider,
            Func<int> latestSynchronizedServerTickProvider,
            int minCommandLeadTicks,
            int maxFutureCommandTicks)
        {
            this.localSimulationTickProvider = localSimulationTickProvider
                ?? throw new ArgumentNullException(
                    nameof(localSimulationTickProvider));
            this.latestSynchronizedServerTickProvider =
                latestSynchronizedServerTickProvider
                ?? throw new ArgumentNullException(
                    nameof(latestSynchronizedServerTickProvider));
            if (minCommandLeadTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(minCommandLeadTicks));
            if (maxFutureCommandTicks <= 0 ||
                minCommandLeadTicks > maxFutureCommandTicks)
                throw new ArgumentOutOfRangeException(
                    nameof(maxFutureCommandTicks));
            this.minCommandLeadTicks = minCommandLeadTicks;
            this.maxFutureCommandTicks = maxFutureCommandTicks;
        }

        public int ResolveTargetTick(out int buildLocalTick)
        {
            buildLocalTick = localSimulationTickProvider();
            int latestSynchronizedServerTick =
                latestSynchronizedServerTickProvider();
            if (buildLocalTick < 0 || latestSynchronizedServerTick < -1)
            {
                throw new DeterministicSimulationException(
                    "Command Tick sources must be non-negative, except the initial synchronized server Tick may be -1.");
            }

            int nextLocalTick;
            int leadTick;
            int latestAllowedTick;
            try
            {
                nextLocalTick = checked(buildLocalTick + 1);
                leadTick = checked(
                    latestSynchronizedServerTick + minCommandLeadTicks);
                latestAllowedTick = checked(
                    buildLocalTick + maxFutureCommandTicks);
            }
            catch (OverflowException exception)
            {
                throw new DeterministicSimulationException(
                    $"Command TargetTick arithmetic overflowed: {exception.Message}");
            }

            int targetTick = Math.Max(nextLocalTick, leadTick);
            if (targetTick > latestAllowedTick)
            {
                throw new DeterministicSimulationException(
                    $"Resolved Command TargetTick {targetTick} exceeds local future window ending at {latestAllowedTick}.");
            }
            return targetTick;
        }
    }
}
