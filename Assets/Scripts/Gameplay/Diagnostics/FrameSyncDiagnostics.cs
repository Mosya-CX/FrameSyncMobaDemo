using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace FrameSyncMoba.Unit
{
    public enum FrameSyncDiagnosticLevel : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4,
    }

    public sealed class FrameSyncDiagnosticOptions
    {
        public string EndpointName { get; }
        public string OutputPath { get; }
        public bool MirrorDiagnosticsToStandardOutput { get; }
        public int MaxQueuedEntries { get; }
        public int MaxPriorityQueuedEntries { get; }
        public int BatchSize { get; }
        public int FlushIntervalMilliseconds { get; }

        public FrameSyncDiagnosticOptions(
            string endpointName,
            string outputPath,
            bool mirrorDiagnosticsToStandardOutput,
            int maxQueuedEntries = 8192,
            int maxPriorityQueuedEntries = 256,
            int batchSize = 128,
            int flushIntervalMilliseconds = 250)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
                throw new ArgumentException(
                    "Diagnostic endpoint name is required.",
                    nameof(endpointName));
            if (maxQueuedEntries < 16)
                throw new ArgumentOutOfRangeException(
                    nameof(maxQueuedEntries));
            if (batchSize < 1 || batchSize > maxQueuedEntries)
                throw new ArgumentOutOfRangeException(
                    nameof(batchSize));
            if (maxPriorityQueuedEntries < 8)
                throw new ArgumentOutOfRangeException(
                    nameof(maxPriorityQueuedEntries));
            if (flushIntervalMilliseconds < 10)
                throw new ArgumentOutOfRangeException(
                    nameof(flushIntervalMilliseconds));

            EndpointName = endpointName;
            OutputPath = outputPath ?? string.Empty;
            MirrorDiagnosticsToStandardOutput =
                mirrorDiagnosticsToStandardOutput;
            MaxQueuedEntries = maxQueuedEntries;
            MaxPriorityQueuedEntries =
                maxPriorityQueuedEntries;
            BatchSize = batchSize;
            FlushIntervalMilliseconds = flushIntervalMilliseconds;
        }
    }

    public readonly struct FrameSyncDiagnosticStats
    {
        public readonly long AcceptedEntries;
        public readonly long WrittenEntries;
        public readonly long DroppedEntries;
        public readonly long WriteFailures;
        public readonly int PendingEntries;

        public FrameSyncDiagnosticStats(
            long acceptedEntries,
            long writtenEntries,
            long droppedEntries,
            long writeFailures,
            int pendingEntries)
        {
            AcceptedEntries = acceptedEntries;
            WrittenEntries = writtenEntries;
            DroppedEntries = droppedEntries;
            WriteFailures = writeFailures;
            PendingEntries = pendingEntries;
        }
    }

    /// <summary>
    /// Optional, presentation-only diagnostic transport. Enabled Player builds
    /// compile calls with FRAME_SYNC_MOBA_DIAGNOSTICS; disabled builds remove
    /// every Conditional call site. Producers only enqueue bounded work. A
    /// dedicated background thread owns formatting and all file/stdout IO.
    /// </summary>
    public static class FrameSyncDiagnostics
    {
        public const string BuildDefine =
            "FRAME_SYNC_MOBA_DIAGNOSTICS";

#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
        private enum WorkKind : byte
        {
            Log = 0,
            Flush = 1,
            Artifact = 2,
        }

        private sealed class WorkItem
        {
            public WorkKind Kind;
            public long Sequence;
            public long UtcTicks;
            public FrameSyncDiagnosticLevel Level;
            public string Message;
            public string MatchId;
            public int PlayerSlot;
            public bool MirrorToStandardOutput;
            public string ArtifactPath;
            public Func<string> ArtifactFactory;
        }

        private static readonly object LifecycleGate = new object();
        private static readonly ConcurrentQueue<WorkItem> PriorityQueue =
            new ConcurrentQueue<WorkItem>();
        private static readonly ConcurrentQueue<WorkItem> NormalQueue =
            new ConcurrentQueue<WorkItem>();
        private static readonly ConcurrentQueue<string> FailureQueue =
            new ConcurrentQueue<string>();

        private static AutoResetEvent wakeSignal;
        private static Thread workerThread;
        private static FrameSyncDiagnosticOptions currentOptions;
        private static string currentMatchId = string.Empty;
        private static int currentPlayerSlot = -1;
        private static int normalPendingCount;
        private static int priorityPendingCount;
        private static int stopRequested;
        private static long nextSequence;
        private static long acceptedEntries;
        private static long writtenEntries;
        private static long droppedEntries;
        private static long writeFailures;
#endif

        public static bool IsCompiledIn
        {
            get
            {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsRunning
        {
            get
            {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
                Thread thread = Volatile.Read(ref workerThread);
                return thread != null && thread.IsAlive;
#else
                return false;
#endif
            }
        }

        public static string OutputPath
        {
            get
            {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
                return Volatile.Read(ref currentOptions)?.OutputPath ??
                    string.Empty;
#else
                return string.Empty;
#endif
            }
        }

        public static FrameSyncDiagnosticStats Stats
        {
            get
            {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
                return new FrameSyncDiagnosticStats(
                    Interlocked.Read(ref acceptedEntries),
                    Interlocked.Read(ref writtenEntries),
                    Interlocked.Read(ref droppedEntries),
                    Interlocked.Read(ref writeFailures),
                    Math.Max(0, Volatile.Read(ref normalPendingCount)) +
                    Math.Max(0, Volatile.Read(ref priorityPendingCount)));
#else
                return default;
#endif
            }
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void Initialize(
            FrameSyncDiagnosticOptions options)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            lock (LifecycleGate)
            {
                StopWorker(1000);
                DrainQueuesWithoutWriting();
                while (FailureQueue.TryDequeue(out _))
                {
                }
                currentOptions = options;
                currentMatchId = string.Empty;
                currentPlayerSlot = -1;
                normalPendingCount = 0;
                priorityPendingCount = 0;
                stopRequested = 0;
                nextSequence = 0;
                acceptedEntries = 0;
                writtenEntries = 0;
                droppedEntries = 0;
                writeFailures = 0;
                wakeSignal = new AutoResetEvent(false);
                workerThread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "FrameSyncDiagnostics",
                    Priority = ThreadPriority.BelowNormal,
                };
                workerThread.Start();
            }
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void SetContext(
            string matchId,
            int playerSlot)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            Volatile.Write(
                ref currentMatchId,
                matchId ?? string.Empty);
            Volatile.Write(ref currentPlayerSlot, playerSlot);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void Log(string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            EnqueueLog(
                FrameSyncDiagnosticLevel.Info,
                message,
                true,
                false);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void LogTrace(string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            EnqueueLog(
                FrameSyncDiagnosticLevel.Trace,
                message,
                true,
                false);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            EnqueueLog(
                FrameSyncDiagnosticLevel.Warning,
                message,
                true,
                false);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            EnqueueLog(
                FrameSyncDiagnosticLevel.Error,
                message,
                true,
                true);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void LogCritical(string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            EnqueueLog(
                FrameSyncDiagnosticLevel.Critical,
                message,
                true,
                true);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void MirrorUnityLog(
            string condition,
            string stackTrace,
            int unityLogType)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            FrameSyncDiagnosticLevel level =
                unityLogType == 0 || unityLogType == 1
                    ? FrameSyncDiagnosticLevel.Error
                    : unityLogType == 2
                        ? FrameSyncDiagnosticLevel.Warning
                        : unityLogType == 4
                            ? FrameSyncDiagnosticLevel.Critical
                            : FrameSyncDiagnosticLevel.Info;
            string message = "[Unity] " + (condition ?? string.Empty);
            if (level >= FrameSyncDiagnosticLevel.Error &&
                !string.IsNullOrWhiteSpace(stackTrace))
            {
                message += "\n" + stackTrace;
            }
            EnqueueLog(level, message, false, level >=
                FrameSyncDiagnosticLevel.Error);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void WriteArtifact(
            string path,
            Func<string> contentFactory)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (!IsRunning ||
                string.IsNullOrWhiteSpace(path) ||
                contentFactory == null)
                return;
            var item = new WorkItem
            {
                Kind = WorkKind.Artifact,
                Sequence = Interlocked.Increment(ref nextSequence),
                UtcTicks = DateTime.UtcNow.Ticks,
                Level = FrameSyncDiagnosticLevel.Critical,
                MatchId = Volatile.Read(ref currentMatchId),
                PlayerSlot = Volatile.Read(ref currentPlayerSlot),
                MirrorToStandardOutput = true,
                ArtifactPath = path,
                ArtifactFactory = contentFactory,
            };
            EnqueuePriority(item);
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void RequestFlush()
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (!IsRunning)
                return;
            EnqueuePriority(
                new WorkItem
                {
                    Kind = WorkKind.Flush,
                    Sequence = Interlocked.Increment(ref nextSequence),
                    UtcTicks = DateTime.UtcNow.Ticks,
                    MatchId = Volatile.Read(ref currentMatchId),
                    PlayerSlot = Volatile.Read(ref currentPlayerSlot),
                });
#endif
        }

        [Conditional(BuildDefine)]
        [Conditional("UNITY_EDITOR")]
        public static void Shutdown(int timeoutMilliseconds = 1500)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (timeoutMilliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            lock (LifecycleGate)
                StopWorker(timeoutMilliseconds);
#endif
        }

        public static bool WaitForIdle(int timeoutMilliseconds)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (timeoutMilliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            var timer = Stopwatch.StartNew();
            while (Stats.PendingEntries > 0 &&
                   timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                Thread.Sleep(2);
            }
            return Stats.PendingEntries == 0;
#else
            return true;
#endif
        }

        public static bool TryDequeueFailure(out string message)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            return FailureQueue.TryDequeue(out message);
#else
            message = null;
            return false;
#endif
        }

#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
        private static void EnqueueLog(
            FrameSyncDiagnosticLevel level,
            string message,
            bool mirrorToStandardOutput,
            bool priority)
        {
            FrameSyncDiagnosticOptions options =
                Volatile.Read(ref currentOptions);
            if (!IsRunning || options == null)
                return;

            var item = new WorkItem
            {
                Kind = WorkKind.Log,
                Sequence = Interlocked.Increment(ref nextSequence),
                UtcTicks = DateTime.UtcNow.Ticks,
                Level = level,
                Message = message ?? string.Empty,
                MatchId = Volatile.Read(ref currentMatchId),
                PlayerSlot = Volatile.Read(ref currentPlayerSlot),
                MirrorToStandardOutput = mirrorToStandardOutput,
            };

            if (priority)
            {
                EnqueuePriority(item);
                return;
            }

            while (true)
            {
                int pending = Volatile.Read(ref normalPendingCount);
                if (pending >= options.MaxQueuedEntries)
                {
                    Interlocked.Increment(ref droppedEntries);
                    return;
                }
                if (Interlocked.CompareExchange(
                        ref normalPendingCount,
                        pending + 1,
                        pending) == pending)
                    break;
            }

            NormalQueue.Enqueue(item);
            Interlocked.Increment(ref acceptedEntries);
            wakeSignal?.Set();
        }

        private static void EnqueuePriority(WorkItem item)
        {
            FrameSyncDiagnosticOptions options =
                Volatile.Read(ref currentOptions);
            if (!IsRunning || options == null)
                return;
            while (true)
            {
                int pending = Volatile.Read(
                    ref priorityPendingCount);
                if (pending >=
                    options.MaxPriorityQueuedEntries)
                {
                    Interlocked.Increment(ref droppedEntries);
                    return;
                }
                if (Interlocked.CompareExchange(
                        ref priorityPendingCount,
                        pending + 1,
                        pending) == pending)
                    break;
            }
            PriorityQueue.Enqueue(item);
            Interlocked.Increment(ref acceptedEntries);
            wakeSignal?.Set();
        }

        private static void WorkerLoop()
        {
            StreamWriter writer = null;
            try
            {
                while (Volatile.Read(ref stopRequested) == 0 ||
                       !PriorityQueue.IsEmpty ||
                       !NormalQueue.IsEmpty)
                {
                    wakeSignal?.WaitOne(
                        currentOptions?.FlushIntervalMilliseconds ?? 250);
                    int written = DrainBatch(ref writer);
                    if (written > 0)
                        TryFlush(writer);
                }
                while (DrainBatch(ref writer) > 0)
                    TryFlush(writer);
                TryFlush(writer);
            }
            catch (Exception exception)
            {
                RecordFailure(
                    "Diagnostic worker terminated unexpectedly: " +
                    exception);
            }
            finally
            {
                try
                {
                    writer?.Flush();
                    writer?.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(
                        "Diagnostic writer shutdown failed: " +
                        exception.Message);
                }
            }
        }

        private static int DrainBatch(ref StreamWriter writer)
        {
            FrameSyncDiagnosticOptions options =
                Volatile.Read(ref currentOptions);
            if (options == null)
                return 0;

            int processed = 0;
            while (processed < options.BatchSize &&
                   TryDequeueWork(out WorkItem item))
            {
                switch (item.Kind)
                {
                    case WorkKind.Log:
                        WriteLogItem(item, options, ref writer);
                        break;
                    case WorkKind.Flush:
                        TryFlush(writer);
                        break;
                    case WorkKind.Artifact:
                        WriteArtifactItem(item, options);
                        break;
                }
                processed++;
            }
            return processed;
        }

        private static bool TryDequeueWork(out WorkItem item)
        {
            if (PriorityQueue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref priorityPendingCount);
                return true;
            }
            if (NormalQueue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref normalPendingCount);
                return true;
            }
            return false;
        }

        private static void WriteLogItem(
            WorkItem item,
            FrameSyncDiagnosticOptions options,
            ref StreamWriter writer)
        {
            string line = FormatLine(item, options.EndpointName);
            bool wrote = false;
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                try
                {
                    writer ??= OpenWriter(options.OutputPath);
                    writer.WriteLine(line);
                    wrote = true;
                }
                catch (Exception exception)
                {
                    TryDispose(ref writer);
                    RecordFailure(
                        $"Diagnostic file write failed path='{options.OutputPath}': " +
                        exception.Message);
                }
            }
            if (options.MirrorDiagnosticsToStandardOutput &&
                item.MirrorToStandardOutput)
            {
                try
                {
                    Console.Out.WriteLine(line);
                    wrote = true;
                }
                catch (Exception exception)
                {
                    RecordFailure(
                        "Diagnostic stdout write failed: " +
                        exception.Message,
                        false);
                }
            }
            if (wrote)
                Interlocked.Increment(ref writtenEntries);
        }

        private static void WriteArtifactItem(
            WorkItem item,
            FrameSyncDiagnosticOptions options)
        {
            try
            {
                string content = item.ArtifactFactory();
                string directory = Path.GetDirectoryName(
                    Path.GetFullPath(item.ArtifactPath));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(
                    item.ArtifactPath,
                    content ?? string.Empty,
                    new UTF8Encoding(false));
                Interlocked.Increment(ref writtenEntries);
                if (options.MirrorDiagnosticsToStandardOutput &&
                    item.MirrorToStandardOutput)
                {
                    Console.Out.WriteLine(
                        FormatLine(
                            new WorkItem
                            {
                                Sequence = item.Sequence,
                                UtcTicks = item.UtcTicks,
                                Level = item.Level,
                                MatchId = item.MatchId,
                                PlayerSlot = item.PlayerSlot,
                                Message =
                                    $"[Diagnostics] artifact written path='{item.ArtifactPath}'",
                            },
                            options.EndpointName));
                }
            }
            catch (Exception exception)
            {
                RecordFailure(
                    $"Diagnostic artifact write failed path='{item.ArtifactPath}': " +
                    exception.Message);
            }
        }

        private static StreamWriter OpenWriter(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var stream = new FileStream(
                fullPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                16384,
                FileOptions.SequentialScan);
            return new StreamWriter(
                stream,
                new UTF8Encoding(false),
                16384)
            {
                AutoFlush = false,
            };
        }

        private static string FormatLine(
            WorkItem item,
            string endpoint)
        {
            string match = string.IsNullOrWhiteSpace(item.MatchId)
                ? "-"
                : item.MatchId;
            string message = (item.Message ?? string.Empty)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return new DateTime(
                    item.UtcTicks,
                    DateTimeKind.Utc)
                .ToString("O") +
                $" endpoint={endpoint}" +
                $" match={match}" +
                $" slot={item.PlayerSlot}" +
                $" seq={item.Sequence}" +
                $" level={item.Level}" +
                $" {message}";
        }

        private static void TryFlush(StreamWriter writer)
        {
            if (writer == null)
                return;
            try
            {
                writer.Flush();
            }
            catch (Exception exception)
            {
                RecordFailure(
                    "Diagnostic file flush failed: " +
                    exception.Message);
            }
        }

        private static void TryDispose(ref StreamWriter writer)
        {
            try
            {
                writer?.Dispose();
            }
            catch
            {
            }
            writer = null;
        }

        private static void RecordFailure(
            string message,
            bool writeToStandardError = true)
        {
            Interlocked.Increment(ref writeFailures);
            FailureQueue.Enqueue(message);
            if (!writeToStandardError)
                return;
            try
            {
                Console.Error.WriteLine(
                    "[FrameSyncDiagnostics] " + message);
            }
            catch
            {
            }
        }

        private static void StopWorker(int timeoutMilliseconds)
        {
            Thread thread = workerThread;
            if (thread == null)
                return;
            Volatile.Write(ref stopRequested, 1);
            wakeSignal?.Set();
            if (thread != Thread.CurrentThread &&
                !thread.Join(timeoutMilliseconds))
            {
                RecordFailure(
                    $"Diagnostic worker did not stop within {timeoutMilliseconds} ms.");
                return;
            }
            workerThread = null;
            currentOptions = null;
            wakeSignal?.Dispose();
            wakeSignal = null;
        }

        private static void DrainQueuesWithoutWriting()
        {
            while (PriorityQueue.TryDequeue(out _))
            {
            }
            while (NormalQueue.TryDequeue(out _))
            {
            }
        }
#endif
    }
}
