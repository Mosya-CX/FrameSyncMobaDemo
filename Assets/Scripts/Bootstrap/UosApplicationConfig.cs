using System;
using Unity.UOS.Common;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Single source of application-level UOS configuration.
    ///
    /// The Matchmaking config ID is read from the UOS Launcher runtime
    /// settings (<see cref="Settings.MatchmakingConfigID"/>, backed by
    /// Assets/Resources/UOSSettings.asset and included in builds). Per-launch
    /// command-line arguments are explicit overrides for CI/testing and never
    /// replace the authored environment settings. Scene-serialized copies of
    /// these values are not supported anymore.
    /// </summary>
    public static class UosApplicationConfig
    {
        public const string MatchmakingConfigIdArg =
            "-matchmakingConfigId";
        public const string RegionIdArg =
            "-uosRegionId";
        public const string OnlineFlowArg =
            "-onlineFlow";
        public const string LocalFlowArg =
            "-localFlow";
        public const string ProfileTestServerEnvironmentVariable =
            "IS_TEST_SERVER";

        /// <summary>
        /// Test-only override. Takes precedence over command-line arguments
        /// and the Launcher settings so EditMode tests stay deterministic.
        /// </summary>
        public static string MatchmakingConfigIdOverride;

        /// <summary>Test-only override for the optional UOS region ID.</summary>
        public static string RegionIdOverride;

        /// <summary>
        /// Test-only flow-mode override. Takes precedence over command-line
        /// arguments so PlayMode tests can force the online/local flow.
        /// </summary>
        public static bool? FlowModeOverride;

        /// <summary>
        /// Injectable Launcher settings reader. Defaults to
        /// <see cref="Settings.MatchmakingConfigID"/>. Override only in tests.
        /// </summary>
        public static Func<string> SettingsMatchmakingConfigIdProvider =
            ReadSettingsMatchmakingConfigId;

        /// <summary>
        /// Resolves whether the process should use the online UOS flow.
        /// Command-line <see cref="OnlineFlowArg"/>/<see cref="LocalFlowArg"/>
        /// override the scene-authored default; otherwise the serialized
        /// value wins.
        /// </summary>
        public static bool IsOnlineFlowRequested(
            bool serializedValue,
            string[] args = null)
        {
            string[] source =
                args ?? Environment.GetCommandLineArgs();
            if (FlowModeOverride.HasValue)
                return FlowModeOverride.Value;
            if (ContainsArg(source, OnlineFlowArg))
                return true;
            if (ContainsArg(source, LocalFlowArg))
                return false;
#if FRAME_SYNC_MOBA_UOS_ONLINE
            return true;
#else
            return serializedValue;
#endif
        }

        /// <summary>
        /// UOS marks the temporary server used to validate a Multiverse
        /// Profile Revision with IS_TEST_SERVER=true. Matchmaking Server SDK
        /// deliberately rejects that environment, so the bootstrap must only
        /// initialize Multiverse, listen and report Ready for this branch.
        /// </summary>
        public static bool IsProfileTestServer()
        {
            return IsProfileTestServer(
                false,
                Environment.GetEnvironmentVariable(
                    ProfileTestServerEnvironmentVariable));
        }

        public static bool IsProfileTestServer(
            string environmentValue)
        {
            return IsProfileTestServer(false, environmentValue);
        }

        public static bool IsProfileTestServer(
            bool multiverseServerInfoFlag,
            string environmentValue)
        {
            return multiverseServerInfoFlag ||
                   string.Equals(
                       environmentValue?.Trim(),
                       "true",
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the UOS Matchmaking config ID from the single source:
        /// test override, command-line argument, then Launcher settings.
        /// Returns null/empty when not configured anywhere.
        /// </summary>
        public static string ResolveMatchmakingConfigId(
            string[] args = null)
        {
            if (!string.IsNullOrWhiteSpace(
                    MatchmakingConfigIdOverride))
                return MatchmakingConfigIdOverride;
            string[] source =
                args ?? Environment.GetCommandLineArgs();
            string argument = ReadArgValue(
                source,
                MatchmakingConfigIdArg);
            if (!string.IsNullOrWhiteSpace(argument))
                return argument;
            return SettingsMatchmakingConfigIdProvider != null
                ? SettingsMatchmakingConfigIdProvider()
                : null;
        }

        /// <summary>
        /// Resolves the optional UOS region ID from the test override or the
        /// command line. Null means the UOS default region.
        /// </summary>
        public static string ResolveRegionId(
            string[] args = null)
        {
            if (!string.IsNullOrWhiteSpace(RegionIdOverride))
                return RegionIdOverride;
            string[] source =
                args ?? Environment.GetCommandLineArgs();
            return ReadArgValue(source, RegionIdArg);
        }

        /// <summary>Restores defaults after tests.</summary>
        public static void ResetTestState()
        {
            MatchmakingConfigIdOverride = null;
            RegionIdOverride = null;
            FlowModeOverride = null;
            SettingsMatchmakingConfigIdProvider =
                ReadSettingsMatchmakingConfigId;
        }

        private static string ReadSettingsMatchmakingConfigId()
        {
            try
            {
                return Settings.MatchmakingConfigID;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[UosApplicationConfig] UOS Launcher settings " +
                    "unavailable: " + exception.Message);
                return null;
            }
        }

        private static bool ContainsArg(
            string[] args,
            string name)
        {
            if (args == null)
                return false;
            for (int i = 0;
                 i < args.Length;
                 i++)
            {
                string item = args[i];
                if (item == null)
                    continue;
                if (string.Equals(
                        item,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
                if (item.StartsWith(
                        name + "=",
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string ReadArgValue(
            string[] args,
            string name)
        {
            if (args == null)
                return null;
            for (int i = 0;
                 i < args.Length;
                 i++)
            {
                string item = args[i];
                if (item == null)
                    continue;
                if (item.StartsWith(
                        name + "=",
                        StringComparison.OrdinalIgnoreCase))
                    return item.Substring(name.Length + 1);
                if (string.Equals(
                        item,
                        name,
                        StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                    return args[i + 1];
            }
            return null;
        }
    }
}
