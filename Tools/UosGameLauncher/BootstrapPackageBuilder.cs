using System.IO.Compression;

namespace FrameSyncMoba.GameLauncher;

internal sealed record BootstrapPackageBuildResult(
    string PackagePath,
    long PackageSize,
    string Sha256,
    int LauncherFileCount);

internal static class BootstrapPackageBuilder
{
    private static readonly HashSet<string> AllowedLauncherFiles = new(
        new[]
        {
            "FrameSyncMobaLauncher.exe",
            "launcher.cdn.json",
            "CdnSigningPublicKey.pem",
            "Assets/Launcher/AppIcon.ico",
            "Assets/Launcher/Background.png",
            "Assets/Launcher/Banner.png",
            "Assets/Launcher/Logo.png"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly DateTimeOffset StableZipTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task<BootstrapPackageBuildResult> BuildAsync(
        string launcherDirectory,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        string launcherRoot = Path.GetFullPath(launcherDirectory);
        string destination = Path.GetFullPath(outputPath);
        ValidateLauncherRoot(launcherRoot);
        string? outputDirectory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("无法解析首包输出目录。");
        }

        string launcherPrefix = launcherRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destination.StartsWith(launcherPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("首包 ZIP 不能写入 Launcher 源目录内部。");
        }

        Directory.CreateDirectory(outputDirectory);
        string temporaryPath = destination + ".tmp";
        File.Delete(temporaryPath);
        string[] files = EnumerateLauncherFiles(launcherRoot);

        await using (FileStream output = new(
                         temporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         1024 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry gameDirectory = archive.CreateEntry("Demo/Game/");
            gameDirectory.LastWriteTime = StableZipTimestamp;
            foreach (string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(launcherRoot, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                string entryPath = "Demo/Launcher/" + CdnPath.NormalizeRelative(relative);
                ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
                entry.LastWriteTime = StableZipTimestamp;
                await using Stream target = entry.Open();
                await using FileStream source = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(target, 1024 * 1024, cancellationToken);
            }
        }

        File.Move(temporaryPath, destination, overwrite: true);
        await AuditAsync(destination, files.Length, cancellationToken);
        FileInfo info = new(destination);
        string hash = await CdnHash.FileSha256Async(destination, cancellationToken);
        return new BootstrapPackageBuildResult(destination, info.Length, hash, files.Length);
    }

    private static async Task AuditAsync(
        string packagePath,
        int expectedLauncherFiles,
        CancellationToken cancellationToken)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        int launcherFiles = 0;
        bool hasEmptyGameDirectory = false;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = entry.FullName;
            if (string.Equals(path, "Demo/Game/", StringComparison.Ordinal))
            {
                hasEmptyGameDirectory = true;
                continue;
            }

            if (!path.StartsWith("Demo/Launcher/", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(entry.Name))
            {
                throw new InvalidDataException($"首包 ZIP 出现非预期路径：{path}。");
            }

            launcherFiles++;
            await using Stream content = entry.Open();
            byte[] buffer = new byte[64 * 1024];
            while (await content.ReadAsync(buffer, cancellationToken) != 0)
            {
            }
        }

        if (!hasEmptyGameDirectory || launcherFiles != expectedLauncherFiles)
        {
            throw new InvalidDataException("首包 ZIP 的 Launcher/Game 布局审计失败。");
        }
    }

    private static void ValidateLauncherRoot(string launcherRoot)
    {
        string[] requiredFiles =
        {
            "FrameSyncMobaLauncher.exe",
            "launcher.cdn.json",
            "CdnSigningPublicKey.pem"
        };
        foreach (string requiredFile in requiredFiles)
        {
            if (!File.Exists(Path.Combine(launcherRoot, requiredFile)))
            {
                throw new FileNotFoundException("Launcher 发布目录缺少必需文件。", requiredFile);
            }
        }
    }

    private static string[] EnumerateLauncherFiles(string launcherRoot)
    {
        List<string> files = new();
        Stack<string> directories = new();
        directories.Push(launcherRoot);
        while (directories.Count > 0)
        {
            string directory = directories.Pop();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Launcher 发布目录包含不允许的联接或符号链接：{path}。");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(path);
                    continue;
                }

                string relative = Path.GetRelativePath(launcherRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!AllowedLauncherFiles.Contains(relative))
                {
                    throw new InvalidDataException($"Launcher 发布目录包含未列入首包白名单的文件：{relative}。");
                }

                RejectPrivateKeyMaterial(path, relative);
                files.Add(path);
            }
        }

        return files
            .OrderBy(path => Path.GetRelativePath(launcherRoot, path), StringComparer.Ordinal)
            .ToArray();
    }

    private static void RejectPrivateKeyMaterial(string path, string relative)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".p12", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".key", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"首包拒绝私钥文件：{relative}。");
        }

        if (!extension.Equals(".pem", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string text = File.ReadAllText(path);
        if (text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"首包拒绝 PEM 私钥材料：{relative}。");
        }
    }
}
