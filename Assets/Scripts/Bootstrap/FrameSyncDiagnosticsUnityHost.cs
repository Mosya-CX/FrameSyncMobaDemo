using System;
using System.IO;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Unity lifecycle adapter for the optional asynchronous diagnostic
    /// transport. Disabled Player builds compile every call to this host out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrameSyncDiagnosticsUnityHost : MonoBehaviour
    {
        public const string RuntimeDisableArgument =
            "-disableFrameSyncDiagnostics";
        public const string RuntimeOutputArgumentPrefix =
            "-frameSyncDiagnosticsPath=";

#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
        private static FrameSyncDiagnosticsUnityHost instance;
        private long lastReportedDroppedEntries;
        private bool shuttingDown;
#endif

        [System.Diagnostics.Conditional(
            FrameSyncDiagnostics.BuildDefine)]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void EnsureInitialized(
            bool dedicatedServer)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            if (!Application.isPlaying)
                return;
            if (HasArgument(RuntimeDisableArgument))
                return;
            if (instance != null)
                return;

            var hostObject = new GameObject(
                "[FrameSyncDiagnostics]");
            DontDestroyOnLoad(hostObject);
            instance = hostObject.AddComponent<
                FrameSyncDiagnosticsUnityHost>();
            instance.Initialize(dedicatedServer);
#endif
        }

        [System.Diagnostics.Conditional(
            FrameSyncDiagnostics.BuildDefine)]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void SetContext(
            string matchId,
            int playerSlot)
        {
#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
            FrameSyncDiagnostics.SetContext(
                matchId,
                playerSlot);
#endif
        }

#if UNITY_EDITOR || FRAME_SYNC_MOBA_DIAGNOSTICS
        private void Initialize(bool dedicatedServer)
        {
            string endpoint = dedicatedServer
                ? "dedicated-server"
                : "client";
            string outputPath = ResolveOutputPath(
                endpoint,
                dedicatedServer);
            FrameSyncDiagnostics.Initialize(
                new FrameSyncDiagnosticOptions(
                    endpoint,
                    outputPath,
                    true));
            Application.logMessageReceivedThreaded +=
                OnUnityLogMessageReceived;
            string startupMessage =
                $"[Diagnostics] asynchronous logging enabled " +
                $"endpoint={endpoint} path='{outputPath}' " +
                $"capacity=8192 priorityCapacity=256 " +
                $"batch=128 flushMs=250";
            FrameSyncDiagnostics.Log(startupMessage);
            Debug.Log(startupMessage);
        }

        private void Update()
        {
            int failureCount = 0;
            while (failureCount < 4 &&
                   FrameSyncDiagnostics.TryDequeueFailure(
                       out string failure))
            {
                failureCount++;
                Debug.LogError(
                    "[Diagnostics] background writer failure: " +
                    failure);
            }

            FrameSyncDiagnosticStats stats =
                FrameSyncDiagnostics.Stats;
            if (stats.DroppedEntries <=
                lastReportedDroppedEntries)
                return;
            long newlyDropped =
                stats.DroppedEntries -
                lastReportedDroppedEntries;
            lastReportedDroppedEntries =
                stats.DroppedEntries;
            Debug.LogWarning(
                $"[Diagnostics] bounded queue dropped " +
                $"{newlyDropped} low-priority entries " +
                $"(total={stats.DroppedEntries}, " +
                $"pending={stats.PendingEntries}).");
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
            Teardown(1500);
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;
            Teardown(shuttingDown ? 1500 : 500);
            instance = null;
        }

        private void Teardown(int timeoutMilliseconds)
        {
            Application.logMessageReceivedThreaded -=
                OnUnityLogMessageReceived;
            FrameSyncDiagnostics.Log(
                "[Diagnostics] shutdown requested.");
            FrameSyncDiagnostics.Shutdown(
                timeoutMilliseconds);
        }

        private static void OnUnityLogMessageReceived(
            string condition,
            string stackTrace,
            LogType type)
        {
            // Host status/errors already have a direct diagnostic or stderr
            // path. Re-enqueueing them would create a feedback loop when the
            // diagnostic destination itself is unavailable.
            if (!string.IsNullOrEmpty(condition) &&
                condition.StartsWith(
                    "[Diagnostics]",
                    StringComparison.Ordinal))
                return;
            FrameSyncDiagnostics.MirrorUnityLog(
                condition,
                stackTrace,
                (int)type);
        }

        private static string ResolveOutputPath(
            string endpoint,
            bool dedicatedServer)
        {
            string[] arguments =
                Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (argument.StartsWith(
                        RuntimeOutputArgumentPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    string explicitPath = argument.Substring(
                        RuntimeOutputArgumentPrefix.Length);
                    if (!string.IsNullOrWhiteSpace(explicitPath))
                        return Path.GetFullPath(explicitPath);
                }
            }

            string unityLogPath =
                Application.consoleLogPath;
            if (!dedicatedServer &&
                !Application.isEditor &&
                !string.IsNullOrWhiteSpace(unityLogPath) &&
                unityLogPath != "-")
            {
                return Path.GetFullPath(
                    unityLogPath +
                    ".diagnostics.log");
            }

            string fileName =
                $"framesync_{endpoint}_" +
                $"{System.Diagnostics.Process.GetCurrentProcess().Id}_" +
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}.log";
            return Path.Combine(
                Application.persistentDataPath,
                "FrameSyncDiagnostics",
                fileName);
        }

        private static bool HasArgument(string expected)
        {
            string[] arguments =
                Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
#endif
    }
}
