# 正式 Demo 游戏启动器与 UOS CDN 更新

> Document class: Operational Guide
> Source: `Tools/UosGameLauncher`
> Published launcher: `Builds/Demo/Launcher`
> CDN package output: `Builds/CdnUpload/<ClientVersion>/Upload`

## 作用范围

这是面向玩家的单客户端启动器，与开发者工具
`Tools/UosClientLauncher` 完全分开。正式 Launcher 负责：

1. 显示固定的四个启动器美术资源；
2. 读取一个登录名；
3. 从 UOS CDN 检查签名客户端清单；
4. 在本地 `Game` 为空时下载完整客户端 ZIP；
5. 已有旧版本时只下载目标清单中新增或 SHA-256 变化的文件；
6. 在独立 staging 中校验并原子切换完整 `Game` 目录；
7. 启动或关闭 `AAALOL.exe`。

启动器不实现 UOS 玩家登录、匹配逻辑、支付、公告或 Gameplay 协议。客户端仍
负责自己的初始化、登录/匹配、游戏日志和本地 Addressables 加载。D-051 的
Addressables catalog/Bundle 仍随 Player 放在本地；本功能是 Player 文件分发，
不是 Remote Addressables 热更新。

## 玩家首包目录

玩家拿到的首包只需要：

```text
Demo/
├─ Launcher/
│  ├─ FrameSyncMobaLauncher.exe
│  ├─ launcher.cdn.json
│  ├─ CdnSigningPublicKey.pem
│  └─ Assets/Launcher/
└─ Game/                         # 可以为空或不存在
```

首次运行时，Launcher 在同级创建并安装完整 `Game`：

```text
Demo/Game/AAALOL.exe
Demo/Game/AAALOL_Data/
Demo/Game/UnityPlayer.dll
```

不要手工清空开发机现有的 `Builds/Demo/Game`。执行下面的首包命令会生成只含
Launcher 与空 `Game` 的 ZIP，不会修改当前客户端：

```powershell
Tools/UosGameLauncher/BuildBootstrapPackage.cmd
```

输出：

```text
Builds/Bootstrap/FrameSyncMobaDemo-Bootstrap-1.3.1.zip
```

Launcher 始终只显示一个主操作按钮，并按状态切换：

```text
未安装             -> 下载游戏
已安装但不是当前版本 -> 更新
已是当前版本        -> 开始游戏
游戏运行中          -> 关闭游戏
```

“下载游戏”和“更新”只完成对应的数据操作，成功后按钮切换为“开始游戏”，不会
自动启动客户端。点击“开始游戏”时 Launcher 会重新获取并验证签名清单；如果此时
发现本体缺失或出现新版本，按钮分别退回“下载游戏”或“更新”，等待玩家再次点击。
下载/更新进行中时，同一个按钮临时切换为“取消下载”或“取消更新”。

## 发布信任与私钥

第一次使用时执行一次：

```powershell
Tools/UosGameLauncher/GenerateCdnSigningKey.cmd
```

生成：

```text
Builds/CdnSigning/FrameSyncMobaCdnPrivateKey.pem       # 私钥，秘密
Tools/UosGameLauncher/CdnSigningPublicKey.pem          # 发布输入；构建时嵌入 Launcher
```

`Builds/` 已被 Git 忽略。私钥不能上传 UOS、不能提交 Git、不能放进首包；必须另行
离线备份。丢失私钥后，已经发出的 Launcher 不会接受新签名；私钥泄漏后必须发布
内置新公钥的新 Launcher。运行时信任根来自 EXE 内嵌公钥，不信任 Launcher 旁边
可被单独替换的 PEM；旁边的 PEM 只保留作发布审计材料。首包生成器采用固定文件
白名单，并在发现额外文件、私钥扩展名或 PEM 私钥内容时直接失败。

此设计防止只篡改 CDN 配置、远端文件或外置 PEM 的攻击，但不声称防住能够同时
改写 `FrameSyncMobaLauncher.exe` 的本地管理员。正式对公网分发时仍应给 EXE/安装
包配置 Authenticode 或通过可信安装渠道发布。

清单使用 RSA-SHA256 detached signature：

```text
client-manifest.json
client-manifest.sig
```

schema-v3 清单列出目标客户端的每个相对路径、长度、SHA-256 和有序内容分片。
下载的完整 ZIP、增量文件和最终 staging 文件都必须通过清单校验才会替换旧客户端。

## 生成 CDN 客户端包

先确保 `Builds/Demo/Game` 是经过验收、入口名为 `AAALOL.exe` 的完整客户端，再
执行：

```powershell
Tools/UosGameLauncher/BuildCdnPackage.cmd 1.0.0
```

版本必须是数字版本号。输出：

```text
Builds/CdnUpload/1.0.0/
├─ package-report.json
└─ Upload/
   ├─ client-manifest.json
   ├─ client-manifest.sig
   └─ content/<sha256>                     # 所有实际上传的数据
```

`Upload` 的**内容**是唯一需要同步到 UOS Bucket 根目录的数据。完整 ZIP 仍用于
空 `Game` 首装和增量校验失败后的修复，但它只在本地由 `content` 中的七个分片
重组，不是远端实体路径。小文件、大文件分片和完整 ZIP 分片统一存放在
`content/<sha256>`；清单保留聚合文件的总长度/SHA-256，并按顺序列出分片，
启动器下载后先重组并校验聚合 SHA-256，再安装。内容按 SHA-256 命名，因此跨文件
和跨 Release 的未变化数据可以复用。打包器保证每个实际上传文件不超过 95,000,000 字节
（约 90.60 MiB，低于十进制 100 MB）。

使用 UOS 网页控制台上传时，先回到 Bucket 根目录，不要选择最外层 `Upload`
文件夹。分别添加根目录的 `client-manifest.json`、`client-manifest.sig`，再添加
`content` 文件夹并点击“全部上传”。完成后的 Bucket 根目录也应只有这三项；
Launcher 的 `manifestPath` 保持 `client-manifest.json`，不会产生 `Upload/Upload`
路径。必须等待全部文件成功后再创建 Release。

打包器会自动排除 `.pdb` 和 `*_BurstDebugInformation_DoNotShip`；两者是开发符号/
Burst 调试产物，不是 Unity Player 运行依赖，也不应进入玩家 CDN 包。排除数量和
字节数写入 `package-report.json`。

打包器会拒绝不完整的 Player、危险输出路径、未标记的既有输出目录和不安全相对
路径；成功前会复核签名、每个对象、完整 ZIP 及 ZIP 内每个文件。

重新审计某个上传目录：

```powershell
dotnet run --project Tools/UosGameLauncher/FrameSyncMoba.GameLauncher.csproj `
  -c Release -- --audit-cdn-package `
  --input Builds/CdnUpload/1.0.0/Upload
```

## 上传到 UOS CDN

UOS 密钥只用于开发机 CLI 登录，不进入 Launcher 配置。以下命令里的值由你从
UOS 控制台取得：

```powershell
uas auth login --uos_app_id <AppId> --uos_app_service_secret <AppServiceSecret>
uas buckets list
uas config set bucket <BucketId>
```

同步打包器生成的 `Upload` 目录。不要加 `-d`，这样历史内容对象和旧 Release 的
回退材料不会从 Current Entries 主动删除：

```powershell
uas entries sync "E:\Unity\Item\FrameSyncMobaDemo\Builds\CdnUpload\1.0.0\Upload" `
  --bucket <BucketId>
```

创建不可变 Release：

```powershell
uas releases create --notes "AAALOL Windows 1.0.0" `
  --bucket <BucketId> --interactive=false
uas releases list --bucket <BucketId>
```

从输出取得新的 `<ReleaseId>`，先分配测试 Badge：

```powershell
uas badges add Test <ReleaseId> --bucket <BucketId>
```

完成真实首装/增量验收后再切正式 Badge：

```powershell
uas badges add Prod <ReleaseId> --bucket <BucketId>
```

发现问题时，把 `Prod` 指回旧 Release 即可回滚。Badge 切换存在短暂 CDN 缓存；
Launcher 默认重试三次，两次退避合计约 60 秒，以覆盖清单、签名或对象短暂跨代。
仍建议 Badge 切换后等待缓存传播完成并用 `Test` 做首装/增量验收，再切 `Prod`；
仍不一致时只允许保留并启动通过签名和全文件哈希复核的旧客户端。

## 配置 Launcher 使用 Bucket

上传完成后，编辑：

```text
Tools/UosGameLauncher/launcher.cdn.json
```

填入公开的 Bucket ID 和 Badge；这里没有秘密：

```json
{
  "enabled": true,
  "bucketId": "你的BucketId",
  "badgeName": "Test",
  "manifestPath": "client-manifest.json",
  "manifestSignaturePath": "client-manifest.sig",
  "manifestUrlOverride": "",
  "maxAttempts": 3
}
```

然后重新生成正式 Launcher 和玩家首包：

```powershell
Tools/UosGameLauncher/BuildBootstrapPackage.cmd
```

测试通过后把 `badgeName` 改为 `Prod`，再次生成正式首包。也可以只修改已发布
`Builds/Demo/Launcher/launcher.cdn.json`，但源码配置必须同步保存，避免下一次
发布退回旧值。

Launcher 使用的 UOS URL 形式为：

```text
https://a.unity.cn/client_api/v1/buckets/{BucketId}/release_by_badge/{Badge}/content/client-manifest.json
```

## 首装、增量和恢复行为

- 本地没有可信安装清单：下载并验证完整 ZIP。
- 本地有旧的可信安装清单：复用哈希未变文件，只下载变化对象。
- 远端清单字节与已安装签名清单完全一致，且本地每个文件 SHA-256 全部通过：
  才直接启动；版本号相同但清单内容变化仍会更新。
- 增量对象下载或 staging 完整性校验失败：自动回退到完整 ZIP 修复，不覆盖旧
  `Game`。
- HTTP 连接中断：保留 `.part`，UOS 返回 206 时断点续传；服务端忽略 Range 并
  返回 200 时自动从头覆盖临时文件。
- 清单签名、文件长度或 SHA-256 不匹配：拒绝安装。
- 更新过程中取消、断网或磁盘空间不足：删除 staging，保留旧版本。
- 崩溃发生在目录切换中间：下次启动对 `Game` 与 `Game.__backup` 都做签名和全文件
  SHA-256 复核；只有一个可信时保留它，两者都不可信时保留现场并停止破坏性恢复。
- 客户端正在运行：拒绝更新。外部启动的同路径 `AAALOL.exe` 也会被检测。
- 更新检查失败但旧客户端的签名清单和全文件 SHA-256 都有效：玩家可选择继续启动
  旧版本；手工拷入、坏签名、缺文件或同长度损坏均不能离线放行。

当前是文件级增量，不是二进制块差分。一个 419 MiB AssetBundle 内任意内容变化，
仍会重新下载该完整 Bundle。进一步缩小内容更新需要单独审批并实施 Remote
Addressables/Bundle 拆分；当前 Launcher 更新不得改动 D-051。

## 构建和自测

正式 Launcher 为 self-contained .NET 8 Windows x64 单文件程序，不要求玩家预装
.NET Desktop Runtime：

```powershell
Tools/UosGameLauncher/BuildLauncher.cmd
Builds/Demo/Launcher/FrameSyncMobaLauncher.exe --self-test
```

`--self-test` 不启动真实游戏。它在临时目录和本机 loopback HTTP 服务中验证：

- 参数、登录名、固定路径和短进程生命周期；
- 签名、路径穿越拒绝和包审计；
- 空 `Game` 完整安装；
- Range 断点续传及只下载一个变化文件；
- 目标清单删除旧文件；
- 同版本同长度内容替换、本地同长度损坏及完整 ZIP 修复；
- 坏签名拒绝、坏增量对象自动降级完整 ZIP；
- 切换中断、无效新目录、双无效候选保留现场；
- 首包额外/私钥文件拒绝。

## 启动参数和美术资源

启动器仍只向客户端传递：

```text
-onlineFlow --TestAccountId=<登录名>
```

登录名保存在：

```text
%LocalAppData%/FrameSyncMobaDemo/GameLauncher/launcher.settings.json
```

下载缓存位于：

```text
%LocalAppData%/FrameSyncMobaDemo/GameLauncher/Cache
```

固定美术仍是 `Background.png`、`Banner.png`、`Logo.png` 和多尺寸
`AppIcon.ico`；CDN 公钥和配置不属于美术目录。
