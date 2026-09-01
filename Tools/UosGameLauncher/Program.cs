namespace FrameSyncMoba.GameLauncher;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (HasFlag(args, "--test-client"))
            {
                Thread.Sleep(150);
                return 0;
            }

            if (HasFlag(args, "--self-test"))
            {
                return await LauncherSelfTest.RunAsync();
            }

            if (HasFlag(args, "--generate-cdn-signing-key"))
            {
                return GenerateSigningKey(args);
            }

            if (HasFlag(args, "--build-cdn-package"))
            {
                return await BuildCdnPackageAsync(args);
            }

            if (HasFlag(args, "--audit-cdn-package"))
            {
                return await AuditCdnPackageAsync(args);
            }

            if (HasFlag(args, "--build-bootstrap-package"))
            {
                return await BuildBootstrapPackageAsync(args);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }

        using Mutex mutex = new(false, "FrameSyncMobaDemo.GameLauncher", out bool createdNew);
        if (!createdNew)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int GenerateSigningKey(string[] args)
    {
        string projectRoot = RequireProjectRoot();
        string privateKeyPath = GetOption(
            args,
            "--private-key",
            Path.Combine(projectRoot, "Builds", "CdnSigning", "FrameSyncMobaCdnPrivateKey.pem"));
        string publicKeyPath = GetOption(
            args,
            "--public-key",
            Path.Combine(projectRoot, "Tools", "UosGameLauncher", "CdnSigningPublicKey.pem"));
        CdnSigningKeyGenerator.Generate(
            privateKeyPath,
            publicKeyPath,
            overwrite: HasFlag(args, "--overwrite"));
        Console.WriteLine("CDN signing key created.");
        Console.WriteLine("Private key (secret, ignored build path): " + Path.GetFullPath(privateKeyPath));
        Console.WriteLine("Public key (publish with launcher): " + Path.GetFullPath(publicKeyPath));
        return 0;
    }

    private static async Task<int> BuildCdnPackageAsync(string[] args)
    {
        string projectRoot = RequireProjectRoot();
        string version = GetRequiredOption(args, "--version");
        string source = GetOption(
            args,
            "--source",
            Path.Combine(projectRoot, "Builds", "Demo", "Game"));
        string output = GetOption(
            args,
            "--output",
            Path.Combine(projectRoot, "Builds", "CdnUpload", version));
        string privateKey = GetOption(
            args,
            "--private-key",
            Path.Combine(projectRoot, "Builds", "CdnSigning", "FrameSyncMobaCdnPrivateKey.pem"));
        CdnPackageBuildResult result = await CdnPackageBuilder.BuildAsync(
            source,
            output,
            version,
            privateKey);
        Console.WriteLine($"CDN package {result.Manifest.ClientVersion} built successfully.");
        Console.WriteLine($"Files: {result.Manifest.Files.Count}; unique content: {result.UniqueContentCount}");
        Console.WriteLine("Upload this directory: " + result.UploadRoot);
        return 0;
    }

    private static async Task<int> AuditCdnPackageAsync(string[] args)
    {
        string projectRoot = RequireProjectRoot();
        string input = GetRequiredOption(args, "--input");
        string publicKey = GetOption(
            args,
            "--public-key",
            Path.Combine(projectRoot, "Tools", "UosGameLauncher", "CdnSigningPublicKey.pem"));
        ClientReleaseManifest manifest = await CdnPackageBuilder.AuditAsync(
            input,
            publicKey,
            privateKeyPath: null);
        Console.WriteLine(
            $"CDN package audit passed: {manifest.ClientVersion}, {manifest.Files.Count} files.");
        return 0;
    }

    private static async Task<int> BuildBootstrapPackageAsync(string[] args)
    {
        string projectRoot = RequireProjectRoot();
        string version = GetOption(args, "--version", LauncherVersion.Current);
        if (!Version.TryParse(version, out _))
        {
            throw new ArgumentException("首包版本必须是数字版本号。", "--version");
        }

        string launcher = GetOption(
            args,
            "--launcher",
            Path.Combine(projectRoot, "Builds", "Demo", "Launcher"));
        string output = GetOption(
            args,
            "--output",
            Path.Combine(
                projectRoot,
                "Builds",
                "Bootstrap",
                $"FrameSyncMobaDemo-Bootstrap-{version}.zip"));
        BootstrapPackageBuildResult result = await BootstrapPackageBuilder.BuildAsync(launcher, output);
        Console.WriteLine(
            $"Bootstrap package built: {result.PackagePath} ({result.PackageSize / 1024 / 1024} MiB)");
        Console.WriteLine("SHA-256: " + result.Sha256);
        return 0;
    }

    private static string RequireProjectRoot()
    {
        return ProjectLocator.FindProjectRoot() ??
               throw new InvalidOperationException(
                   "无法定位 Unity 项目根目录；请从项目内运行启动器开发命令。");
    }

    private static bool HasFlag(IEnumerable<string> args, string flag)
    {
        return args.Any(value => string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRequiredOption(string[] args, string name)
    {
        string value = GetOption(args, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"缺少必需参数 {name}。");
        }

        return value;
    }

    private static string GetOption(string[] args, string name, string defaultValue)
    {
        string prefix = name + "=";
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return argument[prefix.Length..].Trim();
            }

            if (string.Equals(argument, name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return args[index + 1].Trim();
            }
        }

        return defaultValue;
    }
}
