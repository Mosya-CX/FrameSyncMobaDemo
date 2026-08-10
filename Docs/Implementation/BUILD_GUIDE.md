# 打包 / 构建指南（Local NGO C/S 与 UOS）

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
| `ClearBuildBothRetryGuard()` | - | - | 清除 120 秒防重守卫 |

本地 C/S 测试包 = `BuildBoth()`：先 Server 后 Client。

## 3. 调用方式

### 3.1 Unity 菜单手动打包

编辑器内点击 `FrameSyncMoba/Build Local NGO/Build Both`。

### 3.2 通过 Unity MCP 一次性调用

使用 `script-execute`（body-only）执行：

```text
FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildBoth();
```

### 3.3 UOS 服务器镜像

构建 `Build Server Linux (UOS)`，将 `Builds/UosServer/` 目录打成 zip 上传到 UOS 控制台。

2026-08-10 已真实通过 UOS Ready 和两客户端对局的镜像配置如下：

```text
需要增加执行权限的文件：FrameSyncMobaServer.x86_64
入口程序启动命令：./FrameSyncMobaServer.x86_64 -batchmode -nographics
协议和端口：UDP 7777
挂载文件：无
自定义环境变量：无（UOS 注入 allocation/match/Agones 运行变量）
```

资源规格的已知证据：`1 CPU / 1536 MB` 可在约 10 秒内进入 Ready；
更小的 CPU/内存联动档位曾在 Ready 超时前失败。相同镜像提高规格后成功，
因此小规格失败不能直接归因于 Ready SDK 调用错误。当前 Linux 包启动资产较大
（审计时 `resources.assets` 约 330 MB），后续若要降低规格，应先测量启动峰值
内存和 Ready 时间，而不是删除 Ready 调用或盲目延长超时。

UOS 标识不能混用：

```text
Multiverse 启动配置/Profile ID：0fc730a2-ce02-4768-8a75-713ddb36c3b0
Matchmaking config ID：f01c4e66-0023-43f6-af57-dcd8b73e7b90
```

运行时 `MatchmakingConfigID` 必须使用第二项；第一项填入后会产生
`The config [...] is not found`。不要把应用密钥、服务器密钥、allocation
UUID、room ID 或 SDK 端口抄入文档或自定义环境变量。UOS 控制台/日志中出现
秘密值时必须先脱敏。

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
