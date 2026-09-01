# UOS 客户端 GUI 启动器（开发者工具）

> Document class: Operational Guide
> Default read: only when launching packaged UOS clients is in scope

## 用途

`UosClientLauncher` 是一个**仅供开发者使用**的独立 Windows GUI 工具，用于
启动一个或多个 UOS 测试客户端。它不实现或替代 UOS 登录协议：客户端仍由现有
`ClientBootstrap`、`TestAccountBootstrapService` 和 `UosClientSession`
完成初始化与登录。

它不是玩家正式入口。正式 Demo 启动器位于
`Tools/UosGameLauncher`，发布到 `Builds/Demo/Launcher`，只需要填写登录名；
其目录约定、美术资源和白板 UI 见 `GAME_LAUNCHER_GUIDE.md`。不要把本工具复制
或改名后当作正式启动器发布。

启动器只负责：

- 选择 `FrameSyncMobaClient.exe`；
- 为每个实例传入唯一的 `--TestAccountId`；
- 传入 `-onlineFlow`；
- 为每个实例创建独立的 `-logFile`；
- 配置窗口分辨率、匹配配置 ID、区域 ID及额外参数；
- 启动后按进程设置不同的 Windows 窗口标题；
- 查看 PID、运行状态，并按需停止客户端。

## 直接使用

已发布程序的位置：

```text
Builds/Tools/UosClientLauncher/FrameSyncMoba.UosClientLauncher.exe
```

双击该程序即可，不需要 PowerShell。首次启动默认寻找：

```text
Builds/UosClient/FrameSyncMobaClient.exe
```

默认创建两个实例，并为它们生成不同的 `TestAccountId`。勾选需要启动的
实例后点击“启动已勾选”。启动器会在最初 30 秒持续检查该进程的新窗口，
因此 Unity 启动画面切换成正式 Player 窗口后仍会应用正确的实例标题。

注意：

- 同时启动的客户端不能共用 `TestAccountId`；启动器会在启动前拦截重复值。
- 关闭启动器不会关闭已经启动的客户端。
- “停止客户端”会先请求正常关闭，3 秒后仍未退出才结束进程树。
- “关闭帧同步异步诊断”对应运行时参数
  `-disableFrameSyncDiagnostics`；若构建时已编译移除诊断代码，此选项不会产生额外效果。
- “详细校验日志”对应 `-checksumDetail`，只在排查同步问题时开启。

## 配置保存位置

配置按 Windows 用户保存在：

```text
%LocalAppData%/FrameSyncMobaDemo/UosClientLauncher/launcher.settings.json
```

仓库不会保存个人实例 ID 或本地路径。

## 重新构建启动器

开发机安装 .NET 8 SDK 后，双击：

```text
Tools/UosClientLauncher/BuildLauncher.cmd
```

也可以执行：

```text
dotnet publish Tools/UosClientLauncher/FrameSyncMoba.UosClientLauncher.csproj ^
  -c Release -r win-x64 --self-contained false ^
  -o Builds/Tools/UosClientLauncher
```

当前发布方式是单文件、依赖 .NET 8 Desktop Runtime 的 `win-x64` 程序。
这能保持工具体积较小；若目标机器没有对应运行时，安装 .NET 8 Desktop
Runtime，或将 `--self-contained false` 改成 `true` 重新发布。

## 与客户端参数的对应关系

| GUI 设置 | 客户端命令行参数 |
| --- | --- |
| TestAccountId | `--TestAccountId=<value>` |
| 在线 UOS 流程 | `-onlineFlow`（始终传入） |
| 匹配配置 ID | `-matchmakingConfigId=<value>` |
| 区域 ID | `-uosRegionId=<value>` |
| 日志文件 | `-logFile <unique-path>` |
| 窗口化 | `-screen-fullscreen 0` |
| 窗口尺寸 | `-screen-width`、`-screen-height` |
| 详细校验日志 | `-checksumDetail` |
| 关闭帧同步异步诊断 | `-disableFrameSyncDiagnostics` |

窗口标题不作为客户端命令行协议的一部分。启动器只修改属于目标客户端
PID 的顶层 Windows 窗口，不依赖两个实例的启动先后顺序。
