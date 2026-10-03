# CDN 扁平内容布局

## 本次执行范围

本计划对应原编码 0159 的一次执行：CDN 扁平内容布局。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [安全分片上传与可选发布包](../../requirements/launchers/REQ-FEAT-084_safe-content-upload.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [增量更新与内容寻址分片](../../requirements/launchers/REQ-FEAT-083_content-updates.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Tools/UosGameLauncher/CdnPackageBuilder.cs`：`CdnPackageBuilder`。
- `Tools/UosGameLauncher/CdnUpdater.cs`：`CdnUpdatePhase`、`CdnRequiredAction`、`CdnDownloadException`、`CdnDownloadIntegrityException`、`GameClientRunningException`、`CdnDownloadClient`、`CdnInstallService`。
- `Tools/UosGameLauncher/CdnContracts.cs`：`LauncherVersion`、`CdnLauncherConfig`、`ClientReleaseManifest`、`CdnPackageEntry`、`CdnFileEntry`、`CdnChunkEntry`、`CdnJson`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Tools/UosGameLauncher/CdnPackageBuilder.cs`：

```csharp
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
// 方法后续请阅读上述真实源码；这里是节选。
```

`Tools/UosGameLauncher/CdnUpdater.cs`：

```csharp
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
```

### 输入输出与边界

**安全分片上传与可选发布包**

正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。

不修改测试 Builds/UosClient；上传失败不能报告发布成功；私钥不进库；Release ZIP 仅在用户接受后进入 Git。

**增量更新与内容寻址分片**

schema-v3 以 content/<sha256> 标识不超过 95,000,000 字节分片；旧完整内容经校验可复用，新文件按清单组装。

哈希不匹配不能复用；缺旧分片时走完整获取；目标路径规范化和目录穿越检查必需。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Tools/UosGameLauncher/LauncherSelfTest.cs`：启动器自检（本轮不执行），程序集 `独立桌面项目或实际asmdef`，函数 `RunAsync`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
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
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
