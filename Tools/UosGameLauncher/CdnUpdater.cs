using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;

namespace FrameSyncMoba.GameLauncher;

internal enum CdnUpdatePhase
{
    Checking,
    Downloading,
    Installing,
    Verifying,
    Switching,
    Complete
}

internal sealed record CdnUpdateProgress(
    CdnUpdatePhase Phase,
    string Message,
    long CompletedBytes,
    long TotalBytes)
{
    public int Percent => TotalBytes <= 0
        ? 0
        : (int)Math.Clamp(CompletedBytes * 100L / TotalBytes, 0, 100);
}

internal sealed record CdnInstallResult(
    string ClientVersion,
    bool Changed,
    bool UsedFullPackage,
    int DownloadedFileCount,
    int ReusedFileCount);

internal enum CdnRequiredAction
{
    Download,
    Update,
    Start
}

internal sealed record CdnClientCheckResult(
    CdnRequiredAction RequiredAction,
    string? ClientVersion);

internal class CdnDownloadException : IOException
{
    public CdnDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class CdnDownloadIntegrityException : CdnDownloadException
{
    public CdnDownloadIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class GameClientRunningException : InvalidOperationException
{
    public GameClientRunningException()
        : base("客户端正在运行，必须先关闭游戏才能安装或更新。")
    {
    }
}

internal sealed class CdnDownloadClient : IDisposable
{
    private const int BufferSize = 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;

    public CdnDownloadClient(string cacheRoot, TimeSpan timeout)
    {
        _cacheRoot = Path.GetFullPath(cacheRoot);
        Directory.CreateDirectory(_cacheRoot);
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = timeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"FrameSyncMobaLauncher/{LauncherVersion.Current}");
    }

    public string CacheRoot => _cacheRoot;

    public async Task<byte[]> DownloadSmallAsync(
        Uri uri,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
        {
            throw new InvalidDataException($"CDN 响应超过允许大小：{uri}。");
        }

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream destination = new();
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > maximumBytes)
            {
                throw new InvalidDataException($"CDN 响应超过允许大小：{uri}。");
            }

            destination.Write(buffer, 0, read);
        }

        return destination.ToArray();
    }

    public async Task<string> DownloadVerifiedAsync(
        Uri uri,
        long expectedSize,
        string expectedHash,
        string cacheCategory,
        int maxAttempts,
        IProgress<CdnUpdateProgress>? progress,
        string displayName,
        CancellationToken cancellationToken)
    {
        expectedHash = CdnHash.Normalize(expectedHash);
        string category = CdnPath.NormalizeRelative(cacheCategory);
        string categoryRoot = CdnPath.ResolveUnderRoot(_cacheRoot, category);
        Directory.CreateDirectory(categoryRoot);
        string extension = string.Equals(category, "packages", StringComparison.Ordinal)
            ? ".zip"
            : string.Empty;
        string finalPath = Path.Combine(categoryRoot, expectedHash + extension);
        string partialPath = finalPath + ".part";

        if (await IsVerifiedAsync(finalPath, expectedSize, expectedHash, cancellationToken))
        {
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Downloading,
                $"已使用缓存：{displayName}",
                expectedSize,
                expectedSize));
            return finalPath;
        }

        TryDeleteFile(finalPath);
        EnsureCacheSpace(expectedSize, partialPath);
        Exception? lastError = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DownloadAttemptAsync(
                    uri,
                    partialPath,
                    expectedSize,
                    progress,
                    displayName,
                    cancellationToken);
                if (!await IsVerifiedAsync(partialPath, expectedSize, expectedHash, cancellationToken))
                {
                    throw new InvalidDataException($"下载文件校验失败：{displayName}。");
                }

                File.Move(partialPath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                lastError = exception;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                }
            }
            catch (InvalidDataException exception)
            {
                lastError = exception;
                TryDeleteFile(partialPath);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                }
            }
        }

        if (lastError is InvalidDataException)
        {
            throw new CdnDownloadIntegrityException($"下载文件完整性校验失败：{displayName}。", lastError);
        }

        throw new CdnDownloadException($"下载失败：{displayName}。", lastError!);
    }

    public async Task<string> DownloadVerifiedChunksAsync(
        Uri manifestUri,
        IReadOnlyList<CdnChunkEntry> chunks,
        long expectedSize,
        string expectedHash,
        string cacheCategory,
        int maxAttempts,
        IProgress<CdnUpdateProgress>? progress,
        string displayName,
        CancellationToken cancellationToken)
    {
        expectedHash = CdnHash.Normalize(expectedHash);
        string category = CdnPath.NormalizeRelative(cacheCategory);
        string categoryRoot = CdnPath.ResolveUnderRoot(_cacheRoot, category);
        Directory.CreateDirectory(categoryRoot);
        string extension = string.Equals(category, "packages", StringComparison.Ordinal)
            ? ".zip"
            : string.Empty;
        string finalPath = Path.Combine(categoryRoot, expectedHash + extension);
        string partialPath = finalPath + ".assemble.part";
        if (await IsVerifiedAsync(finalPath, expectedSize, expectedHash, cancellationToken))
        {
            return finalPath;
        }

        TryDeleteFile(finalPath);
        TryDeleteFile(partialPath);
        long missingChunkBytes = 0;
        foreach (CdnChunkEntry chunk in chunks)
        {
            string chunkPath = CdnPath.ResolveUnderRoot(
                Path.Combine(_cacheRoot, "chunks"),
                chunk.Sha256);
            if (!await IsVerifiedAsync(chunkPath, chunk.Size, chunk.Sha256, cancellationToken))
            {
                checked
                {
                    missingChunkBytes += chunk.Size;
                }
            }
        }

        EnsureCacheSpace(checked(expectedSize + missingChunkBytes), partialPath);
        List<string> chunkFiles = new(chunks.Count);
        Uri contentBaseUri = new(manifestUri, ".");
        for (int index = 0; index < chunks.Count; index++)
        {
            CdnChunkEntry chunk = chunks[index];
            string safePath = CdnPath.NormalizeRelative(chunk.Path);
            string chunkFile = await DownloadVerifiedAsync(
                new Uri(contentBaseUri, safePath),
                chunk.Size,
                chunk.Sha256,
                "chunks",
                maxAttempts,
                progress,
                $"{displayName} [{index + 1}/{chunks.Count}]",
                cancellationToken);
            chunkFiles.Add(chunkFile);
        }

        try
        {
            await using (FileStream destination = new(
                             partialPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                foreach (string chunkFile in chunkFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using FileStream source = new(
                        chunkFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        BufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await source.CopyToAsync(destination, BufferSize, cancellationToken);
                }
            }

            if (!await IsVerifiedAsync(partialPath, expectedSize, expectedHash, cancellationToken))
            {
                throw new CdnDownloadIntegrityException(
                    $"CDN 分片重组校验失败：{displayName}。",
                    new InvalidDataException("重组后的大小或 SHA-256 与清单不一致。"));
            }

            File.Move(partialPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            TryDeleteFile(partialPath);
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task DownloadAttemptAsync(
        Uri uri,
        string partialPath,
        long expectedSize,
        IProgress<CdnUpdateProgress>? progress,
        string displayName,
        CancellationToken cancellationToken)
    {
        long existingLength = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        if (existingLength < 0 || existingLength > expectedSize)
        {
            TryDeleteFile(partialPath);
            existingLength = 0;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            TryDeleteFile(partialPath);
            throw new InvalidDataException("CDN 拒绝了断点范围，请重新下载。");
        }

        response.EnsureSuccessStatusCode();
        bool append = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (append && response.Content.Headers.ContentRange?.From != existingLength)
        {
            TryDeleteFile(partialPath);
            throw new InvalidDataException("CDN 返回了不匹配的断点范围。");
        }

        if (!append)
        {
            existingLength = 0;
        }

        await using FileStream destination = new(
            partialPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[BufferSize];
        long completed = existingLength;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            completed += read;
            if (completed > expectedSize)
            {
                throw new InvalidDataException($"CDN 返回的数据大于清单声明：{displayName}。");
            }

            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Downloading,
                $"正在下载：{displayName}",
                completed,
                expectedSize));
        }

        await destination.FlushAsync(cancellationToken);
        if (completed != expectedSize)
        {
            throw new HttpRequestException($"CDN 返回的数据大小不完整：{displayName}。");
        }
    }

    private static async Task<bool> IsVerifiedAsync(
        string path,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length != expectedSize)
        {
            return false;
        }

        string hash = await CdnHash.FileSha256Async(path, cancellationToken);
        return string.Equals(hash, expectedHash, StringComparison.Ordinal);
    }

    private void EnsureCacheSpace(long expectedSize, string partialPath)
    {
        long existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        long remaining = Math.Max(0, expectedSize - existing);
        long required = checked(remaining + 64L * 1024 * 1024);
        DriveInfo drive = new(Path.GetPathRoot(_cacheRoot)!);
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException(
                $"下载缓存空间不足，需要至少 {required / 1024 / 1024} MiB 可用空间。");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return TimeSpan.FromSeconds(Math.Min(40, attempt * 20));
    }
}

internal sealed class CdnInstallService : IDisposable
{
    public const string InstalledManifestFileName = ".launcher-installed-manifest.json";
    public const string InstalledSignatureFileName = ".launcher-installed-manifest.sig";

    private readonly CdnDownloadClient _downloadClient;
    private readonly byte[] _trustedPublicKeyPem;
    private readonly bool _allowInsecureHttp;

    internal Func<string, bool>? BackupCleanupOverride { get; set; }

    public CdnInstallService(
        string cacheRoot,
        string publicKeyPath,
        bool allowInsecureHttp = false)
        : this(cacheRoot, File.ReadAllBytes(publicKeyPath), allowInsecureHttp)
    {
    }

    public CdnInstallService(
        string cacheRoot,
        byte[] trustedPublicKeyPem,
        bool allowInsecureHttp = false)
    {
        _downloadClient = new CdnDownloadClient(cacheRoot, TimeSpan.FromMinutes(30));
        _trustedPublicKeyPem = trustedPublicKeyPem.Length > 0
            ? trustedPublicKeyPem.ToArray()
            : throw new ArgumentException("CDN 签名公钥为空。", nameof(trustedPublicKeyPem));
        _allowInsecureHttp = allowInsecureHttp;
    }

    public static string DefaultCacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FrameSyncMobaDemo",
        "GameLauncher",
        "Cache");

    public async Task<CdnClientCheckResult> CheckRequiredActionAsync(
        string gameDirectory,
        CdnLauncherConfig config,
        CancellationToken cancellationToken)
    {
        string gameRoot = Path.GetFullPath(gameDirectory);
        RecoverInterruptedInstall(gameRoot);
        GameInstallStatus layout = GameInstallLocator.Check(
            Path.Combine(gameRoot, LauncherPaths.GameExecutableName));
        if (!layout.IsReady)
        {
            return new CdnClientCheckResult(CdnRequiredAction.Download, null);
        }

        SignedClientManifest remote = await FetchManifestAsync(config, cancellationToken);
        SignedClientManifest? installed = TryLoadInstalledManifest(gameRoot);
        bool isCurrent = installed != null &&
                         installed.ManifestBytes.AsSpan().SequenceEqual(remote.ManifestBytes) &&
                         await ValidateTrustedInstallAsync(gameRoot, installed, cancellationToken);
        return new CdnClientCheckResult(
            isCurrent ? CdnRequiredAction.Start : CdnRequiredAction.Update,
            remote.Manifest.ClientVersion);
    }

    public async Task<CdnInstallResult> EnsureCurrentAsync(
        string gameDirectory,
        CdnLauncherConfig config,
        IProgress<CdnUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        string gameRoot = Path.GetFullPath(gameDirectory);
        string executablePath = Path.Combine(gameRoot, LauncherPaths.GameExecutableName);
        if (GameProcessManager.IsExecutableRunning(executablePath))
        {
            throw new GameClientRunningException();
        }

        RecoverInterruptedInstall(gameRoot);
        progress?.Report(new CdnUpdateProgress(
            CdnUpdatePhase.Checking,
            "正在检查客户端版本……",
            0,
            0));
        SignedClientManifest remote = await FetchManifestAsync(config, cancellationToken);
        SignedClientManifest? installed = TryLoadInstalledManifest(gameRoot);
        if (installed != null &&
            installed.ManifestBytes.AsSpan().SequenceEqual(remote.ManifestBytes) &&
            await ValidateTrustedInstallAsync(gameRoot, installed, cancellationToken))
        {
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Complete,
                $"客户端已是最新版本 {remote.Manifest.ClientVersion}。",
                1,
                1));
            return new CdnInstallResult(remote.Manifest.ClientVersion, false, false, 0, remote.Manifest.Files.Count);
        }

        bool canIncrement = installed != null &&
                            await ValidateTrustedInstallAsync(gameRoot, installed, cancellationToken);
        if (canIncrement)
        {
            try
            {
                return await InstallIncrementalAsync(
                    gameRoot,
                    installed!,
                    remote,
                    config,
                    progress,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or CdnDownloadException)
            {
                progress?.Report(new CdnUpdateProgress(
                    CdnUpdatePhase.Installing,
                    "增量文件校验失败，正在切换到完整修复包……",
                    0,
                    remote.Manifest.FullPackage.Size));
                return await InstallFullAsync(gameRoot, remote, config, progress, cancellationToken);
            }
        }

        return await InstallFullAsync(gameRoot, remote, config, progress, cancellationToken);
    }

    public void Dispose()
    {
        _downloadClient.Dispose();
    }

    public async Task<bool> ValidateTrustedInstallAsync(
        string gameDirectory,
        CancellationToken cancellationToken)
    {
        string gameRoot = Path.GetFullPath(gameDirectory);
        SignedClientManifest? installed = TryLoadInstalledManifest(gameRoot);
        return installed != null &&
               await ValidateTrustedInstallAsync(gameRoot, installed, cancellationToken);
    }

    public void RecoverInterruptedInstall(string gameDirectory)
    {
        string gameRoot = Path.GetFullPath(gameDirectory);
        string staging = gameRoot + ".__staging";
        string backup = gameRoot + ".__backup";
        ValidateSiblingWorkPath(gameRoot, staging, ".__staging");
        ValidateSiblingWorkPath(gameRoot, backup, ".__backup");

        if (!Directory.Exists(gameRoot) && Directory.Exists(backup))
        {
            if (!ValidateTrustedInstall(backup))
            {
                throw new InvalidDataException("中断恢复失败：备份客户端不可信，已保留现场。");
            }

            Directory.Move(backup, gameRoot);
        }
        else if (Directory.Exists(gameRoot) && Directory.Exists(backup))
        {
            bool currentIsValid = ValidateTrustedInstall(gameRoot);
            bool backupIsValid = ValidateTrustedInstall(backup);
            if (currentIsValid)
            {
                Directory.Delete(backup, recursive: true);
            }
            else if (backupIsValid)
            {
                Directory.Delete(gameRoot, recursive: true);
                Directory.Move(backup, gameRoot);
            }
            else
            {
                throw new InvalidDataException("中断恢复失败：当前客户端和备份都不可信，已保留现场。");
            }
        }

        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private async Task<SignedClientManifest> FetchManifestAsync(
        CdnLauncherConfig config,
        CancellationToken cancellationToken)
    {
        Uri manifestUri = config.GetManifestUri(_allowInsecureHttp);
        Uri signatureUri = config.GetSignatureUri(manifestUri);
        Exception? lastError = null;
        for (int attempt = 1; attempt <= config.MaxAttempts; attempt++)
        {
            try
            {
                byte[] manifestBytes = await _downloadClient.DownloadSmallAsync(
                    manifestUri,
                    maximumBytes: 32 * 1024 * 1024,
                    cancellationToken);
                byte[] signatureBytes = await _downloadClient.DownloadSmallAsync(
                    signatureUri,
                    maximumBytes: 16 * 1024,
                    cancellationToken);
                CdnSignature.VerifyOrThrow(manifestBytes, signatureBytes, _trustedPublicKeyPem);
                ClientReleaseManifest manifest = CdnJson.DeserializeManifest(manifestBytes);
                return new SignedClientManifest(manifest, manifestBytes, signatureBytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or InvalidDataException or
                    System.Security.Cryptography.CryptographicException)
            {
                lastError = exception;
                if (attempt < config.MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(40, attempt * 20)), cancellationToken);
                }
            }
        }

        throw new InvalidOperationException("无法取得可信的 CDN 客户端清单。", lastError);
    }

    private async Task<CdnInstallResult> InstallFullAsync(
        string gameRoot,
        SignedClientManifest remote,
        CdnLauncherConfig config,
        IProgress<CdnUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        EnsureFreeSpace(
            gameRoot,
            remote.Manifest.TotalInstalledBytes,
            remote.Manifest.FullPackage.Size);
        Uri manifestUri = config.GetManifestUri(_allowInsecureHttp);
        string packagePath = await _downloadClient.DownloadVerifiedChunksAsync(
            manifestUri,
            remote.Manifest.FullPackage.Chunks,
            remote.Manifest.FullPackage.Size,
            remote.Manifest.FullPackage.Sha256,
            "packages",
            config.MaxAttempts,
            progress,
            remote.Manifest.FullPackage.FileName,
            cancellationToken);

        string staging = PrepareStaging(gameRoot);
        try
        {
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Installing,
                "正在解压完整客户端……",
                0,
                remote.Manifest.TotalInstalledBytes));
            await ExtractFullPackageAsync(
                packagePath,
                staging,
                remote.Manifest,
                progress,
                cancellationToken);
            WriteInstalledManifest(staging, remote);
            await ValidateStagingAsync(staging, remote.Manifest, progress, cancellationToken);
            SwapIntoPlace(gameRoot, staging);
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Complete,
                $"客户端 {remote.Manifest.ClientVersion} 安装完成。",
                1,
                1));
            return new CdnInstallResult(remote.Manifest.ClientVersion, true, true, 1, 0);
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            throw;
        }
    }

    private async Task<CdnInstallResult> InstallIncrementalAsync(
        string gameRoot,
        SignedClientManifest installed,
        SignedClientManifest remote,
        CdnLauncherConfig config,
        IProgress<CdnUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CdnFileEntry> oldFiles = installed.Manifest.Files.ToDictionary(
            entry => entry.Path,
            StringComparer.OrdinalIgnoreCase);
        List<CdnFileEntry> changed = new();
        List<CdnFileEntry> reusable = new();
        foreach (CdnFileEntry target in remote.Manifest.Files)
        {
            string currentPath = CdnPath.ResolveUnderRoot(gameRoot, target.Path);
            if (oldFiles.TryGetValue(target.Path, out CdnFileEntry? old) &&
                string.Equals(old.Sha256, target.Sha256, StringComparison.Ordinal) &&
                File.Exists(currentPath) &&
                new FileInfo(currentPath).Length == target.Size)
            {
                reusable.Add(target);
            }
            else
            {
                changed.Add(target);
            }
        }

        EnsureFreeSpace(
            gameRoot,
            remote.Manifest.TotalInstalledBytes,
            changed.Sum(entry => entry.Size));

        string staging = PrepareStaging(gameRoot);
        try
        {
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Installing,
                "正在组装增量客户端……",
                0,
                remote.Manifest.TotalInstalledBytes));
            long assembledBytes = 0;
            foreach (CdnFileEntry entry in reusable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string sourcePath = CdnPath.ResolveUnderRoot(gameRoot, entry.Path);
                string destinationPath = CdnPath.ResolveUnderRoot(staging, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: false);
                assembledBytes += entry.Size;
                progress?.Report(new CdnUpdateProgress(
                    CdnUpdatePhase.Installing,
                    $"正在复用本地文件：{entry.Path}",
                    assembledBytes,
                    remote.Manifest.TotalInstalledBytes));
            }

            Uri manifestUri = config.GetManifestUri(_allowInsecureHttp);
            foreach (CdnFileEntry entry in changed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string objectPath = await _downloadClient.DownloadVerifiedChunksAsync(
                    manifestUri,
                    entry.Chunks,
                    entry.Size,
                    entry.Sha256,
                    "objects",
                    config.MaxAttempts,
                    progress,
                    entry.Path,
                    cancellationToken);
                string destinationPath = CdnPath.ResolveUnderRoot(staging, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(objectPath, destinationPath, overwrite: false);
                assembledBytes += entry.Size;
            }

            WriteInstalledManifest(staging, remote);
            await ValidateStagingAsync(staging, remote.Manifest, progress, cancellationToken);
            SwapIntoPlace(gameRoot, staging);
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Complete,
                $"客户端已更新到 {remote.Manifest.ClientVersion}。",
                1,
                1));
            return new CdnInstallResult(
                remote.Manifest.ClientVersion,
                true,
                false,
                changed.Count,
                reusable.Count);
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            throw;
        }
    }

    private static async Task ExtractFullPackageAsync(
        string packagePath,
        string staging,
        ClientReleaseManifest manifest,
        IProgress<CdnUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CdnFileEntry> targets = manifest.Files.ToDictionary(
            entry => entry.Path,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> extracted = new(StringComparer.OrdinalIgnoreCase);
        long completed = 0;
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(zipEntry.Name))
            {
                continue;
            }

            string relativePath = CdnPath.NormalizeRelative(zipEntry.FullName);
            if (!targets.TryGetValue(relativePath, out CdnFileEntry? target) || !extracted.Add(relativePath))
            {
                throw new InvalidDataException($"完整 ZIP 包含未声明或重复文件：{relativePath}。");
            }

            if (zipEntry.Length != target.Size)
            {
                throw new InvalidDataException($"完整 ZIP 文件大小与清单不一致：{relativePath}。");
            }

            string destinationPath = CdnPath.ResolveUnderRoot(staging, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using Stream source = zipEntry.Open();
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, 1024 * 1024, cancellationToken);
            completed += target.Size;
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Installing,
                $"正在安装：{relativePath}",
                completed,
                manifest.TotalInstalledBytes));
        }

        if (extracted.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("完整 ZIP 缺少清单声明的文件。");
        }
    }

    private static async Task ValidateStagingAsync(
        string staging,
        ClientReleaseManifest manifest,
        IProgress<CdnUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        long verified = 0;
        HashSet<string> expected = manifest.Files
            .Select(entry => entry.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CdnFileEntry entry in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = CdnPath.ResolveUnderRoot(staging, entry.Path);
            FileInfo info = new(path);
            if (!info.Exists || info.Length != entry.Size)
            {
                throw new InvalidDataException($"安装文件缺失或大小错误：{entry.Path}。");
            }

            string hash = await CdnHash.FileSha256Async(path, cancellationToken);
            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"安装文件 SHA-256 错误：{entry.Path}。");
            }

            verified += entry.Size;
            progress?.Report(new CdnUpdateProgress(
                CdnUpdatePhase.Verifying,
                $"正在校验：{entry.Path}",
                verified,
                manifest.TotalInstalledBytes));
        }

        foreach (string path in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(staging, path).Replace(Path.DirectorySeparatorChar, '/');
            if (string.Equals(relative, InstalledManifestFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, InstalledSignatureFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!expected.Contains(relative))
            {
                throw new InvalidDataException($"安装目录出现清单外文件：{relative}。");
            }
        }

        GameInstallLocator.ValidateOrThrow(Path.Combine(staging, LauncherPaths.GameExecutableName));
    }

    private SignedClientManifest? TryLoadInstalledManifest(string gameRoot)
    {
        try
        {
            string manifestPath = Path.Combine(gameRoot, InstalledManifestFileName);
            string signaturePath = Path.Combine(gameRoot, InstalledSignatureFileName);
            if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
            {
                return null;
            }

            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            byte[] signatureBytes = File.ReadAllBytes(signaturePath);
            CdnSignature.VerifyOrThrow(manifestBytes, signatureBytes, _trustedPublicKeyPem);
            ClientReleaseManifest manifest = CdnJson.DeserializeManifest(manifestBytes);
            return new SignedClientManifest(manifest, manifestBytes, signatureBytes);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
                System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private async Task<bool> ValidateTrustedInstallAsync(
        string gameRoot,
        SignedClientManifest installed,
        CancellationToken cancellationToken)
    {
        try
        {
            await ValidateStagingAsync(
                gameRoot,
                installed.Manifest,
                progress: null,
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool ValidateTrustedInstall(string gameRoot)
    {
        SignedClientManifest? installed = TryLoadInstalledManifest(gameRoot);
        if (installed == null)
        {
            return false;
        }

        try
        {
            HashSet<string> expected = installed.Manifest.Files
                .Select(entry => entry.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (CdnFileEntry entry in installed.Manifest.Files)
            {
                string path = CdnPath.ResolveUnderRoot(gameRoot, entry.Path);
                FileInfo info = new(path);
                if (!info.Exists || info.Length != entry.Size)
                {
                    return false;
                }

                using FileStream stream = File.OpenRead(path);
                string hash = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(stream))
                    .ToLowerInvariant();
                if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            foreach (string path in Directory.EnumerateFiles(gameRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(gameRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (!expected.Contains(relative) &&
                    !string.Equals(relative, InstalledManifestFileName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(relative, InstalledSignatureFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            GameInstallLocator.ValidateOrThrow(Path.Combine(gameRoot, installed.Manifest.EntryPoint));
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string PrepareStaging(string gameRoot)
    {
        string staging = gameRoot + ".__staging";
        ValidateSiblingWorkPath(gameRoot, staging, ".__staging");
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        return staging;
    }

    private void SwapIntoPlace(string gameRoot, string staging)
    {
        string backup = gameRoot + ".__backup";
        ValidateSiblingWorkPath(gameRoot, staging, ".__staging");
        ValidateSiblingWorkPath(gameRoot, backup, ".__backup");
        if (Directory.Exists(backup))
        {
            throw new InvalidOperationException("安装备份目录已存在；必须先完成安全恢复。");
        }

        bool movedOriginal = false;
        bool committed = false;
        try
        {
            string executablePath = Path.Combine(gameRoot, LauncherPaths.GameExecutableName);
            if (GameProcessManager.IsExecutableRunning(executablePath))
            {
                throw new GameClientRunningException();
            }

            if (Directory.Exists(gameRoot))
            {
                Directory.Move(gameRoot, backup);
                movedOriginal = true;
            }

            Directory.Move(staging, gameRoot);
            GameInstallLocator.ValidateOrThrow(Path.Combine(gameRoot, LauncherPaths.GameExecutableName));
            committed = true;
        }
        catch
        {
            if (!committed && Directory.Exists(gameRoot))
            {
                Directory.Delete(gameRoot, recursive: true);
            }

            if (!committed && movedOriginal && Directory.Exists(backup))
            {
                Directory.Move(backup, gameRoot);
            }

            throw;
        }

        if (Directory.Exists(backup))
        {
            _ = TryCleanupBackup(backup);
        }
    }

    private bool TryCleanupBackup(string backup)
    {
        if (BackupCleanupOverride != null)
        {
            return BackupCleanupOverride(backup);
        }

        try
        {
            Directory.Delete(backup, recursive: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteInstalledManifest(string staging, SignedClientManifest signed)
    {
        File.WriteAllBytes(Path.Combine(staging, InstalledManifestFileName), signed.ManifestBytes);
        File.WriteAllBytes(Path.Combine(staging, InstalledSignatureFileName), signed.SignatureBytes);
    }

    private void EnsureFreeSpace(string gameRoot, long installedBytes, long downloadBytes)
    {
        string? parent = Path.GetDirectoryName(gameRoot);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidOperationException("无法解析 Game 所在磁盘。");
        }

        string probe = Directory.Exists(parent) ? parent : Path.GetPathRoot(parent)!;
        DriveInfo drive = new(Path.GetPathRoot(Path.GetFullPath(probe))!);
        string installDrive = Path.GetPathRoot(Path.GetFullPath(probe))!;
        string cacheDrive = Path.GetPathRoot(_downloadClient.CacheRoot)!;
        long sameDriveDownloadBytes = string.Equals(
            installDrive,
            cacheDrive,
            StringComparison.OrdinalIgnoreCase)
            ? downloadBytes
            : 0;
        long required = checked(
            installedBytes + (sameDriveDownloadBytes * 2) +
            Math.Max(512L * 1024 * 1024, installedBytes / 10));
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException(
                $"安装空间不足，需要至少 {required / 1024 / 1024} MiB 可用空间。");
        }
    }

    private static void ValidateSiblingWorkPath(string gameRoot, string candidate, string suffix)
    {
        string expected = Path.GetFullPath(gameRoot) + suffix;
        if (!string.Equals(Path.GetFullPath(candidate), expected, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, Path.GetPathRoot(candidate), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("安装工作目录不在允许的固定位置。");
        }
    }
}
