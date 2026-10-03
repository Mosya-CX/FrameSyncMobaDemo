# 正式游戏启动器

## 本次执行范围

本计划对应原编码 0155 的一次执行：正式游戏启动器。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [游戏下载校验与完整安装](../../requirements/launchers/REQ-FEAT-082_game-install.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [增量更新与内容寻址分片](../../requirements/launchers/REQ-FEAT-083_content-updates.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

清单决定版本、文件与内容分片；下载先校验签名和 SHA-256，再在独立暂存目录组装，最终安装提交只覆盖清单所属内容。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Tools/UosGameLauncher/MainForm.cs`：`LauncherPrimaryAction`、`MainForm`、`LauncherPalette`、`WhiteboardPanel`。
- `Tools/UosGameLauncher/LauncherServices.cs`：`LauncherPaths`、`ProjectLocator`、`GameInstallLocator`、`LauncherSettingsStore`、`GameLaunchArgumentBuilder`、`ManagedGameProcess`、`GameProcessManager`。
- `Tools/UosGameLauncher/LauncherModels.cs`：`LauncherSettings`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Tools/UosGameLauncher/MainForm.cs`：

```csharp
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FrameSyncMoba.GameLauncher;

internal enum LauncherPrimaryAction
{
    Checking,
    Download,
    Update,
    Start,
    CancelDownload,
    CancelUpdate,
    Stop,
    Disabled
}
```

`Tools/UosGameLauncher/LauncherServices.cs`：

```csharp
    public static IReadOnlyList<string> Build(LauncherSettings settings)
    {
        List<string> arguments = new()
        {
            "-onlineFlow"
        };

        AddValue(arguments, "--TestAccountId", settings.LoginName);
        return arguments;
    }
```

### 输入输出与边界

**游戏下载校验与完整安装**

清单决定版本、文件与内容分片；下载先校验签名和 SHA-256，再在独立暂存目录组装，最终安装提交只覆盖清单所属内容。

断网、取消、磁盘不足、损坏分片与中断安装可见失败；不覆盖用户保存数据和无关目录。

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
