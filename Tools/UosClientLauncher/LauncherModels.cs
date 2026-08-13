using System.Text.Json.Serialization;

namespace FrameSyncMoba.UosClientLauncher;

internal sealed class LauncherSettings
{
    public string ClientExecutablePath { get; set; } = string.Empty;

    public string LogDirectory { get; set; } = string.Empty;

    public string MatchmakingConfigId { get; set; } = string.Empty;

    public string RegionId { get; set; } = string.Empty;

    public int WindowWidth { get; set; } = 1280;

    public int WindowHeight { get; set; } = 720;

    public bool Windowed { get; set; } = true;

    public bool ChecksumDetail { get; set; }

    public bool DisableFrameSyncDiagnostics { get; set; }

    public string ExtraArguments { get; set; } = string.Empty;

    public List<ClientLaunchProfile> Profiles { get; set; } = new();

    public static LauncherSettings CreateDefault(string? projectRoot)
    {
        string executablePath = projectRoot == null
            ? string.Empty
            : Path.Combine(
                projectRoot,
                "Builds",
                "UosClient",
                "FrameSyncMobaClient.exe");
        string logDirectory = projectRoot == null
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "FrameSyncMobaDemo",
                "ClientLogs")
            : Path.Combine(projectRoot, "Logs", "UosClient");

        return new LauncherSettings
        {
            ClientExecutablePath = executablePath,
            LogDirectory = logDirectory,
            Profiles =
            {
                ClientLaunchProfile.Create("UOS Client 1"),
                ClientLaunchProfile.Create("UOS Client 2")
            }
        };
    }

    public void Normalize(string? projectRoot)
    {
        Profiles ??= new List<ClientLaunchProfile>();
        Profiles.RemoveAll(profile => profile == null);
        foreach (ClientLaunchProfile profile in Profiles)
        {
            profile.EnsureValidIdentity();
        }

        while (Profiles.Count < 2)
        {
            Profiles.Add(
                ClientLaunchProfile.Create(
                    $"UOS Client {Profiles.Count + 1}"));
        }

        if (string.IsNullOrWhiteSpace(ClientExecutablePath) &&
            projectRoot != null)
        {
            ClientExecutablePath = Path.Combine(
                projectRoot,
                "Builds",
                "UosClient",
                "FrameSyncMobaClient.exe");
        }

        if (string.IsNullOrWhiteSpace(LogDirectory))
        {
            LogDirectory = projectRoot == null
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "FrameSyncMobaDemo",
                    "ClientLogs")
                : Path.Combine(projectRoot, "Logs", "UosClient");
        }

        WindowWidth = Math.Clamp(WindowWidth, 640, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 480, 4320);
    }
}

internal sealed class ClientLaunchProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public bool Enabled { get; set; } = true;

    public string AccountId { get; set; } = Guid.NewGuid().ToString("N");

    public string WindowTitle { get; set; } = "UOS Client";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(WindowTitle)
        ? AccountId
        : WindowTitle;

    public static ClientLaunchProfile Create(string title)
    {
        return new ClientLaunchProfile
        {
            WindowTitle = title
        };
    }

    public void EnsureValidIdentity()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(AccountId))
        {
            AccountId = Guid.NewGuid().ToString("N");
        }
    }
}
