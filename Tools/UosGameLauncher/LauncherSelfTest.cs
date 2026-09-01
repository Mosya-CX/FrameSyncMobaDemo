using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FrameSyncMoba.GameLauncher;

internal static class LauncherSelfTest
{
    public static async Task<int> RunAsync()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrameSyncMoba-GameLauncher-Test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            RunBasicLauncherTests(temporaryDirectory);
            await RunBootstrapSafetyTestAsync(temporaryDirectory);
            await RunCdnInstallTestsAsync(temporaryDirectory);
            return 0;
        }
        catch (Exception exception)
        {
            string errorPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FrameSyncMobaDemo-GameLauncher-self-test-error.txt");
            try
            {
                File.WriteAllText(errorPath, exception.ToString());
            }
            catch
            {
            }

            Console.Error.WriteLine(exception);
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
            }
        }
    }

    private static async Task RunBootstrapSafetyTestAsync(string temporaryDirectory)
    {
        string launcherRoot = Path.Combine(temporaryDirectory, "UnsafeLauncher");
        string assetRoot = Path.Combine(launcherRoot, "Assets", "Launcher");
        Directory.CreateDirectory(assetRoot);
        foreach (string name in new[] { "AppIcon.ico", "Background.png", "Banner.png", "Logo.png" })
        {
            File.WriteAllBytes(Path.Combine(assetRoot, name), new byte[] { 1 });
        }

        File.WriteAllBytes(Path.Combine(launcherRoot, "FrameSyncMobaLauncher.exe"), new byte[] { 1 });
        File.WriteAllText(Path.Combine(launcherRoot, "launcher.cdn.json"), "{}");
        File.WriteAllText(
            Path.Combine(launcherRoot, "CdnSigningPublicKey.pem"),
            "-----BEGIN PUBLIC KEY-----\nAA==\n-----END PUBLIC KEY-----\n");
        File.WriteAllText(Path.Combine(launcherRoot, "leaked-private.key"), "secret");
        await AssertThrowsAsync<InvalidDataException>(
            () => BootstrapPackageBuilder.BuildAsync(
                launcherRoot,
                Path.Combine(temporaryDirectory, "unsafe-bootstrap.zip")),
            "Bootstrap package accepted an unlisted private key file.");
    }

    private static void RunBasicLauncherTests(string temporaryDirectory)
    {
        LauncherSettings settings = LauncherSettings.CreateDefault(null);
        settings.LoginName = "self-test-account";
        settings.GameExecutablePath = Environment.ProcessPath ??
            throw new InvalidOperationException("Self-test executable path is unavailable.");

        string fakeGamePath = Path.Combine(temporaryDirectory, LauncherPaths.GameExecutableName);
        File.WriteAllBytes(fakeGamePath, new byte[] { 0x4D, 0x5A });
        Directory.CreateDirectory(Path.Combine(temporaryDirectory, "AAALOL_Data"));
        Assert(GameInstallLocator.Check(fakeGamePath).IsReady, "Complete game layout was rejected.");
        Assert(!GameInstallLocator.Check(Path.Combine(temporaryDirectory, "Missing.exe")).IsReady,
            "Missing game layout was accepted.");
        LauncherSettings defaultSettings = LauncherSettings.CreateDefault(null);
        Assert(Path.GetFileName(defaultSettings.GameExecutablePath) == LauncherPaths.GameExecutableName,
            "Default executable name is not the formal AAALOL entry point.");

        IReadOnlyList<string> arguments = GameLaunchArgumentBuilder.Build(settings);
        Assert(arguments.Contains("-onlineFlow"), "Missing online flow.");
        Assert(arguments.Contains("--TestAccountId=self-test-account"), "Missing login name.");

        using LauncherArtwork artwork = LauncherArtwork.Load();
        Assert(artwork.Background != null, "Background artwork was not loaded.");
        Assert(artwork.Banner != null, "Banner artwork was not loaded.");
        Assert(artwork.Logo != null, "Logo artwork was not loaded.");
        Assert(artwork.AppIcon != null, "App icon artwork was not loaded.");

        string settingsPath = Path.Combine(temporaryDirectory, "settings.json");
        LauncherSettingsStore.Save(settingsPath, settings);
        string serializedSettings = File.ReadAllText(settingsPath);
        Assert(!serializedSettings.Contains("GameExecutablePath", StringComparison.Ordinal),
            "The fixed game path must not be persisted as a player setting.");
        LauncherSettings restored = LauncherSettingsStore.LoadOrDefault(settingsPath, null);
        Assert(restored.LoginName == settings.LoginName, "Login name round trip failed.");

        AssertThrows<InvalidDataException>(
            () => CdnPath.NormalizeRelative("../escape"),
            "Path traversal was accepted.");
        AssertThrows<InvalidDataException>(
            () => CdnPath.NormalizeRelative("AAALOL_Data\\escape"),
            "Backslash path was accepted.");

        byte[] embeddedPublicKey = CdnSigningTrust.LoadEmbeddedPublicKey();
        byte[] publishedPublicKey = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "CdnSigningPublicKey.pem"));
        Assert(embeddedPublicKey.SequenceEqual(publishedPublicKey),
            "Embedded CDN trust root differs from the published audit PEM.");

        using GameProcessManager processManager = new();
        ManagedGameProcess child = processManager.Start(
            settings,
            allowIncompleteInstallForSelfTest: true);
        Assert(child.Process.Id > 0, "Client process has no PID.");
        Assert(child.Process.WaitForExit(5000), "Client process did not exit.");
        Assert(child.HasExited, "Client exit was not observed.");
        Assert(child.Process.ExitCode == 0, "Client process failed.");
    }

    private static async Task RunCdnInstallTestsAsync(string temporaryDirectory)
    {
        string privateKey = Path.Combine(temporaryDirectory, "private.pem");
        string publicKey = Path.Combine(temporaryDirectory, "public.pem");
        CdnSigningKeyGenerator.Generate(privateKey, publicKey, overwrite: false);

        string source = Path.Combine(temporaryDirectory, "SourceGame");
        CreateFakeGame(source, new byte[] { 1, 2, 3 }, includeObsolete: true);
        CdnPackageBuildResult v1 = await CdnPackageBuilder.BuildAsync(
            source,
            Path.Combine(temporaryDirectory, "V1"),
            "1.0.0",
            privateKey,
            chunkSizeBytes: 2);
        Assert(v1.Manifest.SchemaVersion == 3, "CDN package did not use schema v3.");
        Assert(v1.Manifest.MinimumLauncherVersion == LauncherVersion.Current,
            "CDN package has the wrong minimum launcher version.");
        Assert(v1.Manifest.FullPackage.FileName == "AAALOL-1.0.0-full.zip",
            "Full package did not retain a local reconstructed filename.");
        Assert(v1.Manifest.FullPackage.Chunks.All(
                chunk => chunk.Path.StartsWith("content/", StringComparison.Ordinal)),
            "Full package referenced a legacy CDN directory.");
        Assert(v1.Manifest.Files.SelectMany(entry => entry.Chunks).All(
                chunk => chunk.Path.StartsWith("content/", StringComparison.Ordinal)),
            "Incremental content referenced a legacy CDN directory.");
        string[] uploadDirectories = Directory
            .EnumerateDirectories(v1.UploadRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        Assert(uploadDirectories.SequenceEqual(new[] { "content" }),
            "Upload root contains directories other than content.");
        string[] uploadFiles = Directory
            .EnumerateFiles(v1.UploadRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        Assert(uploadFiles.SequenceEqual(new[] { "client-manifest.json", "client-manifest.sig" }),
            "Upload root contains unexpected metadata files.");
        await CdnPackageBuilder.AuditAsync(v1.UploadRoot, publicKey, privateKeyPath: null);
        string unreferencedPath = Path.Combine(v1.UploadRoot, "content", "unreferenced");
        File.WriteAllBytes(unreferencedPath, new byte[] { 1 });
        await AssertThrowsAsync<InvalidDataException>(
            () => CdnPackageBuilder.AuditAsync(v1.UploadRoot, publicKey, privateKeyPath: null),
            "CDN audit accepted an unreferenced content file.");
        File.Delete(unreferencedPath);

        string gameRoot = Path.Combine(temporaryDirectory, "Demo", "Game");
        string cacheRoot = Path.Combine(temporaryDirectory, "Cache");
        Directory.CreateDirectory(Path.GetDirectoryName(gameRoot)!);
        using CdnInstallService installer = new(cacheRoot, publicKey, allowInsecureHttp: true);
        using (LoopbackFileServer server = new(v1.UploadRoot))
        {
            CdnClientCheckResult beforeInstall = await installer.CheckRequiredActionAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                CancellationToken.None);
            Assert(beforeInstall.RequiredAction == CdnRequiredAction.Download,
                "Empty Game did not request an explicit download.");
            CdnInstallResult result = await installer.EnsureCurrentAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                progress: null,
                CancellationToken.None);
            Assert(result.Changed && result.UsedFullPackage, "Empty Game did not use the full package.");
            Assert(File.Exists(Path.Combine(gameRoot, LauncherPaths.GameExecutableName)),
                "Full install did not create AAALOL.exe.");
            Assert(File.Exists(Path.Combine(gameRoot, "obsolete.bin")),
                "Full install omitted a declared file.");
            CdnClientCheckResult afterInstall = await installer.CheckRequiredActionAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                CancellationToken.None);
            Assert(afterInstall.RequiredAction == CdnRequiredAction.Start,
                "Completed download did not transition to explicit start.");
        }

        string missingChunkGameRoot = Path.Combine(temporaryDirectory, "MissingChunkDemo", "Game");
        CdnChunkEntry missingChunk = v1.Manifest.FullPackage.Chunks[0];
        string missingChunkPath = CdnPath.ResolveUnderRoot(v1.UploadRoot, missingChunk.Path);
        string savedChunkPath = missingChunkPath + ".saved";
        File.Move(missingChunkPath, savedChunkPath);
        try
        {
            using LoopbackFileServer server = new(v1.UploadRoot);
            using CdnInstallService missingInstaller = new(
                Path.Combine(temporaryDirectory, "MissingChunkCache"),
                publicKey,
                allowInsecureHttp: true);
            await AssertThrowsAsync<CdnDownloadException>(
                () => missingInstaller.EnsureCurrentAsync(
                    missingChunkGameRoot,
                    CreateLoopbackConfig(server),
                    progress: null,
                    CancellationToken.None),
                "Missing full-package chunk was accepted.");
            Assert(!Directory.Exists(missingChunkGameRoot),
                "Missing full-package chunk created a partial Game directory.");
        }
        finally
        {
            File.Move(savedChunkPath, missingChunkPath);
        }

        File.WriteAllBytes(Path.Combine(source, "AAALOL_Data", "content.bin"), new byte[] { 9, 8, 7, 6 });
        File.Delete(Path.Combine(source, "obsolete.bin"));
        CdnPackageBuildResult v2 = await CdnPackageBuilder.BuildAsync(
            source,
            Path.Combine(temporaryDirectory, "V2"),
            "1.1.0",
            privateKey,
            chunkSizeBytes: 2);
        CdnFileEntry resumedEntry = v2.Manifest.Files.Single(entry => entry.Path == "AAALOL_Data/content.bin");
        CdnChunkEntry resumedChunk = resumedEntry.Chunks[0];
        string partialPath = Path.Combine(cacheRoot, "chunks", resumedChunk.Sha256 + ".part");
        Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
        byte[] resumedObject = File.ReadAllBytes(CdnPath.ResolveUnderRoot(v2.UploadRoot, resumedChunk.Path));
        File.WriteAllBytes(partialPath, resumedObject[..1]);
        using (LoopbackFileServer server = new(v2.UploadRoot))
        {
            CdnClientCheckResult beforeUpdate = await installer.CheckRequiredActionAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                CancellationToken.None);
            Assert(beforeUpdate.RequiredAction == CdnRequiredAction.Update,
                "Outdated Game did not request an explicit update.");
            CdnInstallResult result = await installer.EnsureCurrentAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                progress: null,
                CancellationToken.None);
            Assert(result.Changed && !result.UsedFullPackage, "Existing Game did not use incremental update.");
            Assert(result.DownloadedFileCount == 1, "Incremental update downloaded an unexpected file count.");
            Assert(!File.Exists(Path.Combine(gameRoot, "obsolete.bin")), "Deleted file survived target assembly.");
            Assert(File.ReadAllBytes(Path.Combine(gameRoot, "AAALOL_Data", "content.bin"))
                    .SequenceEqual(new byte[] { 9, 8, 7, 6 }),
                "Changed file was not installed.");
            CdnClientCheckResult afterUpdate = await installer.CheckRequiredActionAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                CancellationToken.None);
            Assert(afterUpdate.RequiredAction == CdnRequiredAction.Start,
                "Completed update did not transition to explicit start.");
        }

        File.WriteAllBytes(
            Path.Combine(source, "AAALOL_Data", "content.bin"),
            new byte[] { 1, 1, 1, 1 });
        CdnPackageBuildResult sameVersionReplacement = await CdnPackageBuilder.BuildAsync(
            source,
            Path.Combine(temporaryDirectory, "V2SameVersion"),
            "1.1.0",
            privateKey,
            chunkSizeBytes: 2);
        using (LoopbackFileServer server = new(sameVersionReplacement.UploadRoot))
        {
            CdnInstallResult result = await installer.EnsureCurrentAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                progress: null,
                CancellationToken.None);
            Assert(result.Changed, "Same-version manifest replacement was skipped.");
            Assert(File.ReadAllBytes(Path.Combine(gameRoot, "AAALOL_Data", "content.bin"))
                    .SequenceEqual(new byte[] { 1, 1, 1, 1 }),
                "Same-version, same-size changed content was not installed.");
        }

        File.WriteAllBytes(
            Path.Combine(gameRoot, "AAALOL_Data", "content.bin"),
            new byte[] { 2, 2, 2, 2 });
        Assert(!await installer.ValidateTrustedInstallAsync(gameRoot, CancellationToken.None),
            "Same-size local corruption was accepted as trusted.");
        using (LoopbackFileServer server = new(sameVersionReplacement.UploadRoot))
        {
            CdnClientCheckResult corruptState = await installer.CheckRequiredActionAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                CancellationToken.None);
            Assert(corruptState.RequiredAction == CdnRequiredAction.Update,
                "Corrupt installed Game did not return to update.");
            CdnInstallResult result = await installer.EnsureCurrentAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                progress: null,
                CancellationToken.None);
            Assert(result.Changed && result.UsedFullPackage,
                "Same-size local corruption did not trigger a full repair.");
        }
        Assert(await installer.ValidateTrustedInstallAsync(gameRoot, CancellationToken.None),
            "Repaired client did not pass trusted full validation.");

        byte[] validSignature = File.ReadAllBytes(v2.SignaturePath);
        File.WriteAllBytes(v2.SignaturePath, new byte[] { 1, 2, 3, 4 });
        using (LoopbackFileServer server = new(v2.UploadRoot))
        {
            await AssertThrowsAsync<InvalidOperationException>(
                () => installer.EnsureCurrentAsync(
                    gameRoot,
                    CreateLoopbackConfig(server),
                    progress: null,
                    CancellationToken.None),
                "Invalid manifest signature was accepted.");
        }
        File.WriteAllBytes(v2.SignaturePath, validSignature);

        File.WriteAllBytes(Path.Combine(source, "AAALOL_Data", "content.bin"), new byte[] { 4, 5, 6, 7, 8 });
        CdnPackageBuildResult v3 = await CdnPackageBuilder.BuildAsync(
            source,
            Path.Combine(temporaryDirectory, "V3"),
            "1.2.0",
            privateKey,
            chunkSizeBytes: 2);
        CdnFileEntry changedEntry = v3.Manifest.Files.Single(entry => entry.Path == "AAALOL_Data/content.bin");
        File.WriteAllBytes(
            CdnPath.ResolveUnderRoot(v3.UploadRoot, changedEntry.Chunks[0].Path),
            new byte[] { 0xFF });
        installer.BackupCleanupOverride = _ => false;
        using (LoopbackFileServer server = new(v3.UploadRoot))
        {
            CdnInstallResult result = await installer.EnsureCurrentAsync(
                gameRoot,
                CreateLoopbackConfig(server),
                progress: null,
                CancellationToken.None);
            Assert(result.Changed && result.UsedFullPackage,
                "Corrupt incremental object did not fall back to the valid full package.");
        }
        Assert(File.ReadAllBytes(Path.Combine(gameRoot, "AAALOL_Data", "content.bin"))
                .SequenceEqual(new byte[] { 4, 5, 6, 7, 8 }),
            "Full-package fallback did not install the expected client.");
        Assert(Directory.Exists(gameRoot + ".__backup"),
            "Backup-cleanup failure was not retained for recovery.");
        Assert(await installer.ValidateTrustedInstallAsync(gameRoot, CancellationToken.None),
            "Backup-cleanup failure removed or damaged the committed client.");
        installer.BackupCleanupOverride = null;
        installer.RecoverInterruptedInstall(gameRoot);
        Assert(!Directory.Exists(gameRoot + ".__backup"),
            "Deferred backup cleanup was not retried during recovery.");

        string backup = gameRoot + ".__backup";
        string staging = gameRoot + ".__staging";
        Directory.Move(gameRoot, backup);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "partial.tmp"), "partial");
        installer.RecoverInterruptedInstall(gameRoot);
        Assert(Directory.Exists(gameRoot), "Interrupted install backup was not restored.");
        Assert(!Directory.Exists(staging), "Interrupted install staging was not removed.");

        Directory.Move(gameRoot, backup);
        Directory.CreateDirectory(gameRoot);
        File.WriteAllText(Path.Combine(gameRoot, "invalid.partial"), "invalid");
        Directory.CreateDirectory(staging);
        installer.RecoverInterruptedInstall(gameRoot);
        Assert(File.Exists(Path.Combine(gameRoot, LauncherPaths.GameExecutableName)),
            "Invalid swapped Game was preferred over the valid backup.");
        Assert(!Directory.Exists(backup), "Recovered backup directory was not consumed.");

        Directory.Move(gameRoot, backup);
        Directory.CreateDirectory(gameRoot);
        File.WriteAllText(Path.Combine(gameRoot, "invalid-current.tmp"), "invalid");
        File.Delete(Path.Combine(backup, CdnInstallService.InstalledSignatureFileName));
        AssertThrows<InvalidDataException>(
            () => installer.RecoverInterruptedInstall(gameRoot),
            "Recovery accepted two untrusted candidates.");
        Assert(Directory.Exists(gameRoot) && Directory.Exists(backup),
            "Recovery deleted evidence when both candidates were untrusted.");
    }

    private static CdnLauncherConfig CreateLoopbackConfig(LoopbackFileServer server)
    {
        return new CdnLauncherConfig
        {
            Enabled = true,
            ManifestUrlOverride = new Uri(server.BaseUri, "client-manifest.json").AbsoluteUri,
            ManifestSignaturePath = "client-manifest.sig",
            MaxAttempts = 1
        };
    }

    private static void CreateFakeGame(string root, byte[] content, bool includeObsolete)
    {
        Directory.CreateDirectory(Path.Combine(root, "AAALOL_Data"));
        File.WriteAllBytes(Path.Combine(root, LauncherPaths.GameExecutableName), new byte[] { 0x4D, 0x5A, 1, 2 });
        File.WriteAllBytes(Path.Combine(root, "AAALOL_Data", "content.bin"), content);
        if (includeObsolete)
        {
            File.WriteAllBytes(Path.Combine(root, "obsolete.bin"), new byte[] { 7, 7 });
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}

internal sealed class LoopbackFileServer : IDisposable
{
    private readonly string _root;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _acceptLoop;

    public LoopbackFileServer(string root)
    {
        _root = Path.GetFullPath(root);
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _acceptLoop = AcceptLoopAsync(_cancellation.Token);
    }

    public Uri BaseUri { get; }

    public void Dispose()
    {
        _cancellation.Cancel();
        _listener.Stop();
        try
        {
            _acceptLoop.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
        _cancellation.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new(stream, Encoding.ASCII, false, 4096, leaveOpen: true))
        {
            string? requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                int separator = line.IndexOf(':');
                if (separator > 0)
                {
                    headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
                }
            }

            string[] requestParts = requestLine.Split(' ');
            if (requestParts.Length < 2 || !string.Equals(requestParts[0], "GET", StringComparison.Ordinal))
            {
                await WriteStatusAsync(stream, 405, "Method Not Allowed", cancellationToken);
                return;
            }

            string relative;
            try
            {
                relative = CdnPath.NormalizeRelative(Uri.UnescapeDataString(requestParts[1].TrimStart('/')));
            }
            catch (InvalidDataException)
            {
                await WriteStatusAsync(stream, 400, "Bad Request", cancellationToken);
                return;
            }

            string filePath = CdnPath.ResolveUnderRoot(_root, relative);
            if (!File.Exists(filePath))
            {
                await WriteStatusAsync(stream, 404, "Not Found", cancellationToken);
                return;
            }

            FileInfo info = new(filePath);
            long offset = 0;
            bool partial = false;
            if (headers.TryGetValue("Range", out string? range) &&
                range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(range[6..].TrimEnd('-'), out long parsedOffset) &&
                parsedOffset >= 0 && parsedOffset < info.Length)
            {
                offset = parsedOffset;
                partial = true;
            }

            long contentLength = info.Length - offset;
            StringBuilder response = new();
            response.Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n");
            response.Append("Connection: close\r\nAccept-Ranges: bytes\r\n");
            response.Append("Content-Length: ").Append(contentLength).Append("\r\n");
            if (partial)
            {
                response.Append("Content-Range: bytes ")
                    .Append(offset).Append('-').Append(info.Length - 1).Append('/')
                    .Append(info.Length).Append("\r\n");
            }
            response.Append("\r\n");
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response.ToString()), cancellationToken);
            await using FileStream file = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            file.Position = offset;
            await file.CopyToAsync(stream, 64 * 1024, cancellationToken);
        }
    }

    private static async Task WriteStatusAsync(
        Stream stream,
        int code,
        string text,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {code} {text}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
        await stream.WriteAsync(bytes, cancellationToken);
    }
}
