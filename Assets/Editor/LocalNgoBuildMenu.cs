using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FrameSyncMoba.EditorTools
{
    public static class LocalNgoBuildMenu
    {
        private const string BuildBothGuardKey =
            "FrameSyncMoba.LocalNgoBuildMenu.LastBuildBothUtcTicks";
        private static readonly TimeSpan BuildBothRetryGuard =
            TimeSpan.FromMinutes(10);
        private static bool isBuilding;

        public const string ServerScene =
            "Assets/Scenes/ServerBootstrap.unity";
        public const string ClientScene =
            "Assets/Scenes/ClientBootstrap.unity";
        public const string BuildRoot =
            "Builds/LocalNgo";

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Server")]
        public static void BuildServer()
        {
            RunExclusive(BuildServerCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Client")]
        public static void BuildClient()
        {
            RunExclusive(BuildClientCore);
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Both")]
        public static void BuildBoth()
        {
            if (WasBuildBothRecentlyRequested())
            {
                UnityEngine.Debug.LogWarning(
                    "Ignored duplicate Local NGO Build Both request. " +
                    "Use the individual build commands or clear the " +
                    "retry guard before intentionally rebuilding.");
                return;
            }

            SessionState.SetString(
                BuildBothGuardKey,
                DateTime.UtcNow.Ticks.ToString());
            RunExclusive(
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
            SessionState.EraseString(BuildBothGuardKey);
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

        private static void BuildClientCore()
        {
            string output = Path.GetFullPath(
                Path.Combine(
                    BuildRoot,
                    "Client",
                    "FrameSyncMobaClient.exe"));
            Build(ClientScene, output, false);
        }

        private static bool WasBuildBothRecentlyRequested()
        {
            string value = SessionState.GetString(
                BuildBothGuardKey,
                string.Empty);
            return long.TryParse(value, out long ticks) &&
                   DateTime.UtcNow - new DateTime(
                       ticks,
                       DateTimeKind.Utc) < BuildBothRetryGuard;
        }

        private static void RunExclusive(Action buildAction)
        {
            if (isBuilding || BuildPipeline.isBuildingPlayer)
            {
                UnityEngine.Debug.LogWarning(
                    "Ignored Local NGO build request because a Player " +
                    "build is already running.");
                return;
            }

            isBuilding = true;
            try
            {
                buildAction();
            }
            finally
            {
                isBuilding = false;
            }
        }

        private static void Build(
            string scene,
            string output,
            bool dedicatedServer)
        {
            string directory =
                Path.GetDirectoryName(output);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Local NGO build output directory is invalid.");
            Directory.CreateDirectory(directory);
            var options =
                new BuildPlayerOptions
                {
                    scenes =
                        new[]
                        {
                            scene,
                        },
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
