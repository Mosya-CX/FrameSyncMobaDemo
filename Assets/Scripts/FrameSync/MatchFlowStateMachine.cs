using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Observes MatchRuleRuntime from the bootstrap layer, gates Gameplay
    /// commands during PreGame/Countdown, and captures the final
    /// MatchResultSnapshot after the authority owner finishes the match.
    ///
    /// Design: FrameSync_Flow_Integrated_System_Design_v10_2 sections 2, 14
    /// </summary>
    public sealed class MatchFlowStateMachine
    {
        private readonly MatchRuleRuntime _rule;
        private bool _resultCaptured;

        /// <summary>
        /// True when the match has reached the Finished phase
        /// and the result is available.
        /// </summary>
        public bool HasFinished => _rule.CurrentPhase == MatchPhase.Finished;

        /// <summary>
        /// The final match result. Only valid when HasFinished is true.
        /// </summary>
        public MatchResultSnapshot Result { get; private set; }

        public MatchFlowStateMachine(MatchRuleRuntime rule)
        {
            _rule = rule ?? throw new System.ArgumentNullException(nameof(rule));
        }

        /// <summary>
        /// Observes a Tick already advanced by SimulationTickPipeline.
        /// This application-layer object never performs authority evaluation.
        /// </summary>
        public void ObserveTick()
        {
            if (_rule.CurrentPhase == MatchPhase.Finished && !_resultCaptured)
            {
                Result = new MatchResultSnapshot
                {
                    WinningTeamId = _rule.WinningTeamId,
                    EndReason = _rule.EndReason,
                    GameOverTick = _rule.GameOverTick,
                    FinishTick = _rule.FinishTick,
                    Statistics = CaptureStatistics(),
                };
                _resultCaptured = true;
            }
        }

        /// <summary>
        /// Whether the current phase allows Gameplay commands.
        /// Commands are gated during Preparing and Countdown.
        /// </summary>
        public bool AcceptsGameplayCommands =>
            _rule.CurrentPhase == MatchPhase.Running ||
            _rule.CurrentPhase == MatchPhase.Ending;

        private MatchStatisticsResult CaptureStatistics()
        {
            var entries = _rule.Statistics?.Entries;
            if (entries == null) return default;

            var result = new MatchStatisticsResult
            {
                Entries = new MatchStatisticsResultEntry[entries.Count],
            };

            for (int i = 0; i < entries.Count; i++)
            {
                result.Entries[i] = new MatchStatisticsResultEntry
                {
                    HeroUnitUid = entries[i].HeroUnitUid,
                    Kills = entries[i].Kills,
                    Deaths = entries[i].Deaths,
                    Assists = entries[i].Assists,
                };
            }

            return result;
        }
    }
}
