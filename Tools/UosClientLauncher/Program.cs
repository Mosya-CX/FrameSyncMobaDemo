namespace FrameSyncMoba.UosClientLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(argument =>
                string.Equals(
                    argument,
                    "--test-client",
                    StringComparison.OrdinalIgnoreCase)))
        {
            Thread.Sleep(150);
            return 0;
        }

        if (args.Any(argument =>
                string.Equals(
                    argument,
                    "--self-test",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return LauncherSelfTest.Run();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}

internal static class LauncherSelfTest
{
    public static int Run()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrameSyncMoba-UosLauncher-Test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            LauncherSettings settings = LauncherSettings.CreateDefault(null);
            settings.LogDirectory = temporaryDirectory;
            settings.MatchmakingConfigId = "test config";
            settings.RegionId = "test-region";
            settings.WindowWidth = 1024;
            settings.WindowHeight = 640;
            settings.ChecksumDetail = true;
            settings.DisableFrameSyncDiagnostics = true;
            settings.ExtraArguments = "-customFlag \"quoted value\"";
            ClientLaunchProfile profile = settings.Profiles[0];
            profile.AccountId = "self-test-account";

            string firstLog = LaunchArgumentBuilder.CreateUniqueLogPath(
                temporaryDirectory,
                profile);
            string secondLog = LaunchArgumentBuilder.CreateUniqueLogPath(
                temporaryDirectory,
                profile);
            Assert(firstLog != secondLog, "Log paths must be unique.");

            IReadOnlyList<string> arguments = LaunchArgumentBuilder.Build(
                settings,
                profile,
                firstLog);
            Assert(arguments.Contains("-onlineFlow"), "Missing online flow.");
            Assert(
                arguments.Contains("--TestAccountId=self-test-account"),
                "Missing test account.");
            Assert(arguments.Contains(firstLog), "Missing log path.");
            Assert(arguments.Contains("quoted value"), "Quoted arg was split.");
            Assert(
                arguments.Contains("-disableFrameSyncDiagnostics"),
                "Missing diagnostics switch.");

            string settingsPath = Path.Combine(
                temporaryDirectory,
                "settings.json");
            LauncherSettingsStore.Save(settingsPath, settings);
            LauncherSettings restored = LauncherSettingsStore.LoadOrDefault(
                settingsPath,
                null);
            Assert(restored.Profiles.Count == 2, "Profile round trip failed.");
            Assert(
                restored.Profiles[0].AccountId == "self-test-account",
                "Account round trip failed.");
            Assert(
                restored.ExtraArguments == settings.ExtraArguments,
                "Extra arguments round trip failed.");

            settings.ClientExecutablePath =
                Environment.ProcessPath ??
                throw new InvalidOperationException(
                    "Self-test executable path is unavailable.");
            settings.ExtraArguments = "--test-client";
            using (ClientProcessManager processManager = new())
            {
                ManagedClientProcess child = processManager.Start(
                    settings,
                    profile);
                Assert(child.Process.Id > 0, "Client process has no PID.");
                Assert(
                    child.Process.WaitForExit(5000),
                    "Client process did not exit.");
                Assert(child.HasExited, "Client exit was not observed.");
                Assert(child.Process.ExitCode == 0, "Client process failed.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "FrameSyncMobaDemo-UosClientLauncher-self-test-error.txt"),
                exception.ToString());
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // A failed cleanup must not mask the actual self-test result.
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
