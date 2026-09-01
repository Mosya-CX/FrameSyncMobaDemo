using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FrameSyncMoba.GameLauncher;

internal static class LauncherPaths
{
    public const string GameExecutableName = "AAALOL.exe";

    public static string ResolveDefaultGameExecutable(string? projectRoot)
    {
        string siblingPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "Game", GameExecutableName));
        if (File.Exists(siblingPath) || projectRoot == null)
        {
            return siblingPath;
        }

        return Path.Combine(
            projectRoot,
            "Builds",
            "Demo",
            "Game",
            GameExecutableName);
    }

}

internal static class ProjectLocator
{
    public static string? FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 12 && directory != null; depth++)
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

internal static class GameInstallLocator
{
    public static GameInstallStatus Check(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return GameInstallStatus.Missing(
                "未找到正式客户端，请将完整客户端放入 Builds\\Demo\\Game。");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(executablePath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return GameInstallStatus.Missing("客户端路径无效：" + exception.Message);
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallStatus.Missing("客户端路径必须指向 .exe 文件。", fullPath);
        }

        if (!string.Equals(
                Path.GetFileName(fullPath),
                LauncherPaths.GameExecutableName,
                StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallStatus.Missing(
                $"正式客户端入口必须是 {LauncherPaths.GameExecutableName}。",
                fullPath);
        }

        if (!File.Exists(fullPath))
        {
            return GameInstallStatus.Missing(
                "未找到 AAALOL.exe，请将完整客户端放入 Builds\\Demo\\Game。",
                fullPath);
        }

        string? gameDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            return GameInstallStatus.Missing("无法解析客户端所在目录。", fullPath);
        }

        string dataDirectory = Path.Combine(
            gameDirectory,
            Path.GetFileNameWithoutExtension(fullPath) + "_Data");
        if (!Directory.Exists(dataDirectory))
        {
            return GameInstallStatus.Missing(
                "客户端目录不完整：缺少 Unity 的 *_Data 文件夹。",
                fullPath);
        }

        return GameInstallStatus.Ready(fullPath);
    }

    public static void ValidateOrThrow(string executablePath)
    {
        GameInstallStatus status = Check(executablePath);
        if (!status.IsReady)
        {
            throw new InvalidOperationException(status.Message);
        }
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
        "GameLauncher",
        "launcher.settings.json");

    public static LauncherSettings LoadOrDefault(string path, string? projectRoot)
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
            string backupPath = path + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                File.Copy(path, backupPath, overwrite: false);
            }
            catch
            {
                // A settings backup is best effort; defaults remain safe.
            }

            Debug.WriteLine($"Failed to load launcher settings: {exception}");
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

internal static class GameLaunchArgumentBuilder
{
    public static IReadOnlyList<string> Build(LauncherSettings settings)
    {
        List<string> arguments = new()
        {
            "-onlineFlow"
        };

        AddValue(arguments, "--TestAccountId", settings.LoginName);
        return arguments;
    }

    private static void AddValue(
        ICollection<string> arguments,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add($"{name}={value.Trim()}");
        }
    }
}

internal sealed class ManagedGameProcess : IDisposable
{
    public ManagedGameProcess(Process process)
    {
        Process = process;
    }

    public Process Process { get; }

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
                return "游戏运行中";
            }

            try
            {
                return $"游戏已退出（{Process.ExitCode}）";
            }
            catch
            {
                return "游戏已退出";
            }
        }
    }

    public void Dispose()
    {
        Process.Dispose();
    }
}

internal sealed class GameProcessManager : IDisposable
{
    private ManagedGameProcess? _managed;

    public ManagedGameProcess? Current => _managed;

    public static bool IsExecutableRunning(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            return false;
        }

        string expected = Path.GetFullPath(executablePath);
        string processName = Path.GetFileNameWithoutExtension(expected);
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    string? runningPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(runningPath) &&
                        string.Equals(
                            Path.GetFullPath(runningPath),
                            expected,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or System.ComponentModel.Win32Exception or
                        NotSupportedException)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public ManagedGameProcess Start(
        LauncherSettings settings,
        bool allowIncompleteInstallForSelfTest = false)
    {
        if (_managed is { HasExited: false })
        {
            throw new InvalidOperationException("客户端已经在运行。");
        }

        _managed?.Dispose();
        _managed = null;

        string executablePath = Path.GetFullPath(settings.GameExecutablePath);
        if (!allowIncompleteInstallForSelfTest)
        {
            GameInstallLocator.ValidateOrThrow(executablePath);
            if (IsExecutableRunning(executablePath))
            {
                throw new GameClientRunningException();
            }
        }
        else if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("自测客户端程序不存在。", executablePath);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false
        };
        if (allowIncompleteInstallForSelfTest && IsDotnetHost(executablePath))
        {
            string entryPoint = Environment.GetCommandLineArgs().FirstOrDefault() ??
                                 throw new InvalidOperationException("自测入口程序集路径不可用。");
            startInfo.ArgumentList.Add(entryPoint);
            startInfo.ArgumentList.Add("--test-client");
        }
        else
        {
            foreach (string argument in GameLaunchArgumentBuilder.Build(settings))
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (allowIncompleteInstallForSelfTest)
            {
                startInfo.ArgumentList.Add("--test-client");
            }
        }

        Process process = Process.Start(startInfo) ??
                          throw new InvalidOperationException("Windows 没有返回已启动的客户端进程。");
        _managed = new ManagedGameProcess(process);
        return _managed;
    }

    private static bool IsDotnetHost(string executablePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(executablePath);
        return string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase);
    }

    public async Task StopAsync()
    {
        ManagedGameProcess? managed = _managed;
        if (managed == null || managed.HasExited)
        {
            return;
        }

        Process process = managed.Process;
        try
        {
            process.CloseMainWindow();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // The process exited between the timeout and the kill request.
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the status check and the close request.
        }
    }

    public void Dispose()
    {
        _managed?.Dispose();
        _managed = null;
    }
}

internal sealed class LauncherArtwork : IDisposable
{
    private readonly List<IDisposable> _ownedResources = new();

    private LauncherArtwork(Image? logo, Image? banner, Image? background, Icon? icon)
    {
        Logo = Track(logo);
        Banner = Track(banner);
        Background = Track(background);
        AppIcon = Track(icon);
    }

    public Image? Logo { get; }

    public Image? Banner { get; }

    public Image? Background { get; }

    public Icon? AppIcon { get; }

    public static LauncherArtwork Load()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Assets", "Launcher");
        return new LauncherArtwork(
            TryLoadImage(Path.Combine(root, "Logo.png")),
            TryLoadImage(Path.Combine(root, "Banner.png")),
            TryLoadImage(Path.Combine(root, "Background.png")),
            TryLoadIcon(Path.Combine(root, "AppIcon.ico")));
    }

    public void Dispose()
    {
        foreach (IDisposable resource in _ownedResources)
        {
            resource.Dispose();
        }

        _ownedResources.Clear();
    }

    private T? Track<T>(T? resource) where T : class, IDisposable
    {
        if (resource != null)
        {
            _ownedResources.Add(resource);
        }

        return resource;
    }

    private static Image? TryLoadImage(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using Image source = Image.FromFile(path);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Icon? TryLoadIcon(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Icon(path);
        }
        catch
        {
            return null;
        }
    }
}
