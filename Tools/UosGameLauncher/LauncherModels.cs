using System.Text.Json.Serialization;

namespace FrameSyncMoba.GameLauncher;

internal sealed class LauncherSettings
{
    [JsonIgnore]
    public string GameExecutablePath { get; set; } = string.Empty;

    public string LoginName { get; set; } = string.Empty;

    public static LauncherSettings CreateDefault(string? projectRoot)
    {
        return new LauncherSettings
        {
            GameExecutablePath = LauncherPaths.ResolveDefaultGameExecutable(projectRoot)
        };
    }

    public void Normalize(string? projectRoot)
    {
        // The player-facing launcher intentionally has no directory settings.
        // Always derive the formal entry point from the fixed package layout.
        GameExecutablePath = LauncherPaths.ResolveDefaultGameExecutable(projectRoot);

        LoginName ??= string.Empty;
    }
}

internal sealed record GameInstallStatus(
    bool IsReady,
    string Message,
    string? ExecutablePath,
    string? GameDirectory)
{
    public static GameInstallStatus Missing(string message, string? path = null)
    {
        string? directory = string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetDirectoryName(path);
        return new GameInstallStatus(false, message, path, directory);
    }

    public static GameInstallStatus Ready(string path)
    {
        return new GameInstallStatus(
            true,
            "客户端文件完整，可以启动",
            path,
            Path.GetDirectoryName(path));
    }
}
