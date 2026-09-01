# 打包 / 构建指南（Local NGO C/S 与 UOS）

> Document class: Operational Guide
> Default read: only when packaging or build output is in scope

本文档记录本项目打包的入口、方式与必须遵守的操作纪律。

## 1. 入口

所有打包逻辑集中在：

```text
Assets/Editor/LocalNgoBuildMenu.cs
命名空间：FrameSyncMoba.EditorTools
类型：LocalNgoBuildMenu（静态类）
```

Unity 菜单路径：

```text
FrameSyncMoba/Build Local NGO/
    Build Server
    Build Server Linux (UOS)
    Build Client
    Build Client Windows (UOS)
    Build Release Client (Optional CDN Package)...
    Build Client + Server (UOS, Once)
    Build Both
    Clear Build-Both Retry Guard
```

## 2. 打包方法一览

| 方法 | 目标平台 | 场景 | 输出路径 |
|---|---|---|---|
| `BuildServer()` | Windows64 DedicatedServer（Development） | ServerBootstrap + Lobby + GameScene | `Builds/LocalNgo/Server/FrameSyncMobaServer.exe` |
| `BuildClient()` | Windows64 Player（Development） | ClientBootstrap + Lobby + GameScene | `Builds/LocalNgo/Client/FrameSyncMobaClient.exe` |
| `BuildBoth()` | 上述两者依次构建 | 同上 | 同上 |
| `BuildServerLinux()` | Linux64 DedicatedServer | ServerBootstrap + Lobby + GameScene | `Builds/UosServer/FrameSyncMobaServer.x86_64` |
| `BuildClientUos()` | Windows64 Player（Development、UOS Online） | ClientBootstrap + Lobby + GameScene | `Builds/UosClient/FrameSyncMobaClient.exe` |
| `BuildReleaseClient(version, buildCdnPackage)` | Windows64 Player（非 Development、UOS Online）；分片为可选后处理 | ClientBootstrap + Lobby + GameScene | `Builds/Demo/Game/AAALOL.exe`、可选 `Builds/CdnUpload/<version>/Upload/` |
| `BuildUosClientAndServerOnce()` | UOS Windows Client 后接 Linux DedicatedServer，并自动压缩服务端 | 同上 | `Builds/UosClient/`、`Builds/UosServer/`、`Builds/UosUpload/` |
| `ClearBuildBothRetryGuard()` | - | - | 清除 120 秒防重守卫 |

本地 C/S 测试包 = `BuildBoth()`：先 Server 后 Client。

### 2.1 UOS Client 与 CDN 分片关系

`BuildClientUos()` 和 `BuildUosClientAndServerOnce()` 只负责生成测试用的完整
`Builds/UosClient/FrameSyncMobaClient.exe` Player；它们不会调用正式 Launcher 的
CDN 签名/分片器，也不会被正式发布构建覆盖。

正式发布客户端使用独立菜单窗口
`Build Release Client (Optional CDN Package)...`。它固定输出
`Builds/Demo/Game/AAALOL.exe`，因此 Unity 自动生成的同名内容为
`AAALOL_Data`、`AAALOL_*` 等。窗口中的“构建成功后生成签名 CDN 分片”默认不勾选：

- 不勾选：只构建正式 Player；
- 勾选：Player 成功后继续调用现有 schema-v3 分片器，输出
  `Builds/CdnUpload/<版本>/Upload`；
- Player 构建失败时不会运行分片器；两个流程都不写入 `Builds/UosClient`。

为避免上一版残留文件被写入新清单，这个正式入口会在 Player 构建开始前清空它
独占的 `Builds/Demo/Game`。它不会清理同级 `Demo/Launcher`、测试用
`Builds/UosClient` 或 Git 跟踪的 `Release`。

如果正式 Player 已经存在，也可继续单独运行：

```text
Tools/UosGameLauncher/BuildCdnPackage.cmd <客户端版本>
```

该命令同样读取 `Builds/Demo/Game`，生成签名 schema-v3 清单和不超过
95,000,000 字节的 `content/<sha256>`。不要把测试目录
`Builds/UosClient` 直接当作 Launcher 发布源。

### 2.2 Git 发布目录边界

`Builds/` 是可重复生成的本地工作目录并由 `.gitignore` 排除。只有经过完整验收、
确定要发布的客户端和服务端压缩包才由发布者复制到：

```text
Release/<发布版本>/Client/
Release/<发布版本>/Server/
```

Git 提交/推送发布物时只选择 `Release/`，不要强制添加 `Builds/`。`Release/**/*.zip`
由 `.gitattributes` 交给 Git LFS；签名私钥、UOS 凭据、日志、缓存、未验收构建和
CDN 中间目录不得进入 `Release`。替换一个发布包前，应核对版本、SHA-256 和对应
客户端/服务端验收结果。

### 2.3 本地 Addressables 与 Dedicated Server

普通 Windows 客户端构建会随 Player 构建当前平台的本地 Addressables
catalog/bundle；没有远程 catalog、下载或热更新步骤。正式根资产位于六个
`Client-*` 组，安装包内由本地 catalog 解析。

构建入口会在 Addressables 开始前显式切换活动目标：Windows 客户端必须是
`StandaloneWindows64/Player`，UOS 服务端必须是
`StandaloneLinux64/Server`。客户端构建前只清理上次生成的
`<Client>_Data/StreamingAssets/aa`，保留 Lua 等其他 StreamingAssets；构建后
检查 `settings.json` 的 `m_buildTarget`、平台目录和 Bundle。Windows 包中若出现
`StandaloneLinux64` 内容会直接使构建失败，不能交付一个流程可运行但 Shader
全部变紫的包。

Dedicated Server 构建进入专用资源作用域：

- 不构建 Addressables 客户端内容；
- 即使磁盘上已有上一次客户端构建的 Addressables 输出，也不会将其复制进
  Server 的 `StreamingAssets`；
- 构建前剥离 Server 场景中的客户端表现对象/组件；
- 构建后审计 catalog、bundle、模型、动画、材质、VFX、音频和 UI 依赖，
  发现禁用资源则使构建失败。

因此不要在打 Server 前手动删除客户端 Addressables 输出，也不要把
`Assets/ClientContent/` 添加到 Server 场景或 Resources。完整边界和新增资源流程见
`Docs/Implementation/Addressables/RESOURCE_ARCHITECTURE.md`。

## 3. 调用方式

### 3.1 Unity 菜单手动打包

编辑器内点击 `FrameSyncMoba/Build Local NGO/Build Both`。

### 3.2 通过 Unity MCP 一次性调用

使用 `script-execute`（body-only）执行：

```text
FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildBoth();
```

### 3.3 UOS 一键构建

在 Unity 编辑器中点击：

```text
FrameSyncMoba/Build Local NGO/Build Client + Server (UOS, Once)
```

该入口依次构建 UOS Windows 客户端和 Linux Dedicated Server。服务端构建成功后会自动生成上传 ZIP 与 SHA-256 文件。组合入口与两个子步骤都有 120 秒防重守卫；重复菜单调用会被忽略，若前一轮只完成了一端，短时间重试也只会继续尚未成功的一端。

通过 Unity MCP 调用时只需执行一次：

```text
FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildUosClientAndServerOnce();
```

### 3.3.1 正式客户端与可选 CDN 分片

在 Unity 编辑器中点击：

```text
FrameSyncMoba/Build Local NGO/Build Release Client (Optional CDN Package)...
```

保持复选框关闭并点击“构建正式客户端”，只会生成
`Builds/Demo/Game/AAALOL.exe`、`AAALOL_Data` 及其他 Unity Player 配套文件。
需要本次构建成功后直接生成 CDN 上传内容时，勾选复选框并填写客户端版本；分片器
成功后只把 `Builds/CdnUpload/<版本>/Upload` 内的内容上传到 Bucket 根目录，不要
上传外层 `Upload` 文件夹。此入口不会自动把任何中间产物复制到 Git 跟踪的
`Release`；验收完成后仍由发布者选择最终客户端/服务端压缩包放入 `Release`。

### 3.4 UOS 服务器镜像

构建 `Build Server Linux (UOS)`，将 `Builds/UosServer/` 目录打成 zip 上传到 UOS 控制台。

2026-08-10 已真实通过 UOS Ready 和两客户端对局的镜像配置如下：

```text
需要增加执行权限的文件：FrameSyncMobaServer.x86_64
入口程序启动命令：./FrameSyncMobaServer.x86_64 -batchmode -nographics
协议和端口：UDP 7777
挂载文件：无
自定义环境变量：无（UOS 注入 allocation/match/Agones 运行变量）
```

历史资源规格证据：`1 CPU / 1536 MB` 可在约 10 秒内进入 Ready；
更小的 CPU/内存联动档位曾在 Ready 超时前失败。相同镜像提高规格后成功，
因此小规格失败不能直接归因于 Ready SDK 调用错误。D-048 已在源代码中排除
客户端 Addressables 与表现资源，但新的 Linux Player 包大小、启动峰值内存和
Ready 时间必须以 ExecPlan 0138 的最终 BuildReport/UOS 实测为准，不能沿用旧包
约 330 MB `resources.assets` 的数值，也不要通过删除 Ready 调用或盲目延长超时
掩盖资源问题。

UOS 标识不能混用：

```text
Multiverse 启动配置/Profile ID：0fc730a2-ce02-4768-8a75-713ddb36c3b0
Matchmaking config ID：f01c4e66-0023-43f6-af57-dcd8b73e7b90
```

运行时 `MatchmakingConfigID` 必须使用第二项；第一项填入后会产生
`The config [...] is not found`。不要把应用密钥、服务器密钥、allocation
UUID、room ID 或 SDK 端口抄入文档或自定义环境变量。UOS 控制台/日志中出现
秘密值时必须先脱敏。

### 3.5 用 UOS CDN 分发完整 Windows 客户端

正式 Demo 使用 Launcher 管理的完整 Player 分发，不把 UOS SDK 或密钥嵌入玩家
程序。当前验收客户端必须先复制/构建为：

```text
Builds/Demo/Game/AAALOL.exe
Builds/Demo/Game/AAALOL_Data/
```

第一次使用先生成并离线备份签名私钥，然后为每个客户端版本生成上传目录：

```powershell
Tools/UosGameLauncher/GenerateCdnSigningKey.cmd
Tools/UosGameLauncher/BuildCdnPackage.cmd 1.0.0
```

只上传：

```text
Builds/CdnUpload/1.0.0/Upload/
```

目录同时包含签名清单、完整首装 ZIP 分片和按 SHA-256 命名的文件级增量对象/分片。
每个实际上传文件最多 95,000,000 字节（约 90.60 MiB，低于十进制 100 MB）。
空 `Game` 下载并重组完整 ZIP；已有可信安装只下载目标清单中新增/变化对象的
分片。安装在同盘 staging 中完成，签名、长度和 SHA-256 全部通过后才切换
`Game`；失败保留旧版。
这是 Player 文件更新，不改变 D-051 的本地 Addressables。

UOS CLI 操作顺序是 `auth login`、`entries sync`、`releases create`、把 `Test`
Badge 指向新 Release、真实验收、再把 `Prod` 指向同一 Release。上传同步不要使用
`-d`，以保留历史内容对象和回退材料。完整命令、Launcher Bucket 配置、私钥纪律、
首装/增量/回滚与故障恢复见
`Docs/Implementation/GAME_LAUNCHER_GUIDE.md`。

UOS 官方入口：

- 概念/示例：`https://uos.unity.cn/docs/cdn/concept.html`、
  `https://uos.unity.cn/docs/cdn/tutorial.html`
- Package/CLI：`https://uos.unity.cn/docs/cdn/package.html`、
  `https://uos.unity.cn/docs/cdn/cli.html`
- 地址、Badge、缓存与下载文件名：
  `https://uos.unity.cn/docs/cdn/qa.html`

### 3.6 若要把 Addressables 本体迁到 UOS CDN

当前 `Client-*` 组全部使用 `Local.BuildPath / Local.LoadPath`，客户端构建会把
catalog 和 Bundle 放入 `StreamingAssets/aa`。如果目标是缩小首包、运行时按需
下载英雄/VFX等资源，需要另建一轮受控迁移，不能只把现有 Player ZIP 上传后
修改一个网址。

最小迁移步骤如下：

1. 新建 `UOS-Remote` Addressables Profile：
   `Remote.BuildPath = ServerData/[BuildTarget]`，
   `Remote.LoadPath` 使用 UOS Release/Badge 的路径模式前缀。
2. 仅把计划远端化的 `Client-*` 组切到 Remote Build/Load Path；保留启动页、
   错误页和最低可运行内容在 Local。按 Windows/Android/iOS 分 Bucket。
3. 开启远端 catalog 与 AssetBundle 缓存，构建 Addressables，将
   `ServerData/[BuildTarget]` 下 catalog/hash/bundle 的相对目录完整上传，再创建
   Release 和 Badge。保存 Addressables content-state 文件，后续用 Content
   Update 流程生成增量内容。
4. 调整 `LocalNgoBuildMenu` / `DedicatedServerPresentationBuildPipeline` 的客户端
   构建审计：当前审计要求本地 `StreamingAssets/aa` 中存在完整 Windows Bundle；
   远端模式应改为校验远端 catalog、平台、依赖闭包和必留本地组，而不是把
   “未嵌入远端 Bundle”误判为失败。
5. `AddressablesClientContentService`、`VfxManager`、单位/投射物 Binder 已经按
   Addressables 地址异步取得并持有 lease，正确 Profile/catalog 生效后通常不需
   为 URL 再写一套下载代码。但加载页还应补下载大小/进度、超时重试、磁盘空间、
   断网提示、catalog/Gameplay 版本兼容和失败回退策略。

UOS 给 Addressables 的 Badge 前缀格式为：

```text
https://a.unity.cn/client_api/v1/buckets/{BucketId}/release_by_badge/{BadgeName}/content/
```

正式发布建议用可人工回退的 `Prod` Badge，不直接依赖自动前移的 `latest`。
具体配置见 UOS 官方 Addressables 文档：
`https://uos.unity.cn/docs/cdn/addressables.html`。

### 3.7 构建正式 Demo 启动器和空 Game 首包

正式启动器与 `Tools/UosClientLauncher` 开发者工具分开维护。玩家包使用下面的
开发目录结构：

```text
Builds/Demo/Launcher/FrameSyncMobaLauncher.exe
Builds/Demo/Game/AAALOL.exe
Builds/Demo/Game/AAALOL_Data/
```

在 Windows 开发机执行：

```powershell
Tools/UosGameLauncher/BuildLauncher.cmd
```

这会把 self-contained .NET 8 WinForms 启动器发布到
`Builds/Demo/Launcher`。Launcher 不要求玩家预装 .NET Desktop Runtime。配置好
UOS Bucket/Badge 后，玩家只填写登录名；启动器先完成 CDN 安装/更新，再向
`AAALOL.exe` 传入 `-onlineFlow` 和 `--TestAccountId=<登录名>`。匹配、登录、
游戏内日志和本地 Addressables 仍由客户端负责。

不要为了制作玩家首包删除开发机的 `Builds/Demo/Game`。执行：

```powershell
Tools/UosGameLauncher/BuildBootstrapPackage.cmd
```

会生成只包含 `Demo/Launcher` 与空 `Demo/Game` 的
`Builds/Bootstrap/FrameSyncMobaDemo-Bootstrap-1.2.0.zip`。

四个美术文件仍为 `Background.png`、`Banner.png`、`Logo.png` 和多尺寸
`AppIcon.ico`。CDN 路由位于 `launcher.cdn.json`，签名信任根位于
Launcher EXE 内嵌公钥；旁边的 `CdnSigningPublicKey.pem` 仅用于发布审计。两者
都不在美术目录。完整说明见
`GAME_LAUNCHER_GUIDE.md`。

## 4. 本地 C/S 启动（测试）

完整测试流程、检查清单与日志解读见 `Docs/Implementation/C_S_TEST_GUIDE.md`。
以下为启动命令速查。

三个进程**都必须带 `-logFile`**，日志写到 `Builds/LocalNgo/Logs/` 下。不要使用默认的 LocalLow
共享 `Player.log`（否则两个客户端混写同一个文件、服务端日志直接输出到控制台而丢失）。
运行编号沿用历史约定 `cs<N>_<yyyyMMdd>`，`<N>` 为当天第几次测试。

```powershell
# 服务端（先启动，不带 slot 参数）
Start-Process -FilePath "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Server\FrameSyncMobaServer.exe" `
  -WorkingDirectory "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Server" `
  -ArgumentList "-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_server.log"

# Client 0（slot 0）
Start-Process -FilePath "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Client\FrameSyncMobaClient.exe" `
  -WorkingDirectory "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Client" `
  -ArgumentList "--LocalPlayerSlot=0","-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_client0.log"

# Client 1（slot 1）
Start-Process -FilePath "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Client\FrameSyncMobaClient.exe" `
  -WorkingDirectory "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo\Client" `
  -ArgumentList "--LocalPlayerSlot=1","-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_client1.log"
```

要点：

- 客户端**必须**带 `--LocalPlayerSlot=0/1`，否则两个客户端都按 slot 0 处理，匹配页会卡住。
- 服务端直接启动即可，不需要 slot 参数。
- 日志命名示例：`cs23_20260810_client0.log`。测试结束后先停进程，再按文件读对应日志。
- 启动命令只发送一次，随后不要轮询、不要重复触发，等用户测试完再处理。

## 5. 操作纪律（必须遵守）

1. **打包命令只能发送一次**，发送后立即停止操作，不要再运行任何命令、不要轮询、不要重复触发。
2. 等待用户通知“打包结束”后再继续；否则容易造成重复构建。
3. 代码/资产层面的改动必须在打包前完成；打包过程中不要向 Unity 发送其它指令。

## 6. 防重复构建

- `RunExclusive`：若 `isBuilding` 或 `BuildPipeline.isBuildingPlayer` 为真，忽略新请求。
- `BuildBoth` 的 120 秒守卫：上一次构建完成后的 120 秒内重复调用会被忽略
  （用于吞掉 MCP 桥接重试产生的重复调用）。
- 若确实需要立刻重新打包，先执行 `Clear Build-Both Retry Guard` 清掉守卫再打包。

### 3.2.1 批处理模式打包（推荐：快、省 CPU/内存）

当本机 CPU/内存紧张（例如同时运行 Unity 编辑器与 Codex）时，推荐先关闭 Unity 编辑器，再用 headless 批处理模式打包（无窗口/渲染开销，并释放编辑器占用的资源）：

`	ext
Builds\build_both.bat
`

脚本内部执行：

`	ext
Unity.exe -batchmode -quit -projectPath <项目路径> -executeMethod FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildBoth -logFile Builds\LocalNgo\Logs\build_both_batch.log
`

要点：
- 必须先关闭当前项目的 Unity 编辑器（同一项目 Library 被占用，批处理与编辑器不能同时打开同一项目）；
- 关闭前保存未保存的场景/预制体修改；
- 打包完成后重新打开编辑器（Codex 会自动重连 MCP）；
- exit code 0 = 成功；非 0 时查看 -logFile 末尾的 BuildReport。

## 7. UOS 服务端自动压缩

`BuildServerLinux()` 仅在 Linux Dedicated Server 的 `BuildReport` 成功后，
自动生成：

```text
Builds/UosUpload/FrameSyncMobaServer_uos_<yyyyMMdd-HHmmss>.zip
Builds/UosUpload/FrameSyncMobaServer_uos_<yyyyMMdd-HHmmss>.zip.sha256
```

ZIP 根目录直接包含 `FrameSyncMobaServer.x86_64`、`UnityPlayer.so` 和
`FrameSyncMobaServer_Data/`，不会额外嵌套 `UosServer/`。Unity 生成的
`*_BurstDebugInformation_DoNotShip` 调试目录不会进入上传包。

压缩使用临时文件并在成功后改名；压缩或完整性检查失败时不会留下可误上传
的 ZIP。已有上传包不会覆盖，同一秒重复压缩会追加 `-01`、`-02` 等后缀。

只压缩当前 `Builds/UosServer`、不重新构建也不上传时，可以双击：

```text
Tools/PackageLatestUosServer.cmd
```
