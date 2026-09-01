using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FrameSyncMoba.GameLauncher;

internal sealed record CdnPackageBuildResult(
    string OutputRoot,
    string UploadRoot,
    ClientReleaseManifest Manifest,
    string ManifestPath,
    string SignaturePath,
    string FullPackageFileName,
    int UniqueContentCount);

internal static class CdnPackageBuilder
{
    private const string OutputMarkerFileName = ".framesync-cdn-package-root";
    public const int DefaultChunkSizeBytes = 95_000_000;
    private static readonly DateTimeOffset StableZipTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task<CdnPackageBuildResult> BuildAsync(
        string sourceGameDirectory,
        string outputRoot,
        string clientVersion,
        string privateKeyPath,
        CancellationToken cancellationToken = default,
        int chunkSizeBytes = DefaultChunkSizeBytes)
    {
        string sourceRoot = Path.GetFullPath(sourceGameDirectory);
        string output = Path.GetFullPath(outputRoot);
        ValidateBuildLocations(sourceRoot, output);
        GameInstallLocator.ValidateOrThrow(Path.Combine(sourceRoot, LauncherPaths.GameExecutableName));
        if (!Version.TryParse(clientVersion, out _))
        {
            throw new ArgumentException("客户端版本必须是数字版本号，例如 1.0.0。", nameof(clientVersion));
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException("没有找到 CDN 签名私钥。", privateKeyPath);
        }

        if (chunkSizeBytes <= 0 || chunkSizeBytes > CdnChunkEntry.MaximumSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSizeBytes),
                "CDN 分片大小必须在 1 字节到 95,000,000 字节之间。");
        }

        RecreateDirectory(output);
        string uploadRoot = Path.Combine(output, "Upload");
        Directory.CreateDirectory(Path.Combine(uploadRoot, "content"));

        string[] allSourceFiles = EnumerateFilesWithoutReparsePoints(sourceRoot);
        string[] sourceFiles = allSourceFiles
            .Where(path => !IsExcludedDistributionFile(sourceRoot, path))
            .OrderBy(path => ToManifestPath(sourceRoot, path), StringComparer.Ordinal)
            .ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidDataException("Game 目录中没有可打包文件。");
        }

        List<CdnFileEntry> entries = new(sourceFiles.Length);
        HashSet<string> copiedObjects = new(StringComparer.Ordinal);
        Dictionary<string, List<CdnChunkEntry>> objectChunks = new(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (string sourcePath in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = ToManifestPath(sourceRoot, sourcePath);
            FileInfo info = new(sourcePath);
            string hash = await CdnHash.FileSha256Async(sourcePath, cancellationToken);
            if (copiedObjects.Add(hash))
            {
                objectChunks.Add(
                    hash,
                    await WriteChunksAsync(
                        sourcePath,
                        uploadRoot,
                        chunkSizeBytes,
                        cancellationToken));
            }

            entries.Add(new CdnFileEntry
            {
                Path = relativePath,
                Size = info.Length,
                Sha256 = hash,
                Chunks = objectChunks[hash]
            });
            checked
            {
                totalBytes += info.Length;
            }
        }

        string packageFileName = $"AAALOL-{clientVersion}-full.zip";
        string fullPackagePath = Path.Combine(output, packageFileName + ".build.tmp");
        await CreateStableZipAsync(sourceRoot, sourceFiles, fullPackagePath, cancellationToken);
        FileInfo packageInfo = new(fullPackagePath);
        string packageHash = await CdnHash.FileSha256Async(fullPackagePath, cancellationToken);
        List<CdnChunkEntry> packageChunks = await WriteChunksAsync(
            fullPackagePath,
            uploadRoot,
            chunkSizeBytes,
            cancellationToken);

        ClientReleaseManifest manifest = new()
        {
            SchemaVersion = 3,
            ClientVersion = clientVersion,
            MinimumLauncherVersion = LauncherVersion.Current,
            EntryPoint = LauncherPaths.GameExecutableName,
            TotalInstalledBytes = totalBytes,
            FullPackage = new CdnPackageEntry
            {
                FileName = packageFileName,
                Size = packageInfo.Length,
                Sha256 = packageHash,
                Chunks = packageChunks
            },
            Files = entries
        };
        byte[] manifestBytes = CdnJson.SerializeManifest(manifest);
        byte[] signatureBytes = CdnSignature.Sign(manifestBytes, privateKeyPath);
        string manifestPath = Path.Combine(uploadRoot, "client-manifest.json");
        string signaturePath = Path.Combine(uploadRoot, "client-manifest.sig");
        await File.WriteAllBytesAsync(manifestPath, manifestBytes, cancellationToken);
        await File.WriteAllBytesAsync(signaturePath, signatureBytes, cancellationToken);

        await AuditAsync(uploadRoot, publicKeyPath: null, privateKeyPath, cancellationToken);
        string reportPath = Path.Combine(output, "package-report.json");
        byte[] reportBytes = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                clientVersion,
                sourceRoot,
                uploadRoot,
                files = entries.Count,
                uniqueContent = copiedObjects.Count,
                totalInstalledBytes = totalBytes,
                fullPackageBytes = packageInfo.Length,
                fullPackageSha256 = packageHash,
                chunkSizeBytes,
                logicalChunkReferences = packageChunks.Count + objectChunks.Values.Sum(chunks => chunks.Count),
                physicalContentFiles = Directory
                    .EnumerateFiles(Path.Combine(uploadRoot, "content"), "*", SearchOption.TopDirectoryOnly)
                    .Count(),
                maximumUploadFileBytes = Directory
                    .EnumerateFiles(uploadRoot, "*", SearchOption.AllDirectories)
                    .Max(path => new FileInfo(path).Length),
                excludedFiles = allSourceFiles.Length - sourceFiles.Length,
                excludedBytes = allSourceFiles
                    .Except(sourceFiles, StringComparer.OrdinalIgnoreCase)
                    .Sum(path => new FileInfo(path).Length)
            },
            CdnJson.Options);
        await File.WriteAllBytesAsync(reportPath, reportBytes, cancellationToken);
        File.Delete(fullPackagePath);

        return new CdnPackageBuildResult(
            output,
            uploadRoot,
            manifest,
            manifestPath,
            signaturePath,
            packageFileName,
            copiedObjects.Count);
    }

    private static async Task<List<CdnChunkEntry>> WriteChunksAsync(
        string sourcePath,
        string uploadRoot,
        int chunkSizeBytes,
        CancellationToken cancellationToken)
    {
        FileInfo sourceInfo = new(sourcePath);
        int chunkCount = sourceInfo.Length == 0
            ? 1
            : checked((int)((sourceInfo.Length + chunkSizeBytes - 1) / chunkSizeBytes));
        List<CdnChunkEntry> chunks = new(chunkCount);
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] buffer = new byte[Math.Min(chunkSizeBytes, 1024 * 1024)];
        for (int index = 0; index < chunkCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destinationPath = Path.Combine(
                uploadRoot,
                ".content-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            long remaining = Math.Min(chunkSizeBytes, sourceInfo.Length - source.Position);
            await using (FileStream destination = new(
                             destinationPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (remaining > 0)
                {
                    int read = await source.ReadAsync(
                        buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                        cancellationToken);
                    if (read == 0)
                    {
                        throw new EndOfStreamException($"CDN 分片读取提前结束：{sourcePath}。");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    remaining -= read;
                }
            }

            FileInfo chunkInfo = new(destinationPath);
            long actualChunkSize = chunkInfo.Length;
            string chunkHash = await CdnHash.FileSha256Async(destinationPath, cancellationToken);
            string chunkPath = "content/" + chunkHash;
            string contentPath = CdnPath.ResolveUnderRoot(uploadRoot, chunkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(contentPath)!);
            if (File.Exists(contentPath))
            {
                File.Delete(destinationPath);
            }
            else
            {
                File.Move(destinationPath, contentPath);
            }

            chunks.Add(new CdnChunkEntry
            {
                Path = chunkPath,
                Size = actualChunkSize,
                Sha256 = chunkHash
            });
        }

        if (source.Position != sourceInfo.Length)
        {
            throw new InvalidDataException($"CDN 分片没有覆盖完整文件：{sourcePath}。");
        }

        return chunks;
    }

    private static string[] EnumerateFilesWithoutReparsePoints(string sourceRoot)
    {
        List<string> files = new();
        Stack<string> directories = new();
        directories.Push(sourceRoot);
        while (directories.Count > 0)
        {
            string directory = directories.Pop();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Game 目录包含不允许的联接或符号链接：{path}。");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(path);
                }
                else
                {
                    files.Add(path);
                }
            }
        }

        return files.ToArray();
    }

    public static async Task<ClientReleaseManifest> AuditAsync(
        string uploadRoot,
        string? publicKeyPath,
        string? privateKeyPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(uploadRoot);
        string manifestPath = Path.Combine(root, "client-manifest.json");
        string signaturePath = Path.Combine(root, "client-manifest.sig");
        byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
        byte[] signatureBytes = await File.ReadAllBytesAsync(signaturePath, cancellationToken);
        ClientReleaseManifest manifest = CdnJson.DeserializeManifest(manifestBytes);
        if (publicKeyPath != null)
        {
            CdnSignature.VerifyOrThrow(manifestBytes, signatureBytes, publicKeyPath);
        }
        else if (privateKeyPath != null)
        {
            using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(privateKeyPath, Encoding.ASCII));
            if (!rsa.VerifyData(
                    manifestBytes,
                    signatureBytes,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    System.Security.Cryptography.RSASignaturePadding.Pkcs1))
            {
                throw new InvalidDataException("生成后的客户端清单签名复核失败。");
            }
        }

        HashSet<string> expectedPhysicalPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "client-manifest.json",
            "client-manifest.sig"
        };
        AddChunkPaths(expectedPhysicalPaths, manifest.FullPackage.Chunks);
        await VerifyChunksAsync(
            root,
            manifest.FullPackage.Chunks,
            manifest.FullPackage.Size,
            manifest.FullPackage.Sha256,
            cancellationToken);
        HashSet<string> verifiedObjects = new(StringComparer.Ordinal);
        foreach (CdnFileEntry entry in manifest.Files)
        {
            AddChunkPaths(expectedPhysicalPaths, entry.Chunks);
            if (verifiedObjects.Add(entry.Sha256))
            {
                await VerifyChunksAsync(
                    root,
                    entry.Chunks,
                    entry.Size,
                    entry.Sha256,
                    cancellationToken);
            }
        }

        foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (!string.Equals(Path.GetFileName(directory), "content", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"CDN 上传目录包含未声明文件夹：{directory}。");
            }
        }

        foreach (string physicalFile in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relativePath = ToManifestPath(root, physicalFile);
            if (!expectedPhysicalPaths.Remove(relativePath))
            {
                throw new InvalidDataException($"CDN 上传目录包含未声明文件：{relativePath}。");
            }

            if (new FileInfo(physicalFile).Length > CdnChunkEntry.MaximumSize)
            {
                throw new InvalidDataException($"CDN 上传文件超过 95,000,000 字节：{physicalFile}。");
            }
        }

        if (expectedPhysicalPaths.Count != 0)
        {
            throw new InvalidDataException(
                "CDN 上传目录缺少清单引用文件：" +
                string.Join(", ", expectedPhysicalPaths.OrderBy(path => path, StringComparer.Ordinal)));
        }

        string auditPackagePath = Path.Combine(
            Path.GetTempPath(),
            "FrameSyncMoba-CdnAudit-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await AssembleChunksAsync(
                root,
                manifest.FullPackage.Chunks,
                auditPackagePath,
                cancellationToken);
            await VerifyZipContentsAsync(auditPackagePath, manifest, cancellationToken);
        }
        finally
        {
            File.Delete(auditPackagePath);
        }

        return manifest;
    }

    private static void AddChunkPaths(
        HashSet<string> expectedPhysicalPaths,
        IEnumerable<CdnChunkEntry> chunks)
    {
        foreach (CdnChunkEntry chunk in chunks)
        {
            expectedPhysicalPaths.Add(chunk.Path);
        }
    }

    private static async Task VerifyZipContentsAsync(
        string packagePath,
        ClientReleaseManifest manifest,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CdnFileEntry> expected = manifest.Files.ToDictionary(
            entry => entry.Path,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(zipEntry.Name))
            {
                continue;
            }

            string relativePath = CdnPath.NormalizeRelative(zipEntry.FullName);
            if (!expected.TryGetValue(relativePath, out CdnFileEntry? entry) || !seen.Add(relativePath))
            {
                throw new InvalidDataException($"完整 ZIP 包含未声明或重复文件：{relativePath}。");
            }

            if (zipEntry.Length != entry.Size)
            {
                throw new InvalidDataException($"完整 ZIP 文件大小不匹配：{relativePath}。");
            }

            await using Stream content = zipEntry.Open();
            string hash = await CdnHash.StreamSha256Async(content, cancellationToken);
            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"完整 ZIP 文件 SHA-256 不匹配：{relativePath}。");
            }
        }

        if (seen.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("完整 ZIP 缺少清单声明的文件。");
        }
    }

    private static async Task CreateStableZipAsync(
        string sourceRoot,
        IEnumerable<string> sourceFiles,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using ZipArchive archive = new(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (string sourcePath in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry zipEntry = archive.CreateEntry(
                ToManifestPath(sourceRoot, sourcePath),
                CompressionLevel.Fastest);
            zipEntry.LastWriteTime = StableZipTimestamp;
            await using Stream zipStream = zipEntry.Open();
            await using FileStream source = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(zipStream, 1024 * 1024, cancellationToken);
        }
    }

    private static async Task VerifyFileAsync(
        string path,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length != expectedSize)
        {
            throw new InvalidDataException($"CDN 输出文件大小不匹配：{path}。");
        }

        string actualHash = await CdnHash.FileSha256Async(path, cancellationToken);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"CDN 输出文件 SHA-256 不匹配：{path}。");
        }
    }

    private static async Task VerifyChunksAsync(
        string root,
        IReadOnlyList<CdnChunkEntry> chunks,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        using System.Security.Cryptography.IncrementalHash aggregateHash =
            System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        long total = 0;
        foreach (CdnChunkEntry chunk in chunks)
        {
            string chunkPath = CdnPath.ResolveUnderRoot(root, chunk.Path);
            await VerifyFileAsync(chunkPath, chunk.Size, chunk.Sha256, cancellationToken);
            await using FileStream source = new(
                chunkPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                aggregateHash.AppendData(buffer, 0, read);
                total += read;
            }
        }

        string actualHash = Convert.ToHexString(aggregateHash.GetHashAndReset()).ToLowerInvariant();
        if (total != expectedSize || !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("CDN 分片重组后的大小或 SHA-256 不匹配。");
        }
    }

    private static async Task AssembleChunksAsync(
        string root,
        IReadOnlyList<CdnChunkEntry> chunks,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        foreach (CdnChunkEntry chunk in chunks)
        {
            string chunkPath = CdnPath.ResolveUnderRoot(root, chunk.Path);
            await using FileStream source = new(
                chunkPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, 1024 * 1024, cancellationToken);
        }
    }

    private static string ToManifestPath(string sourceRoot, string sourcePath)
    {
        return CdnPath.NormalizeRelative(
            Path.GetRelativePath(sourceRoot, sourcePath).Replace(Path.DirectorySeparatorChar, '/'));
    }

    private static bool IsExcludedDistributionFile(string sourceRoot, string path)
    {
        string relativePath = Path.GetRelativePath(sourceRoot, path);
        string fileName = Path.GetFileName(relativePath);
        if (string.Equals(fileName, CdnInstallService.InstalledManifestFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, CdnInstallService.InstalledSignatureFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(fileName), ".pdb", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.EndsWith(
                "_BurstDebugInformation_DoNotShip",
                StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateBuildLocations(string sourceRoot, string outputRoot)
    {
        string sourcePrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string outputPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (outputPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) ||
            sourcePrefix.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(outputRoot, Path.GetPathRoot(outputRoot), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CDN 输出目录不能包含 Game 源目录、被其包含或指向磁盘根目录。");
        }
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            string markerPath = Path.Combine(path, OutputMarkerFileName);
            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException(
                    $"拒绝清理没有 {OutputMarkerFileName} 标记的 CDN 输出目录：{path}。");
            }

            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, OutputMarkerFileName), "FrameSyncMoba CDN package output\n");
    }
}
