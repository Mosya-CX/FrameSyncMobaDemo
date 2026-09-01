using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        public const string ReleaseClientBuildRoot =
            "Builds/Demo/Game";
        public const string ReleaseClientExecutableName =
            "AAALOL.exe";
        public const string CdnPackagerProjectPath =
            "Tools/UosGameLauncher/FrameSyncMoba.GameLauncher.csproj";
        public const string CdnUploadBuildRoot =
            "Builds/CdnUpload";
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

        public static void BuildReleaseClient(
            string clientVersion,
            bool buildCdnPackage)
        {
            string normalizedVersion = buildCdnPackage
                ? NormalizeReleaseClientVersion(clientVersion)
                : string.Empty;
            RunExclusive(
                "client-windows-release",
                () =>
                {
                    BuildReleaseClientCore();
                    if (buildCdnPackage)
                        BuildReleaseCdnPackage(normalizedVersion);
                });
        }

        public static string NormalizeReleaseClientVersion(string value)
        {
            string normalized = value == null
                ? string.Empty
                : value.Trim();
            if (!Version.TryParse(normalized, out Version version) ||
                version.Major < 0 ||
                normalized.Length > 64)
            {
                throw new ArgumentException(
                    "客户端版本必须是数字版本号，例如 1.0.0。",
                    nameof(value));
            }

            return normalized;
        }

        public static string GetReleaseClientExecutablePath()
        {
            return Path.GetFullPath(
                Path.Combine(
                    GetProjectRoot(),
                    ReleaseClientBuildRoot,
                    ReleaseClientExecutableName));
        }

        public static string GetCdnPackageOutputRoot(string clientVersion)
        {
            string normalizedVersion =
                NormalizeReleaseClientVersion(clientVersion);
            return Path.GetFullPath(
                Path.Combine(
                    GetProjectRoot(),
                    CdnUploadBuildRoot,
                    normalizedVersion));
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, ".."));
        }

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Build Client + Server (UOS, Once)")]
        public static void BuildUosClientAndServerOnce()
        {
            RunExclusive(
                "both-uos",
                () =>
                {
                    RunCompositeBuildStepOnce(
                        "client-windows-uos",
                        "UOS Windows client",
                        BuildClientUosCore);
                    RunCompositeBuildStepOnce(
                        "server-linux-uos",
                        "UOS Linux server",
                        BuildServerLinuxCore);
                });
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
                "client-windows-release",
                "both-local",
                "both-uos",
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
            UosServerUploadPackage package =
                UosServerUploadPackager.CreateArchive(
                    Path.GetDirectoryName(output),
                    Path.GetFullPath(
                        UosServerUploadPackager.DefaultUploadRoot),
                    DateTime.Now);
            UnityEngine.Debug.Log(
                UosServerUploadPackager.FormatSuccessLog(package));
        }

        private static void BuildLinuxServer(string output)
        {
            EnsureStandaloneBuildTarget(
                BuildTarget.StandaloneLinux64,
                StandaloneBuildSubtarget.Server);
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
            string[] scriptingDefines =
                FrameSyncDiagnosticBuildOptions
                    .ComposeScriptingDefines(
                        true,
                        UosOnlineBuildDefine);
            LogDiagnosticBuildMode(
                "UOS Linux server",
                scriptingDefines);
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
                        scriptingDefines,
                };
            BuildReport report;
            using (new AddressablesPlayerBuildScope(true))
                report = BuildPipeline.BuildPlayer(options);
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

        private static void BuildReleaseClientCore()
        {
            string output = GetReleaseClientExecutablePath();
            PrepareReleaseClientOutput(output);
            Build(
                ClientScene,
                output,
                false,
                true,
                false);
            ValidateReleaseClientOutput(output);
        }

        private static void PrepareReleaseClientOutput(
            string executablePath)
        {
            string expectedRoot = Path.GetFullPath(
                Path.Combine(GetProjectRoot(), ReleaseClientBuildRoot));
            string actualRoot = Path.GetFullPath(
                Path.GetDirectoryName(executablePath) ?? string.Empty);
            if (!string.Equals(
                    expectedRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    actualRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "拒绝清理非正式发布目录：" + actualRoot);
            }

            if (Directory.Exists(expectedRoot))
                Directory.Delete(expectedRoot, true);
        }

        private static void ValidateReleaseClientOutput(string executablePath)
        {
            if (!string.Equals(
                    Path.GetFileName(executablePath),
                    ReleaseClientExecutableName,
                    StringComparison.Ordinal) ||
                !File.Exists(executablePath))
            {
                throw new InvalidOperationException(
                    $"发布客户端入口必须是 {ReleaseClientExecutableName}。");
            }

            string directory = Path.GetDirectoryName(executablePath);
            string dataDirectory = Path.Combine(directory, "AAALOL_Data");
            if (!Directory.Exists(dataDirectory))
            {
                throw new InvalidOperationException(
                    "发布客户端缺少 AAALOL_Data。请勿混用 Builds/UosClient 测试包。");
            }
        }

        private static void BuildReleaseCdnPackage(string clientVersion)
        {
            string projectRoot = GetProjectRoot();
            string projectPath = Path.GetFullPath(
                Path.Combine(projectRoot, CdnPackagerProjectPath));
            string sourceRoot = Path.GetFullPath(
                Path.Combine(projectRoot, ReleaseClientBuildRoot));
            string outputRoot = GetCdnPackageOutputRoot(clientVersion);
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException(
                    "没有找到正式 Launcher CDN 分片器项目。",
                    projectPath);
            }

            ValidateReleaseClientOutput(
                Path.Combine(sourceRoot, ReleaseClientExecutableName));
            string arguments = ComposeCdnPackagerArguments(
                projectPath,
                sourceRoot,
                outputRoot,
                clientVersion);
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "无法启动 CDN 分片器进程。");
                }

                Task<string> outputTask =
                    process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask =
                    process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);
                string standardOutput = outputTask.Result.Trim();
                string standardError = errorTask.Result.Trim();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "CDN 签名分片失败。\n" +
                        (string.IsNullOrEmpty(standardError)
                            ? standardOutput
                            : standardError));
                }

                if (!string.IsNullOrEmpty(standardOutput))
                    UnityEngine.Debug.Log(standardOutput);
            }

            UnityEngine.Debug.Log(
                $"[Build] Release CDN package ready: {outputRoot}");
        }

        public static string ComposeCdnPackagerArguments(
            string projectPath,
            string sourceRoot,
            string outputRoot,
            string clientVersion)
        {
            string normalizedVersion =
                NormalizeReleaseClientVersion(clientVersion);
            return
                "run --project " + QuoteProcessArgument(projectPath) +
                " -c Release -- --build-cdn-package" +
                " --version " + normalizedVersion +
                " --source " + QuoteProcessArgument(sourceRoot) +
                " --output " + QuoteProcessArgument(outputRoot);
        }

        private static string QuoteProcessArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf('"') >= 0)
            {
                throw new ArgumentException(
                    "进程参数路径为空或包含不允许的双引号。",
                    nameof(value));
            }

            return "\"" + value + "\"";
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
            BuildTarget? previousTarget = null;
            StandaloneBuildSubtarget? previousSubtarget = null;
            try
            {
                // Remember the editor's build target before the menu build
                // switches it (e.g. to a Server subtarget). After the build,
                // restore it so the editor never stays in the server target,
                // which excludes client-only assemblies such as
                // FrameSyncMoba.ClientContent (UNITY_SERVER) and breaks the
                // next script compilation.
                previousTarget =
                    EditorUserBuildSettings
                        .activeBuildTarget;
                previousSubtarget =
                    EditorUserBuildSettings
                        .standaloneBuildSubtarget;
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
                RestoreEditorBuildTarget(
                    previousTarget,
                    previousSubtarget);
            }
        }

        /// <summary>
        /// Returns the active build target to the state captured before a
        /// menu build. Failing to restore only logs a warning: the build
        /// itself already succeeded and the editor target is secondary.
        /// </summary>
        private static void RestoreEditorBuildTarget(
            BuildTarget? target,
            StandaloneBuildSubtarget? subtarget)
        {
            if (!target.HasValue ||
                !subtarget.HasValue)
            {
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget ==
                    target.Value &&
                EditorUserBuildSettings.standaloneBuildSubtarget ==
                    subtarget.Value)
            {
                return;
            }
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget !=
                    target.Value)
                {
                    if (!EditorUserBuildSettings
                            .SwitchActiveBuildTarget(
                                BuildTargetGroup.Standalone,
                                target.Value))
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[BuildTarget] Failed to restore active " +
                            $"build target {target.Value}/{subtarget.Value}.");
                        return;
                    }
                }
                EditorUserBuildSettings.standaloneBuildSubtarget =
                    subtarget.Value;
                UnityEngine.Debug.Log(
                    $"[BuildTarget] Restored active target: " +
                    $"{target.Value}/{subtarget.Value}.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BuildTarget] Restore failed: {exception}");
            }
        }

        private static void RunCompositeBuildStepOnce(
            string buildKey,
            string displayName,
            Action buildAction)
        {
            if (WasBuildRecentlyCompleted(buildKey))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Build] Skipped duplicate {displayName} step " +
                    "because it completed recently.");
                return;
            }

            buildAction();
            MarkBuildCompleted(buildKey);
        }

        private static void MarkBuildCompleted(string buildKey)
        {
            SessionState.SetString(
                BuildCompletedGuardKeyPrefix + buildKey,
                DateTime.UtcNow.Ticks.ToString());
        }

        private static void Build(
            string scene,
            string output,
            bool dedicatedServer,
            bool uosOnline = false,
            bool developmentBuild = true)
        {
            EnsureStandaloneBuildTarget(
                BuildTarget.StandaloneWindows64,
                dedicatedServer
                    ? StandaloneBuildSubtarget.Server
                    : StandaloneBuildSubtarget.Player);
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
            string[] scriptingDefines =
                FrameSyncDiagnosticBuildOptions
                    .ComposeScriptingDefines(
                        uosOnline,
                        UosOnlineBuildDefine);
            LogDiagnosticBuildMode(
                dedicatedServer
                    ? "Local Windows server"
                    : uosOnline
                        ? "UOS Windows client"
                        : "Local Windows client",
                scriptingDefines);
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
                        developmentBuild
                            ? BuildOptions.Development
                            : BuildOptions.None,
                    subtarget = dedicatedServer
                        ? (int)StandaloneBuildSubtarget
                            .Server
                        : (int)StandaloneBuildSubtarget
                            .Player,
                    extraScriptingDefines =
                        scriptingDefines,
                };
            BuildReport report;
            if (!dedicatedServer)
                AddressablesClientBuildAudit.PrepareOutput(output);
            using (new AddressablesPlayerBuildScope(dedicatedServer))
                report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result !=
                BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"{(dedicatedServer ? "Server" : "Client")} build failed: {report.summary.result}.");
            if (!dedicatedServer)
            {
                AddressablesClientBuildAudit.ValidateOutput(
                    output,
                    BuildTarget.StandaloneWindows64);
            }
        }

        private static void EnsureStandaloneBuildTarget(
            BuildTarget target,
            StandaloneBuildSubtarget subtarget)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone,
                    target))
            {
                throw new InvalidOperationException(
                    $"Unable to switch the active build target to {target} before building Addressables content.");
            }

            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;
            if (EditorUserBuildSettings.activeBuildTarget != target ||
                EditorUserBuildSettings.standaloneBuildSubtarget != subtarget)
            {
                throw new InvalidOperationException(
                    $"Active build target mismatch before Player build. " +
                    $"Expected {target}/{subtarget}, actual " +
                    $"{EditorUserBuildSettings.activeBuildTarget}/" +
                    $"{EditorUserBuildSettings.standaloneBuildSubtarget}.");
            }

            UnityEngine.Debug.Log(
                $"[BuildTarget] Active target confirmed: {target}/{subtarget}.");
        }

        private static void LogDiagnosticBuildMode(
            string targetName,
            IReadOnlyList<string> scriptingDefines)
        {
            bool diagnosticsIncluded = false;
            for (int i = 0; i < scriptingDefines.Count; i++)
            {
                if (string.Equals(
                        scriptingDefines[i],
                        FrameSyncDiagnosticBuildOptions
                            .DiagnosticsBuildDefine,
                        StringComparison.Ordinal))
                {
                    diagnosticsIncluded = true;
                    break;
                }
            }
            UnityEngine.Debug.Log(
                $"[Build] {targetName}: async diagnostics " +
                (diagnosticsIncluded
                    ? "included."
                    : "fully compiled out."));
        }
    }
}
