using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class FrameSyncDiagnosticsTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(
                Path.GetTempPath(),
                "FrameSyncMobaDiagnosticsTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            FrameSyncDiagnostics.Shutdown(2000);
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [Test]
        public void BackgroundWriter_WritesContextLogAndArtifact()
        {
            string logPath = Path.Combine(
                testDirectory,
                "diagnostics.log");
            string artifactPath = Path.Combine(
                testDirectory,
                "mismatch.worlddump.txt");
            FrameSyncDiagnostics.Initialize(
                new FrameSyncDiagnosticOptions(
                    "test-client",
                    logPath,
                    false,
                    64,
                    16,
                    8,
                    10));
            FrameSyncDiagnostics.SetContext(
                "match-alpha",
                2);

            FrameSyncDiagnostics.Log(
                "[Shop] Purchase item=31009 tick=5845");
            FrameSyncDiagnostics.WriteArtifact(
                artifactPath,
                () => "world-state");
            FrameSyncDiagnostics.RequestFlush();
            FrameSyncDiagnostics.Shutdown(2000);

            Assert.That(File.Exists(logPath), Is.True);
            string log = File.ReadAllText(logPath);
            StringAssert.Contains(
                "endpoint=test-client",
                log);
            StringAssert.Contains(
                "match=match-alpha",
                log);
            StringAssert.Contains("slot=2", log);
            StringAssert.Contains(
                "[Shop] Purchase item=31009 tick=5845",
                log);
            Assert.That(
                File.ReadAllText(artifactPath),
                Is.EqualTo("world-state"));
            Assert.That(
                FrameSyncDiagnostics.Stats.WriteFailures,
                Is.Zero);
        }

        [Test]
        public void BoundedQueue_DropsLowPriorityWithoutBlockingProducer()
        {
            string logPath = Path.Combine(
                testDirectory,
                "bounded.log");
            string artifactPath = Path.Combine(
                testDirectory,
                "gate.txt");
            using var workerEntered =
                new ManualResetEventSlim(false);
            using var releaseWorker =
                new ManualResetEventSlim(false);
            FrameSyncDiagnostics.Initialize(
                new FrameSyncDiagnosticOptions(
                    "test-server",
                    logPath,
                    false,
                    16,
                    8,
                    1,
                    10));
            FrameSyncDiagnostics.WriteArtifact(
                artifactPath,
                () =>
                {
                    workerEntered.Set();
                    releaseWorker.Wait(2000);
                    return "released";
                });
            Assert.That(
                workerEntered.Wait(1000),
                Is.True,
                "Background worker did not start the priority artifact.");

            for (int i = 0; i < 1000; i++)
                FrameSyncDiagnostics.LogTrace(
                    "trace-" + i);

            FrameSyncDiagnosticStats saturated =
                FrameSyncDiagnostics.Stats;
            Assert.That(
                saturated.DroppedEntries,
                Is.GreaterThan(0));
            Assert.That(
                saturated.PendingEntries,
                Is.LessThanOrEqualTo(17),
                "The bounded normal queue plus one priority artifact " +
                "must remain bounded while the writer is stalled.");

            releaseWorker.Set();
            FrameSyncDiagnostics.Shutdown(2000);
            Assert.That(File.Exists(artifactPath), Is.True);
        }

        [Test]
        public void WriterFailure_IsObservableInsteadOfSwallowed()
        {
            Directory.CreateDirectory(testDirectory);
            FrameSyncDiagnostics.Initialize(
                new FrameSyncDiagnosticOptions(
                    "test-client",
                    testDirectory,
                    false,
                    32,
                    8,
                    4,
                    10));

            FrameSyncDiagnostics.LogError(
                "force invalid file target");
            FrameSyncDiagnostics.Shutdown(2000);

            Assert.That(
                FrameSyncDiagnostics.Stats.WriteFailures,
                Is.GreaterThan(0));
            Assert.That(
                FrameSyncDiagnostics.TryDequeueFailure(
                    out string failure),
                Is.True);
            StringAssert.Contains(
                "Diagnostic file write failed",
                failure);
        }

        [Test]
        public void Options_RejectInvalidQueueConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FrameSyncDiagnosticOptions(
                    "test",
                    string.Empty,
                    false,
                    8,
                    8));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FrameSyncDiagnosticOptions(
                    "test",
                    string.Empty,
                    false,
                    16,
                    8,
                    17));
        }
    }
}
