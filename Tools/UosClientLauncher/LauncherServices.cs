using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FrameSyncMoba.UosClientLauncher;

internal static class ProjectLocator
{
    public static string? FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 10 && directory != null; depth++)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Docs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal static class LauncherSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FrameSyncMobaDemo",
        "UosClientLauncher",
        "launcher.settings.json");

    public static LauncherSettings LoadOrDefault(
        string path,
        string? projectRoot)
    {
        if (!File.Exists(path))
        {
            return LauncherSettings.CreateDefault(projectRoot);
        }

        try
        {
            LauncherSettings? settings = JsonSerializer.Deserialize<LauncherSettings>(
                File.ReadAllText(path, Encoding.UTF8),
                JsonOptions);
            if (settings == null)
            {
                return LauncherSettings.CreateDefault(projectRoot);
            }

            settings.Normalize(projectRoot);
            return settings;
        }
        catch (Exception exception)
        {
            string backupPath = path + ".invalid-" +
                                DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                File.Copy(path, backupPath, overwrite: false);
            }
            catch
            {
                // Loading still falls back safely if a corrupt-file backup fails.
            }

            Debug.WriteLine(
                $"Failed to load launcher settings: {exception}");
            return LauncherSettings.CreateDefault(projectRoot);
        }
    }

    public static void Save(string path, LauncherSettings settings)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(settings, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

internal static class LaunchArgumentBuilder
{
    public static IReadOnlyList<string> Build(
        LauncherSettings settings,
        ClientLaunchProfile profile,
        string logPath)
    {
        List<string> arguments = new()
        {
            "-onlineFlow",
            $"--TestAccountId={profile.AccountId.Trim()}",
            "-logFile",
            logPath
        };

        AddValue(
            arguments,
            "-matchmakingConfigId",
            settings.MatchmakingConfigId);
        AddValue(arguments, "-uosRegionId", settings.RegionId);

        if (settings.Windowed)
        {
            arguments.Add("-screen-fullscreen");
            arguments.Add("0");
        }

        arguments.Add("-screen-width");
        arguments.Add(settings.WindowWidth.ToString());
        arguments.Add("-screen-height");
        arguments.Add(settings.WindowHeight.ToString());

        if (settings.ChecksumDetail)
        {
            arguments.Add("-checksumDetail");
        }

        if (settings.DisableFrameSyncDiagnostics)
        {
            arguments.Add("-disableFrameSyncDiagnostics");
        }

        arguments.AddRange(WindowsCommandLine.Split(settings.ExtraArguments));
        return arguments;
    }

    public static string CreateUniqueLogPath(
        string logDirectory,
        ClientLaunchProfile profile)
    {
        Directory.CreateDirectory(logDirectory);
        string title = SanitizeFileName(profile.DisplayName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        return Path.Combine(
            logDirectory,
            $"{title}-{timestamp}-{suffix}.log");
    }

    internal static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            builder.Append(invalidCharacters.Contains(character)
                ? '_'
                : character);
        }

        string result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "UOS-Client" : result;
    }

    private static void AddValue(
        ICollection<string> arguments,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add($"{name}={value.Trim()}");
    }
}

internal static class WindowsCommandLine
{
    public static IReadOnlyList<string> Split(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return Array.Empty<string>();
        }

        IntPtr argumentPointers = CommandLineToArgvW(
            "launcher.exe " + commandLine,
            out int argumentCount);
        if (argumentPointers == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "无法解析额外命令行参数。",
                new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error()));
        }

        try
        {
            string[] result = new string[Math.Max(0, argumentCount - 1)];
            for (int index = 1; index < argumentCount; index++)
            {
                IntPtr valuePointer = Marshal.ReadIntPtr(
                    argumentPointers,
                    index * IntPtr.Size);
                result[index - 1] = Marshal.PtrToStringUni(valuePointer) ??
                                    string.Empty;
            }

            return result;
        }
        finally
        {
            LocalFree(argumentPointers);
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
        out int argumentCount);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}

internal sealed class ManagedClientProcess : IDisposable
{
    public ManagedClientProcess(Process process, string logPath)
    {
        Process = process;
        LogPath = logPath;
    }

    public Process Process { get; }

    public string LogPath { get; }

    public bool HasExited
    {
        get
        {
            try
            {
                return Process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }

    public string Status
    {
        get
        {
            if (!HasExited)
            {
                return "运行中";
            }

            try
            {
                return $"已退出 ({Process.ExitCode})";
            }
            catch
            {
                return "已退出";
            }
        }
    }

    public void Dispose()
    {
        Process.Dispose();
    }
}

internal sealed class ClientProcessManager : IDisposable
{
    private readonly Dictionary<string, ManagedClientProcess> _processes =
        new(StringComparer.Ordinal);

    public ManagedClientProcess Start(
        LauncherSettings settings,
        ClientLaunchProfile profile)
    {
        if (!File.Exists(settings.ClientExecutablePath))
        {
            throw new FileNotFoundException(
                "找不到 UOS 客户端程序。",
                settings.ClientExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(profile.AccountId))
        {
            throw new InvalidOperationException(
                $"{profile.DisplayName} 没有 TestAccountId。");
        }

        if (_processes.TryGetValue(profile.Id, out ManagedClientProcess? old))
        {
            if (!old.HasExited)
            {
                throw new InvalidOperationException(
                    $"{profile.DisplayName} 已在运行。");
            }

            old.Dispose();
            _processes.Remove(profile.Id);
        }

        string logPath = LaunchArgumentBuilder.CreateUniqueLogPath(
            settings.LogDirectory,
            profile);
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.GetFullPath(settings.ClientExecutablePath),
            WorkingDirectory = Path.GetDirectoryName(
                Path.GetFullPath(settings.ClientExecutablePath))!,
            UseShellExecute = false
        };
        foreach (string argument in LaunchArgumentBuilder.Build(
                     settings,
                     profile,
                     logPath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process = Process.Start(startInfo) ??
                          throw new InvalidOperationException(
                              "Windows 没有返回已启动的客户端进程。");
        ManagedClientProcess managed = new(process, logPath);
        _processes.Add(profile.Id, managed);
        _ = WindowTitleService.TrySetAsync(
            process,
            profile.WindowTitle,
            TimeSpan.FromSeconds(30));
        return managed;
    }

    public ManagedClientProcess? Get(string profileId)
    {
        return _processes.GetValueOrDefault(profileId);
    }

    public async Task StopAsync(string profileId)
    {
        if (!_processes.TryGetValue(
                profileId,
                out ManagedClientProcess? managed) ||
            managed.HasExited)
        {
            return;
        }

        Process process = managed.Process;
        try
        {
            process.CloseMainWindow();
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(3));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // The process exited between the status check and the stop request.
        }
    }

    public void Dispose()
    {
        foreach (ManagedClientProcess process in _processes.Values)
        {
            process.Dispose();
        }

        _processes.Clear();
    }
}

internal static class WindowTitleService
{
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    public static async Task<bool> TrySetAsync(
        Process process,
        string? title,
        TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool changed = false;
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (process.HasExited)
                {
                    return changed;
                }

                if (TrySetForProcess(process.Id, title.Trim()))
                {
                    changed = true;
                }
            }
            catch (InvalidOperationException)
            {
                return changed;
            }
            await Task.Delay(200);
        }

        return changed;
    }

    private static bool TrySetForProcess(int processId, string title)
    {
        bool changed = false;
        EnumWindows(
            (window, _) =>
            {
                GetWindowThreadProcessId(window, out uint ownerProcessId);
                if (ownerProcessId != (uint)processId ||
                    !IsWindowVisible(window))
                {
                    return true;
                }

                if (SetWindowText(window, title))
                {
                    changed = true;
                }

                return true;
            },
            IntPtr.Zero);
        return changed;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsCallback callback,
        IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowText(IntPtr window, string text);
}
