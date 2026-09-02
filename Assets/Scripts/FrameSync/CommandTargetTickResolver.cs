using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Owns the formal local Command target-Tick formula. PlayerInput supplies
    /// intent only and never chooses a Tick itself. Network timing is an
    /// optional client-side lower bound; the static formula remains the
    /// cold-start and stale-sample fallback.
    /// </summary>
    public sealed class CommandTargetTickResolver
    {
        private readonly Func<int> localSimulationTickProvider;
        private readonly Func<int> latestSynchronizedServerTickProvider;
        private readonly int minCommandLeadTicks;
        private readonly int maxFutureCommandTicks;
        private readonly ICommandNetworkTimingProvider networkTimingProvider;
        private bool hasCachedTargetTick;
        private int cachedBuildLocalTick = -1;
        private int cachedTargetTick = -1;

        public CommandTargetTickResolver(
            Func<int> localSimulationTickProvider,
            Func<int> latestSynchronizedServerTickProvider,
            int minCommandLeadTicks,
            int maxFutureCommandTicks,
            ICommandNetworkTimingProvider networkTimingProvider = null)
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
            this.networkTimingProvider = networkTimingProvider;
        }

        public int ResolveTargetTick(out int buildLocalTick)
        {
            buildLocalTick = localSimulationTickProvider();
            if (hasCachedTargetTick &&
                buildLocalTick == cachedBuildLocalTick)
                return cachedTargetTick;

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
                    $"Static Command TargetTick {targetTick} exceeds local future window ending at {latestAllowedTick}.");
            }
            if (networkTimingProvider != null &&
                networkTimingProvider.TryGetCommandNetworkTiming(
                    out CommandNetworkTiming timing))
            {
                int adaptiveDesiredTick;
                int estimatedServerLatestAllowedTick;
                try
                {
                    adaptiveDesiredTick = checked(
                        timing.EstimatedServerTickNow +
                        timing.NetworkBudgetTicks +
                        timing.DesiredServerSlackTicks);
                    estimatedServerLatestAllowedTick = checked(
                        timing.EstimatedServerTickNow +
                        maxFutureCommandTicks);
                }
                catch (OverflowException exception)
                {
                    throw new DeterministicSimulationException(
                        $"Adaptive Command TargetTick arithmetic overflowed: {exception.Message}");
                }
                int adaptiveTick = Math.Min(
                    adaptiveDesiredTick,
                    Math.Min(
                        latestAllowedTick,
                        estimatedServerLatestAllowedTick));
                targetTick = Math.Max(targetTick, adaptiveTick);
            }
            hasCachedTargetTick = true;
            cachedBuildLocalTick = buildLocalTick;
            cachedTargetTick = targetTick;
            return targetTick;
        }
    }
}
