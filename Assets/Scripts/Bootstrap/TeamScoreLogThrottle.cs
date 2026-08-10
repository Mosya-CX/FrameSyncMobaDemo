using System.Collections.Generic;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Throttles the per-frame [Scoreboard] debug log: a line is emitted only
    /// when the scoreboard content actually changed (kills / deaths /
    /// assists / creep-kill deltas). Event logs stay available for analysis
    /// while identical per-frame repeats no longer flood the log file.
    /// </summary>
    internal sealed class TeamScoreLogThrottle
    {
        private readonly Dictionary<int, string>
            lastLineByRank =
                new Dictionary<int, string>();

        public bool ShouldLog(
            int rank,
            string line)
        {
            if (lastLineByRank.TryGetValue(
                    rank,
                    out string previous) &&
                previous == line)
            {
                return false;
            }
            lastLineByRank[rank] = line;
            return true;
        }
    }
}
