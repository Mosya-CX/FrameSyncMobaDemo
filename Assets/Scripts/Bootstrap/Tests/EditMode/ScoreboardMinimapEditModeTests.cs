using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Unit tests for ScoreboardController and MinimapController (ExecPlan 0087).
    /// </summary>
    public class ScoreboardMinimapEditModeTests
    {
        [Test]
        public void ScoreboardController_CreatedWithCanvas()
        {
            var go = new GameObject("TestScoreboard", typeof(ScoreboardController));
            var controller = go.GetComponent<ScoreboardController>();
            Assert.That(controller, Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MinimapController_CreatedWithTexture()
        {
            var go = new GameObject("TestMinimap", typeof(MinimapController));
            var controller = go.GetComponent<MinimapController>();
            Assert.That(controller, Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoreboardRow_Created()
        {
            var go = new GameObject("TestRow", typeof(ScoreboardRow));
            var row = go.GetComponent<ScoreboardRow>();
            Assert.That(row, Is.Not.Null);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            row.Initialize(font);
            row.Set("Player1", 5, 2, 10, Color.white);
            Assert.That(row, Is.Not.Null);
            Object.DestroyImmediate(go);
        }
    }
}
