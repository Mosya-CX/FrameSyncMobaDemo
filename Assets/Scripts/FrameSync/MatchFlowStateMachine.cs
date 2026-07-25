using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Wraps MatchRuleRuntime to provide a composable bootstrap-level
    /// match flow state machine. Drives AdvanceTick per tick, gates
    /// Gameplay commands during PreGame/Countdown, and captures the
    /// final MatchResultSnapshot.
    ///
    /// Design: FrameSync_Flow_Integrated_System_Design_v10_2 sections 2, 14
    /// </summary>
    public sealed class MatchFlowStateMachine
    {
        private readonly MatchRuleRuntime _rule;
        private readonly int _countdownTicks;
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

        public MatchFlowStateMachine(MatchRuleRuntime rule, int countdownTicks)
        {
            _rule = rule ?? throw new System.ArgumentNullException(nameof(rule));
            _countdownTicks = countdownTicks;
        }

        /// <summary>
        /// Advance the match state machine by one logic tick.
        /// Call after deterministic tick execution completes.
        /// </summary>
        public void AdvanceTick(int currentTick, UnitWorld unitWorld)
        {
            if (_rule == null) return;

            // Phase transitions driven by tick count
            _rule.AdvanceTick(currentTick);

            // Authority-confirmed end evaluation
            if (_rule.CurrentPhase == MatchPhase.Running)
            {
                _rule.EvaluateAuthorityConfirmedTick(currentTick, unitWorld);
            }

            // Record result when match finishes
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
