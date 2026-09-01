using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FrameSyncMoba.GameLauncher;

internal static class LauncherVersion
{
    public const string Current = "1.3.1";
}

internal sealed class CdnLauncherConfig
{
    public bool Enabled { get; set; }

    public string BucketId { get; set; } = string.Empty;

    public string BadgeName { get; set; } = "Test";

    public string ManifestPath { get; set; } = "client-manifest.json";

    public string ManifestSignaturePath { get; set; } = "client-manifest.sig";

    public string ManifestUrlOverride { get; set; } = string.Empty;

    public int MaxAttempts { get; set; } = 3;

    public static string DefaultPath => Path.Combine(
        AppContext.BaseDirectory,
        "launcher.cdn.json");

    public static CdnLauncherConfig LoadOrDisabled(string? path = null)
    {
        string configPath = path ?? DefaultPath;
        if (!File.Exists(configPath))
        {
            return new CdnLauncherConfig();
        }

        try
        {
            CdnLauncherConfig? config = JsonSerializer.Deserialize<CdnLauncherConfig>(
                File.ReadAllBytes(configPath),
                CdnJson.Options);
            return config ?? new CdnLauncherConfig();
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw new InvalidDataException("launcher.cdn.json 无法读取。", exception);
        }
    }

    public Uri GetManifestUri(bool allowInsecureHttp = false)
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("CDN 更新尚未启用。请先配置 launcher.cdn.json。");
        }

        Uri uri;
        if (!string.IsNullOrWhiteSpace(ManifestUrlOverride))
        {
            if (!Uri.TryCreate(ManifestUrlOverride.Trim(), UriKind.Absolute, out uri!))
            {
                throw new InvalidDataException("ManifestUrlOverride 不是有效的绝对 URL。");
            }
        }
        else
        {
            ValidateRouteToken(BucketId, nameof(BucketId));
            ValidateRouteToken(BadgeName, nameof(BadgeName));
            string manifestPath = CdnPath.NormalizeRelative(ManifestPath);
            string escapedPath = string.Join(
                '/',
                manifestPath.Split('/').Select(Uri.EscapeDataString));
            uri = new Uri(
                $"https://a.unity.cn/client_api/v1/buckets/{Uri.EscapeDataString(BucketId)}/" +
                $"release_by_badge/{Uri.EscapeDataString(BadgeName)}/content/{escapedPath}");
        }

        bool schemeAllowed = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                             allowInsecureHttp &&
                             string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                             uri.IsLoopback;
        if (!schemeAllowed)
        {
            throw new InvalidDataException("CDN 清单必须使用 HTTPS；仅本机自测允许 HTTP。");
        }

        if (MaxAttempts is < 1 or > 10)
        {
            throw new InvalidDataException("MaxAttempts 必须在 1 到 10 之间。");
        }

        return uri;
    }

    public Uri GetSignatureUri(Uri manifestUri)
    {
        if (!string.IsNullOrWhiteSpace(ManifestUrlOverride))
        {
            string overrideValue = ManifestUrlOverride.Trim();
            int slashIndex = overrideValue.LastIndexOf('/');
            string baseUrl = slashIndex >= 0 ? overrideValue[..(slashIndex + 1)] : overrideValue;
            return new Uri(new Uri(baseUrl), CdnPath.NormalizeRelative(ManifestSignaturePath));
        }

        string signaturePath = CdnPath.NormalizeRelative(ManifestSignaturePath);
        Uri baseUri = new(manifestUri, ".");
        return new Uri(baseUri, signaturePath);
    }

    private static void ValidateRouteToken(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Regex.IsMatch(value, "^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"{fieldName} 为空或包含不允许的字符。");
        }
    }
}

internal sealed class ClientReleaseManifest
{
    public int SchemaVersion { get; set; } = 3;

    public string ClientVersion { get; set; } = string.Empty;

    public string MinimumLauncherVersion { get; set; } = LauncherVersion.Current;

    public string EntryPoint { get; set; } = LauncherPaths.GameExecutableName;

    public long TotalInstalledBytes { get; set; }

    public CdnPackageEntry FullPackage { get; set; } = new();

    public List<CdnFileEntry> Files { get; set; } = new();

    public void Validate()
    {
        if (SchemaVersion != 3)
        {
            throw new InvalidDataException($"不支持的客户端清单版本：{SchemaVersion}。");
        }

        ValidateVersion(ClientVersion, nameof(ClientVersion));
        ValidateVersion(MinimumLauncherVersion, nameof(MinimumLauncherVersion));
        if (new Version(MinimumLauncherVersion) > new Version(LauncherVersion.Current))
        {
            throw new InvalidDataException(
                $"客户端版本 {ClientVersion} 需要启动器 {MinimumLauncherVersion} 或更高版本。");
        }

        if (!string.Equals(EntryPoint, LauncherPaths.GameExecutableName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"客户端入口必须是 {LauncherPaths.GameExecutableName}。");
        }

        FullPackage.Validate();
        if (Files.Count == 0 || Files.Count > 100000)
        {
            throw new InvalidDataException("客户端清单没有文件或文件数量异常。");
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        long total = 0;
        bool hasEntryPoint = false;
        bool hasDataFile = false;
        foreach (CdnFileEntry file in Files)
        {
            file.Validate();
            if (!paths.Add(file.Path))
            {
                throw new InvalidDataException($"客户端清单包含重复路径：{file.Path}。");
            }

            if (previous != null && string.CompareOrdinal(previous, file.Path) >= 0)
            {
                throw new InvalidDataException("客户端清单文件必须按路径严格升序排列。");
            }

            previous = file.Path;
            checked
            {
                total += file.Size;
            }

            hasEntryPoint |= string.Equals(file.Path, EntryPoint, StringComparison.Ordinal);
            hasDataFile |= file.Path.StartsWith("AAALOL_Data/", StringComparison.Ordinal);
        }

        if (!hasEntryPoint || !hasDataFile)
        {
            throw new InvalidDataException("客户端清单缺少 AAALOL.exe 或 AAALOL_Data 内容。");
        }

        if (TotalInstalledBytes != total)
        {
            throw new InvalidDataException("客户端清单总字节数与文件列表不一致。");
        }
    }

    private static void ValidateVersion(string value, string fieldName)
    {
        if (!Version.TryParse(value, out Version? parsed) || parsed.Major < 0 || value.Length > 64)
        {
            throw new InvalidDataException($"{fieldName} 必须是有效的数字版本号。");
        }
    }
}

internal sealed class CdnPackageEntry
{
    public string FileName { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public List<CdnChunkEntry> Chunks { get; set; } = new();

    public void Validate()
    {
        FileName = CdnPath.NormalizeRelative(FileName);
        if (!string.Equals(FileName, Path.GetFileName(FileName), StringComparison.Ordinal) ||
            !FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("完整客户端文件名必须是不含目录的 ZIP 文件名。");
        }

        if (Size <= 0)
        {
            throw new InvalidDataException("完整客户端 ZIP 大小无效。");
        }

        Sha256 = CdnHash.Normalize(Sha256);
        CdnChunkEntry.ValidateSequence(Chunks, FileName, Size);
    }
}

internal sealed class CdnFileEntry
{
    public string Path { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public List<CdnChunkEntry> Chunks { get; set; } = new();

    public void Validate()
    {
        Path = CdnPath.NormalizeRelative(Path);
        if (Size < 0)
        {
            throw new InvalidDataException($"文件大小无效：{Path}。");
        }

        Sha256 = CdnHash.Normalize(Sha256);
        CdnChunkEntry.ValidateSequence(Chunks, Path, Size);
    }
}

internal sealed class CdnChunkEntry
{
    public const long MaximumSize = 95_000_000;

    public string Path { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public void Validate()
    {
        Path = CdnPath.NormalizeRelative(Path);
        if (Size < 0 || Size > MaximumSize)
        {
            throw new InvalidDataException($"CDN 分片大小超出 95,000,000 字节边界：{Path}。");
        }

        Sha256 = CdnHash.Normalize(Sha256);
    }

    public static void ValidateSequence(
        List<CdnChunkEntry> chunks,
        string aggregatePath,
        long aggregateSize)
    {
        if (chunks.Count == 0 || chunks.Count > 100000)
        {
            throw new InvalidDataException($"CDN 聚合文件没有有效分片：{aggregatePath}。");
        }

        long total = 0;
        for (int index = 0; index < chunks.Count; index++)
        {
            CdnChunkEntry chunk = chunks[index];
            chunk.Validate();
            string expectedPath = "content/" + chunk.Sha256;
            if (!string.Equals(chunk.Path, expectedPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"CDN 分片路径或顺序无效：期望 {expectedPath}，实际 {chunk.Path}。");
            }

            checked
            {
                total += chunk.Size;
            }
        }

        if (total != aggregateSize || aggregateSize > 0 && chunks.Any(chunk => chunk.Size == 0))
        {
            throw new InvalidDataException($"CDN 分片总大小与聚合文件不一致：{aggregatePath}。");
        }
    }
}

internal sealed record SignedClientManifest(
    ClientReleaseManifest Manifest,
    byte[] ManifestBytes,
    byte[] SignatureBytes);

internal static class CdnJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static byte[] SerializeManifest(ClientReleaseManifest manifest)
    {
        manifest.Validate();
        string json = JsonSerializer.Serialize(manifest, Options) + "\n";
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
    }

    public static ClientReleaseManifest DeserializeManifest(ReadOnlySpan<byte> bytes)
    {
        ClientReleaseManifest manifest = JsonSerializer.Deserialize<ClientReleaseManifest>(bytes, Options) ??
                                         throw new InvalidDataException("客户端清单为空。");
        manifest.Validate();
        return manifest;
    }
}

internal static class CdnHash
{
    public static async Task<string> FileSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await StreamSha256Async(stream, cancellationToken);
    }

    public static async Task<string> StreamSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Normalize(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("SHA-256 必须是 64 位十六进制字符串。");
        }

        return normalized;
    }
}

internal static class CdnSignature
{
    public static byte[] Sign(ReadOnlySpan<byte> data, string privateKeyPath)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath, Encoding.ASCII));
        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static void VerifyOrThrow(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        string publicKeyPath)
    {
        if (!File.Exists(publicKeyPath))
        {
            throw new FileNotFoundException("启动器缺少 CDN 签名公钥。", publicKeyPath);
        }

        VerifyOrThrow(data, signature, File.ReadAllBytes(publicKeyPath));
    }

    public static void VerifyOrThrow(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> publicKeyPem)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(Encoding.ASCII.GetString(publicKeyPem));
        if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new CryptographicException("CDN 客户端清单签名无效。");
        }
    }
}

internal static class CdnSigningTrust
{
    private const string EmbeddedResourceName =
        "FrameSyncMoba.GameLauncher.CdnSigningPublicKey.pem";

    public static byte[] LoadEmbeddedPublicKey()
    {
        using Stream stream = typeof(CdnSigningTrust).Assembly.GetManifestResourceStream(
                                  EmbeddedResourceName) ??
                              throw new InvalidOperationException("启动器未内置 CDN 签名公钥。");
        using MemoryStream destination = new();
        stream.CopyTo(destination);
        return destination.ToArray();
    }
}

internal static class CdnSigningKeyGenerator
{
    public static void Generate(string privateKeyPath, string publicKeyPath, bool overwrite)
    {
        if (!overwrite && (File.Exists(privateKeyPath) || File.Exists(publicKeyPath)))
        {
            throw new IOException("签名密钥已存在；未指定覆盖，因此没有改写。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(privateKeyPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(publicKeyPath))!);
        using RSA rsa = RSA.Create(3072);
        File.WriteAllText(
            privateKeyPath,
            rsa.ExportPkcs8PrivateKeyPem(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            publicKeyPath,
            rsa.ExportSubjectPublicKeyInfoPem(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

internal static class CdnPath
{
    private static readonly HashSet<string> ReservedWindowsNames = new(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        },
        StringComparer.OrdinalIgnoreCase);

    public static string NormalizeRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.Contains('\\'))
        {
            throw new InvalidDataException("CDN 路径为空、过长或包含反斜杠。");
        }

        string[] segments = value.Split('/');
        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
                segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                ReservedWindowsNames.Contains(Path.GetFileNameWithoutExtension(segment)))
            {
                throw new InvalidDataException($"CDN 路径包含不安全片段：{value}。");
            }
        }

        return string.Join('/', segments);
    }

    public static string ResolveUnderRoot(string root, string relativePath)
    {
        string normalized = NormalizeRelative(relativePath);
        string fullRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(
            Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"路径逃逸安装目录：{relativePath}。");
        }

        return candidate;
    }
}
