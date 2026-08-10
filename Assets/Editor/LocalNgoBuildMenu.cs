using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FrameSyncMoba.EditorTools
{
    public static class LocalNgoBuildMenu
    {
        // Blocks the MCP bridge's queued re-executions of the same build
        // tool call, which arrive right after the first build completes.
        // 120 seconds is enough to swallow that burst while allowing an
        // intentional rebuild a couple of minutes later.
        private const string BuildCompletedGuardKeyPrefix =
            "FrameSyncMoba.LocalNgoBuildMenu.LastBuildCompletedUtcTicks.";
        private static readonly TimeSpan BuildReplayGuardWindow =
            TimeSpan.FromSeconds(120);
        private static bool isBuilding;

        public const string ServerScene =
            "Assets/Scenes/ServerBootstrap.unity";
        public const string ClientScene =
            "Assets/Scenes/ClientBootstrap.unity";
        public const string LobbyScene =
            "Assets/Scenes/Lobby.unity";
        public const string GameScene =
            "Assets/Scenes/GameScene.unity";
        public const string BuildRoot =
            "Builds/LocalNgo";
        public const string UosServerBuildRoot =
            "Builds/UosServer";
        public const string UosClientBuildRoot =
            "Builds/UosClient";
        private const string UosOnlineBuildDefine =
            "FRAME_SYNC_MOBA_UOS_ONLINE";

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Server")]
        public static void BuildServer()
        {
            RunExclusive("server-windows", BuildServerCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Server Linux (UOS)")]
        public static void BuildServerLinux()
        {
            RunExclusive("server-linux-uos", BuildServerLinuxCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Client")]
        public static void BuildClient()
        {
            RunExclusive("client-windows-local", BuildClientCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Client Windows (UOS)")]
        public static void BuildClientUos()
        {
            RunExclusive("client-windows-uos", BuildClientUosCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Both")]
        public static void BuildBoth()
        {
            if (WasBuildRecentlyCompleted("both-local"))
            {
                UnityEngine.Debug.LogWarning(
                    "Ignored duplicate Local NGO Build Both request. " +
                    "Use the individual build commands or clear the " +
                    "retry guard before intentionally rebuilding.");
                return;
            }

            RunExclusive(
                "both-local",
                () =>
                {
                    BuildServerCore();
                    BuildClientCore();
                });
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Clear Build-Both Retry Guard")]
        public static void ClearBuildBothRetryGuard()
        {
            string[] keys =
            {
                "server-windows",
                "server-linux-uos",
                "client-windows-local",
                "client-windows-uos",
                "both-local",
            };
            for (int i = 0; i < keys.Length; i++)
                SessionState.EraseString(
                    BuildCompletedGuardKeyPrefix + keys[i]);
        }

        private static void BuildServerCore()
        {
            string output = Path.GetFullPath(
                Path.Combine(
                    BuildRoot,
                    "Server",
                    "FrameSyncMobaServer.exe"));
            Build(ServerScene, output, true);
        }

        /// <summary>
        /// Builds the Dedicated Server for the UOS Multiverse image. The
        /// output folder must be zipped and uploaded in the UOS console
        /// (entry command: FrameSyncMobaServer.x86_64, port 7777).
        /// </summary>
        private static void BuildServerLinuxCore()
        {
            string output = Path.GetFullPath(
                Path.Combine(
                    UosServerBuildRoot,
                    "FrameSyncMobaServer.x86_64"));
            BuildLinuxServer(output);
        }

        private static void BuildLinuxServer(string output)
        {
            string directory =
                Path.GetDirectoryName(output);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "UOS server build output directory is invalid.");
            Directory.CreateDirectory(directory);
            var scenes = new List<string>
            {
                ServerScene,
                LobbyScene,
                GameScene,
            };
            var options =
                new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = output,
                    target =
                        BuildTarget.StandaloneLinux64,
                    targetGroup =
                        BuildTargetGroup.Standalone,
                    options =
                        BuildOptions.None,
                    subtarget =
                        (int)StandaloneBuildSubtarget
                            .Server,
                    extraScriptingDefines =
                        new[] { UosOnlineBuildDefine },
                };
            BuildReport report =
                BuildPipeline.BuildPlayer(options);
            if (report.summary.result !=
                BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"UOS server build failed: {report.summary.result}.");
        }

        private static void BuildClientCore()
        {
            string output = Path.GetFullPath(
                Path.Combine(
                    BuildRoot,
                    "Client",
                    "FrameSyncMobaClient.exe"));
            Build(ClientScene, output, false);
        }

        private static void BuildClientUosCore()
        {
            string output = Path.GetFullPath(
                Path.Combine(
                    UosClientBuildRoot,
                    "FrameSyncMobaClient.exe"));
            Build(ClientScene, output, false, true);
        }

        private static bool WasBuildRecentlyCompleted(
            string buildKey)
        {
            string value = SessionState.GetString(
                BuildCompletedGuardKeyPrefix + buildKey,
                string.Empty);
            return long.TryParse(value, out long ticks) &&
                   DateTime.UtcNow - new DateTime(
                       ticks,
                       DateTimeKind.Utc) <
                   BuildReplayGuardWindow;
        }

        private static void RunExclusive(
            string buildKey,
            Action buildAction)
        {
            if (isBuilding || BuildPipeline.isBuildingPlayer)
            {
                UnityEngine.Debug.LogWarning(
                    "Ignored Local NGO build request because a Player " +
                    "build is already running.");
                return;
            }
            if (WasBuildRecentlyCompleted(buildKey))
            {
                UnityEngine.Debug.LogWarning(
                    $"Ignored duplicate '{buildKey}' build request " +
                    "because the same build completed recently.");
                return;
            }

            isBuilding = true;
            bool completed = false;
            try
            {
                buildAction();
                completed = true;
            }
            finally
            {
                isBuilding = false;
                if (completed)
                    SessionState.SetString(
                        BuildCompletedGuardKeyPrefix + buildKey,
                        DateTime.UtcNow.Ticks.ToString());
            }
        }

        private static void Build(
            string scene,
            string output,
            bool dedicatedServer,
            bool uosOnline = false)
        {
            string directory =
                Path.GetDirectoryName(output);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Local NGO build output directory is invalid.");
            Directory.CreateDirectory(directory);
            var scenes = new List<string>
            {
                scene,
                LobbyScene,
                GameScene,
            };
            var options =
                new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = output,
                    target =
                        BuildTarget.StandaloneWindows64,
                    targetGroup =
                        BuildTargetGroup.Standalone,
                    options =
                        BuildOptions
                            .Development,
                    subtarget = dedicatedServer
                        ? (int)StandaloneBuildSubtarget
                            .Server
                        : (int)StandaloneBuildSubtarget
                            .Player,
                    extraScriptingDefines = uosOnline
                        ? new[] { UosOnlineBuildDefine }
                        : Array.Empty<string>(),
                };
            BuildReport report =
                BuildPipeline.BuildPlayer(options);
            if (report.summary.result !=
                BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"{(dedicatedServer ? "Server" : "Client")} build failed: {report.summary.result}.");
        }
    }
}
