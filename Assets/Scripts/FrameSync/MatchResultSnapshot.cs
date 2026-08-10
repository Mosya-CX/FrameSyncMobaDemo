using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Immutable snapshot of the final match result.
    /// Captures winner, end reason, tick timing, and per-player statistics.
    /// Consumed by the Result presentation screen.
    ///
    /// Design: FrameSync_Flow_Integrated_System_Design_v10_2 section 14.6
    /// </summary>
    public struct MatchResultSnapshot
    {
        /// <summary>
        /// The team that won. Neutral for draws.
        /// </summary>
        public TeamId WinningTeamId;

        /// <summary>
        /// Why the match ended (base destroyed, simultaneous destruction).
        /// </summary>
        public MatchEndReason EndReason;

        /// <summary>
        /// The tick when the game-ending condition was confirmed.
        /// </summary>
        public int GameOverTick;

        /// <summary>
        /// The tick when the Ending phase transitions to Finished.
        /// </summary>
        public int FinishTick;

        /// <summary>
        /// Duration in ticks from Countdown start to GameOver.
        /// </summary>
        public int DurationTicks => GameOverTick;

        /// <summary>
        /// Per-player KDA statistics at match end.
        /// </summary>
        public MatchStatisticsResult Statistics;

        public static readonly MatchResultSnapshot Empty = default;
    }

    /// <summary>
    /// Per-player match-end statistics.
    /// </summary>
    public struct MatchStatisticsResult
    {
        public MatchStatisticsResultEntry[] Entries;
    }

    /// <summary>
    /// Single player's match-end KDA.
    /// </summary>
    public struct MatchStatisticsResultEntry
    {
        public UnitUid HeroUnitUid;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int CreepKills;
    }
}
