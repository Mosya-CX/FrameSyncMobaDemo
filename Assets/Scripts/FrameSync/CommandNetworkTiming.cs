using System;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Client-transport estimate consumed only while a new Command header is
    /// created. Once chosen, TargetTick remains canonical and is never
    /// recomputed during resend, rollback or replay.
    /// </summary>
    public readonly struct CommandNetworkTiming
    {
        public readonly int EstimatedServerTickNow;
        public readonly int NetworkBudgetTicks;
        public readonly int DesiredServerSlackTicks;

        public CommandNetworkTiming(
            int estimatedServerTickNow,
            int networkBudgetTicks,
            int desiredServerSlackTicks)
        {
            if (estimatedServerTickNow < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(estimatedServerTickNow));
            if (networkBudgetTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(networkBudgetTicks));
            if (desiredServerSlackTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(desiredServerSlackTicks));

            EstimatedServerTickNow = estimatedServerTickNow;
            NetworkBudgetTicks = networkBudgetTicks;
            DesiredServerSlackTicks = desiredServerSlackTicks;
        }
    }

    /// <summary>
    /// Transport-neutral boundary. Bootstrap may use monotonic network timing;
    /// FrameSync and Gameplay never reference NGO or wall-clock APIs.
    /// </summary>
    public interface ICommandNetworkTimingProvider
    {
        bool TryGetCommandNetworkTiming(
            out CommandNetworkTiming timing);
    }
}
