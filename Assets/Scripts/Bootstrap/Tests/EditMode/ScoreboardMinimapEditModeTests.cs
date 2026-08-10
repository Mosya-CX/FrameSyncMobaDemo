using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Unit tests for MinimapController (ExecPlan 0087). The scoreboard moved
    /// to the Lua-driven HUD page (ExecPlan 0126 Slice E).
    /// </summary>
    public class ScoreboardMinimapEditModeTests
    {
        [Test]
        public void MinimapController_CreatedWithTexture()
        {
            var go = new GameObject("TestMinimap", typeof(MinimapController));
            var controller = go.GetComponent<MinimapController>();
            Assert.That(controller, Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void
            TeamScoreLogThrottle_LogsOnlyWhenContentChanges()
        {
            var throttle =
                new TeamScoreLogThrottle();
            const string lineA =
                "[Scoreboard] rank=0 targetTeam=1 " +
                "score=0 teamIds=1,2 " +
                "breakdown=[3/1101/0:k0/d0/a0/c2]";
            const string lineB =
                "[Scoreboard] rank=0 targetTeam=1 " +
                "score=0 teamIds=1,2 " +
                "breakdown=[3/1101/0:k0/d0/a0/c3]";

            Assert.IsTrue(
                throttle.ShouldLog(0, lineA));
            Assert.IsFalse(
                throttle.ShouldLog(0, lineA),
                "Identical per-frame repeat must be suppressed.");
            Assert.IsTrue(
                throttle.ShouldLog(1, lineA),
                "Different rank keeps an independent state.");
            Assert.IsTrue(
                throttle.ShouldLog(0, lineB),
                "Changed content (creep kill delta) must be logged.");
            Assert.IsFalse(
                throttle.ShouldLog(0, lineB));
        }
    }
}
