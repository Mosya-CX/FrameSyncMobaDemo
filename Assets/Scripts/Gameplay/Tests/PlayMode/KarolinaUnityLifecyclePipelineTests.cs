using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Karolina.Testing;
using Karolina.Testing.Pipeline;
using Karolina.Testing.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Unit.PlayModeTests
{
    public sealed class KarolinaUnityLifecyclePipelineTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private CancellationTokenSource activeCancellation;
        private Task activeExecution;

        [UnityTest]
        public IEnumerator SamplesUnityUpdateWithoutAProjectTick()
        {
            var scenario = new UnityFrameScenario(TrackRoot);
            activeCancellation = new CancellationTokenSource();
            activeExecution = RunPipelineAsync(
                scenario,
                activeCancellation.Token);
            int remainingFrames = 120;
            while (!activeExecution.IsCompleted && remainingFrames-- > 0)
            {
                yield return null;
            }

            if (!activeExecution.IsCompleted)
            {
                activeCancellation.Cancel();
                int cleanupFrames = 30;
                while (!activeExecution.IsCompleted && cleanupFrames-- > 0)
                {
                    yield return null;
                }
            }

            Assert.That(activeExecution.IsCompleted, Is.True,
                "等待 Unity Update 的测试流水线未在时限内结束。");
            if (activeExecution.IsFaulted)
            {
                Assert.Fail(activeExecution.Exception?.GetBaseException().ToString() ??
                    "Unity 生命周期测试流水线失败。");
                yield break;
            }
            if (activeExecution.IsCanceled)
            {
                Assert.Fail("Unity 生命周期测试流水线被取消。");
                yield break;
            }

            TestPipelineExecutionResult result =
                ((Task<TestPipelineExecutionResult>)activeExecution)
                    .GetAwaiter()
                    .GetResult();
            yield return null;

            Assert.That(result.Status, Is.EqualTo(TestPipelineStatus.Passed), result.ErrorMessage);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Assertions.Count, Is.EqualTo(1));
            Assert.That(scenario.LastFixture.Root == null, Is.True,
                "流水线清理后仍留有 Unity 测试 GameObject。");
            Assert.That(scenario.LogContents, Does.Contain("\"eventType\":\"lifecycle.frame.sample\""));
            Assert.That(scenario.LogContents, Does.Contain("\"domain\":\"UnityFrame\""));
        }

        [UnityTest]
        public IEnumerator WaitRequestedBeforeClockUpdateResolvesOnLaterFrame()
        {
            activeCancellation = new CancellationTokenSource();
            var root = CreateTrackedRoot("KarolinaUnityFrameClockEarlyWaitTest");
            var clock = root.AddComponent<UnityFrameClock>();
            var requester = root.AddComponent<UnityFrameWaitProbe>();
            requester.Clock = clock;
            requester.CancellationToken = activeCancellation.Token;

            int remainingFrames = 5;
            while ((requester.WaitTask == null || !requester.WaitTask.IsCompleted) &&
                   remainingFrames-- > 0)
            {
                activeExecution = requester.WaitTask;
                yield return null;
            }
            activeExecution = requester.WaitTask;

            Assert.That(requester.WaitTask, Is.Not.Null,
                "早期帧等待器没有在 Start 中启动。");
            Assert.That(requester.WaitTask.IsCompleted, Is.True,
                "UnityFrameClock 没有在有限帧内完成等待。");
            Assert.That(requester.WaitTask.IsFaulted, Is.False,
                requester.WaitTask.Exception?.GetBaseException().ToString());
            UnityFrameSample sample = requester.WaitTask.GetAwaiter().GetResult();
            Assert.That(sample.FrameCount, Is.GreaterThan(requester.RequestedAfterFrame),
                "等待必须返回请求之后的帧，不能由同帧 Update 完成。");
        }

        [UnityTest]
        public IEnumerator CancellationRemovesPendingFrameWait()
        {
            var root = CreateTrackedRoot("KarolinaUnityFrameClockCancellationTest");
            var clock = root.AddComponent<UnityFrameClock>();
            using (var cancellation = new CancellationTokenSource())
            {
                Task<UnityFrameSample> wait = clock
                    .WaitForNextFrameAsync(cancellation.Token)
                    .AsTask();
                activeExecution = wait;
                cancellation.Cancel();

                int remainingFrames = 10;
                while (!wait.IsCompleted && remainingFrames-- > 0)
                {
                    yield return null;
                }

                Assert.That(wait.IsCanceled, Is.True,
                    "取消令牌没有结束挂起的 Unity 帧等待。");
            }
        }

        [UnityTest]
        public IEnumerator DisablingClockFailsPendingFrameWait()
        {
            var root = CreateTrackedRoot("KarolinaUnityFrameClockDisableTest");
            var clock = root.AddComponent<UnityFrameClock>();
            Task<UnityFrameSample> wait = clock
                .WaitForNextFrameAsync()
                .AsTask();
            activeExecution = wait;
            clock.enabled = false;

            int remainingFrames = 10;
            while (!wait.IsCompleted && remainingFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(wait.IsFaulted, Is.True,
                "禁用 UnityFrameClock 后等待没有进入明确失败状态。");
            Assert.That(wait.Exception.GetBaseException(), Is.TypeOf<InvalidOperationException>());
        }

        [UnityTearDown]
        public IEnumerator CleanupTestObjectsAndPendingExecution()
        {
            activeCancellation?.Cancel();
            int remainingFrames = 120;
            while (activeExecution != null &&
                   !activeExecution.IsCompleted &&
                   remainingFrames-- > 0)
            {
                yield return null;
            }

            for (int i = roots.Count - 1; i >= 0; i--)
            {
                if (roots[i] != null)
                {
                    UnityEngine.Object.Destroy(roots[i]);
                }
            }
            roots.Clear();
            yield return null;

            activeExecution = null;
            activeCancellation?.Dispose();
            activeCancellation = null;
        }

        private GameObject CreateTrackedRoot(string objectName)
        {
            var root = new GameObject(objectName);
            TrackRoot(root);
            return root;
        }

        private void TrackRoot(GameObject root)
        {
            if (root != null)
            {
                roots.Add(root);
            }
        }

        private static async Task<TestPipelineExecutionResult> RunPipelineAsync(
            UnityFrameScenario scenario,
            CancellationToken cancellationToken)
        {
            using (var stream = new MemoryStream())
            {
                var log = new AsyncTestSessionLog(stream, leaveOpen: true);
                try
                {
                    return await new TestPipelineExecutor().RunAsync(
                        scenario,
                        new TestSessionIdentity(
                            Guid.NewGuid().ToString("N"),
                            scenario.Id,
                            "attempt-1",
                            1),
                        log,
                        cancellationToken);
                }
                finally
                {
                    await log.DisposeAsync();
                    scenario.LogContents = Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private sealed class UnityFrameScenario :
            ITestPipelineScenario<UnityFrameFixture, UnityFrameSample>
        {
            private readonly Action<GameObject> trackRoot;

            public string Id => "karolina.unity.update-frame";
            public string Title => "Unity Update 生命周期采样";
            public UnityFrameFixture LastFixture { get; private set; }
            public string LogContents { get; set; }

            public UnityFrameScenario(Action<GameObject> trackRoot)
            {
                this.trackRoot = trackRoot ?? throw new ArgumentNullException(nameof(trackRoot));
            }

            public ValueTask<UnityFrameFixture> PrepareAsync(
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = new GameObject("KarolinaUnityFrameClockTest");
                trackRoot(root);
                var fixture = new UnityFrameFixture
                {
                    Root = root,
                    Clock = root.AddComponent<UnityFrameClock>(),
                };
                LastFixture = fixture;
                context.Record(
                    TestSessionLogLevel.Info,
                    "prepare",
                    "lifecycle.fixture.ready",
                    "UnityFrameClock 测试夹具已创建");
                return new ValueTask<UnityFrameFixture>(fixture);
            }

            public ValueTask InjectAsync(
                UnityFrameFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Record(
                    TestSessionLogLevel.Info,
                    "input",
                    "lifecycle.wait.requested",
                    "请求等待下一个 Unity Update 回调");
                return new ValueTask();
            }

            public async ValueTask<UnityFrameSample> SampleAsync(
                UnityFrameFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                fixture.RequestedAfterFrame = Time.frameCount;
                UnityFrameSample sample = await fixture.Clock
                    .WaitForNextFrameAsync(cancellationToken)
                    .ConfigureAwait(true);
                context.Record(
                    TestSessionLogLevel.Info,
                    "sample",
                    "lifecycle.frame.sample",
                    "从 Unity Update 获取一帧样本",
                    sample.ToTimelinePosition(),
                    new Dictionary<string, string>
                    {
                        ["requestedAfterFrame"] = fixture.RequestedAfterFrame.ToString(),
                        ["frameCount"] = sample.FrameCount.ToString(),
                        ["unscaledTimeSeconds"] = sample.UnscaledTimeSeconds.ToString("R"),
                        ["unscaledDeltaSeconds"] = sample.UnscaledDeltaSeconds.ToString("R"),
                    });
                return sample;
            }

            public ValueTask<IReadOnlyList<TestAssertionResult>> AnalyzeAsync(
                UnityFrameFixture fixture,
                UnityFrameSample sample,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var assertion = new TestAssertionResult(
                    "unity.frame.advanced",
                    "采样来自请求之后的 Unity Update 回调",
                    sample.FrameCount > fixture.RequestedAfterFrame
                        ? TestAssertionStatus.Passed
                        : TestAssertionStatus.Failed,
                    "Unity 帧号严格大于请求时的帧号",
                    fixture.RequestedAfterFrame + " -> " + sample.FrameCount);
                return new ValueTask<IReadOnlyList<TestAssertionResult>>(
                    new[] { assertion });
            }

            public ValueTask CleanupAsync(
                UnityFrameFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                if (fixture.Root != null)
                {
                    UnityEngine.Object.Destroy(fixture.Root);
                }
                return new ValueTask();
            }
        }

        private sealed class UnityFrameFixture
        {
            public GameObject Root;
            public UnityFrameClock Clock;
            public int RequestedAfterFrame;
        }
    }

    [DefaultExecutionOrder(-32000)]
    public sealed class UnityFrameWaitProbe : MonoBehaviour
    {
        public UnityFrameClock Clock { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public Task<UnityFrameSample> WaitTask { get; private set; }
        public int RequestedAfterFrame { get; private set; }

        private void Start()
        {
            RequestedAfterFrame = Time.frameCount;
            WaitTask = Clock.WaitForNextFrameAsync(CancellationToken).AsTask();
        }
    }
}
