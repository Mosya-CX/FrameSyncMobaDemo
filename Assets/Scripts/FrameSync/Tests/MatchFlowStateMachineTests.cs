using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    /// <summary>
    /// Tests for MatchFlowStateMachine and MatchResultSnapshot (ExecPlan 0090).
    /// </summary>
    public class MatchFlowStateMachineTests
    {
        [Test]
        public void MatchFlow_InitialState_Preparing()
        {
            var rule = new MatchRuleRuntime(60);
            var flow = new MatchFlowStateMachine(rule, 90);
            Assert.That(flow.HasFinished, Is.False);
            Assert.That(flow.AcceptsGameplayCommands, Is.False);
            Assert.That(flow.Result.WinningTeamId, Is.EqualTo(TeamId.Neutral));
        }

        [Test]
        public void MatchFlow_AfterCountdown_TransitionsToRunning()
        {
            var rule = new MatchRuleRuntime(60);
            rule.BeginCountdown(0, 90);
            var flow = new MatchFlowStateMachine(rule, 90);

            // Advance during countdown
            for (int tick = 0; tick < 90; tick++)
                flow.AdvanceTick(tick, null);

            // At tick 90, should be Running
            flow.AdvanceTick(90, null);

            Assert.That(flow.HasFinished, Is.False);
            Assert.That(flow.AcceptsGameplayCommands, Is.True);
        }

        [Test]
        public void MatchFlow_CommandsGatedDuringCountdown()
        {
            var rule = new MatchRuleRuntime(60);
            rule.BeginCountdown(0, 90);
            var flow = new MatchFlowStateMachine(rule, 90);

            // Advance to tick 50 (still in countdown)
            flow.AdvanceTick(50, null);
            Assert.That(flow.AcceptsGameplayCommands, Is.False);

            // Advance to tick 90 (now running)
            flow.AdvanceTick(90, null);
            Assert.That(flow.AcceptsGameplayCommands, Is.True);
        }

        [Test]
        public void MatchResultSnapshot_DefaultIsEmpty()
        {
            var snapshot = MatchResultSnapshot.Empty;
            Assert.That(snapshot.WinningTeamId, Is.EqualTo(TeamId.Neutral));
            Assert.That(snapshot.EndReason, Is.EqualTo(MatchEndReason.None));
            Assert.That(snapshot.GameOverTick, Is.EqualTo(0));
            Assert.That(snapshot.FinishTick, Is.EqualTo(0));
        }

        [Test]
        public void MatchResultSnapshot_StoresResult()
        {
            var snapshot = new MatchResultSnapshot
            {
                WinningTeamId = new TeamId(1),
                EndReason = MatchEndReason.BaseDestroyed,
                GameOverTick = 3600,
                FinishTick = 3660,
                Statistics = new MatchStatisticsResult(),
            };
            Assert.That(snapshot.WinningTeamId.Value, Is.EqualTo(1));
            Assert.That(snapshot.EndReason, Is.EqualTo(MatchEndReason.BaseDestroyed));
            Assert.That(snapshot.GameOverTick, Is.EqualTo(3600));
            Assert.That(snapshot.DurationTicks, Is.EqualTo(3600));
        }

        [Test]
        public void MatchFlow_AcceptsCommands_OnlyRunningOrEnding()
        {
            var rule = new MatchRuleRuntime(60);
            var flow = new MatchFlowStateMachine(rule, 90);

            // Preparing -> No commands
            Assert.That(flow.AcceptsGameplayCommands, Is.False);

            // Transition to countdown
            rule.BeginCountdown(0, 90);
            flow.AdvanceTick(0, null);
            Assert.That(flow.AcceptsGameplayCommands, Is.False);

            // Running -> Commands accepted
            flow.AdvanceTick(90, null);
            Assert.That(flow.AcceptsGameplayCommands, Is.True);
        }
    }
}
