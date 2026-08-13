using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Client acknowledgement sent only after the authoritative bootstrap
    /// snapshot has been restored and the local controlled unit is bound.
    /// This is application-flow state and never enters Gameplay snapshots.
    /// </summary>
    public readonly struct BootstrapAppliedConfirmation
    {
        public readonly string MatchId;
        public readonly int StartTick;

        public BootstrapAppliedConfirmation(
            string matchId,
            int startTick)
        {
            MatchId = matchId;
            StartTick = startTick;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(MatchId) ||
                StartTick < 0)
                throw new DeterministicSimulationException(
                    "BootstrapAppliedConfirmation requires a valid MatchId and StartTick.");
        }
    }

    /// <summary>
    /// Server-authoritative wall-clock launch decision broadcast only after
    /// every assigned client confirms that bootstrap application completed.
    /// </summary>
    public readonly struct MatchLaunchCommit
    {
        public readonly string MatchId;
        public readonly int StartTick;
        public readonly long LaunchUtcTicks;

        public MatchLaunchCommit(
            string matchId,
            int startTick,
            long launchUtcTicks)
        {
            MatchId = matchId;
            StartTick = startTick;
            LaunchUtcTicks = launchUtcTicks;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(MatchId) ||
                StartTick < 0 ||
                LaunchUtcTicks <= 0)
                throw new DeterministicSimulationException(
                    "MatchLaunchCommit requires a valid MatchId, StartTick and LaunchUtcTicks.");
        }
    }
}
