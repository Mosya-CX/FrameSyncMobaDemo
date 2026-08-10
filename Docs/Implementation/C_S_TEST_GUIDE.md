# 本地 C/S 测试指南（Local NGO）

本文档描述本地 C/S 测试的完整流程：测试目的、打包入口、启动方式、日志信息与常见问题排查。
资源布局与打包纪律分别见 `Docs/Architecture/REPOSITORY_MAP.md` 与
`Docs/Implementation/BUILD_GUIDE.md`。

## 1. 测试目的

本地 C/S 测试验证**完整对局链路**在确定性帧同步下的正确性：

- 两个客户端 + 一个 Dedicated Server 走通 主菜单 -> 匹配 -> 选英雄 -> Loading -> GameScene；
- 双端在同一 `StartTick` 开始，客户端预测 + 权威帧回滚重演；
- 心跳/延迟指标：本地 Ping（UTP RTT）、模拟落后 Tick（`local - sync`）；
- 战斗内容：移动、普攻、Q/W/E/R、Buff、击杀/死亡/复活、计分板、补刀、金币/商店（当前商店为空目录）、技能点升级；
- 长时间稳定性：不应出现 `DeterministicSimulationException` / 校验码不一致 / 单端刷错卡死。

## 2. 打包入口

所有打包逻辑集中在 `Assets/Editor/LocalNgoBuildMenu.cs`（静态类
`FrameSyncMoba.EditorTools.LocalNgoBuildMenu`）。

| 方法 | 输出 |
|---|---|
| `BuildBoth()` | `Builds/LocalNgo/Server/FrameSyncMobaServer.exe` + `Builds/LocalNgo/Client/FrameSyncMobaClient.exe` |
| `BuildServer()` | 仅服务端 |
| `BuildClient()` | 仅客户端 |

### 2.1 手动打包

编辑器菜单 `FrameSyncMoba/Build Local NGO/Build Both`。

### 2.2 通过 Unity MCP 一次性调用

```text
FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildBoth();
```

**纪律**：打包命令只发送一次，发送后立即停止操作（不轮询、不重复触发、不再向 Unity
发送其它指令），等待用户通知“打包结束”。`BuildBoth` 有 120 秒防重守卫，需要立刻重打时先执行
`Clear Build-Both Retry Guard`。

### 2.3 exe 时间戳说明

构建会刷新 `Builds/LocalNgo/` 下的 `_Data` 等内容；exe 文件本身的时间戳可能保持不变，
这是已知现象，不代表没有重新打包。

## 3. 启动方式

三个进程**都必须带 `-logFile`** 指向 `Builds/LocalNgo/Logs/`。运行编号沿用
`cs<N>_<yyyyMMdd>`（`<N>` 为当天第几次测试，如 `cs23_20260810`）。

```powershell
$run = "cs<N>_<yyyyMMdd>"
$base = "E:\Unity\Item\FrameSyncMobaDemo\Builds\LocalNgo"

# 服务端（先启动，不带 slot 参数）
Start-Process -FilePath "$base\Server\FrameSyncMobaServer.exe" `
  -WorkingDirectory "$base\Server" `
  -ArgumentList "-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/${run}_server.log"

# Client 0（slot 0）
Start-Process -FilePath "$base\Client\FrameSyncMobaClient.exe" `
  -WorkingDirectory "$base\Client" `
  -ArgumentList "--LocalPlayerSlot=0","-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/${run}_client0.log"

# Client 1（slot 1）
Start-Process -FilePath "$base\Client\FrameSyncMobaClient.exe" `
  -WorkingDirectory "$base\Client" `
  -ArgumentList "--LocalPlayerSlot=1","-logFile","E:/Unity/Item/FrameSyncMobaDemo/Builds/LocalNgo/Logs/${run}_client1.log"
```

要点：

- 客户端**必须**带 `--LocalPlayerSlot=0/1`；否则两个客户端都按 slot 0 处理，匹配页会卡住。
- 服务端直接启动即可，不需要 slot 参数。
- 测试结束后先停进程（`Get-Process | Where-Object { $_.ProcessName -like '*FrameSyncMoba*' } | Stop-Process -Force`），再读对应日志。

## 4. 测试流程

1. 启动服务端，等待监听（日志出现 `[LocalNGO] Server listening on ...`）。
2. 依次启动 Client 0 / Client 1。
3. 两端主菜单 -> 匹配 -> 选英雄（当前为韦鲁斯，选人阶段同一队伍允许重复）-> Ready。
4. 双端进入 Loading，等待同一 `StartTick` 后进入 GameScene（客户端用真实时间倒计时启动）。
5. 对局内检查清单：
   - 摄像机跟随本地英雄（Y 锁定英雄，鼠标靠屏边缘平移，仅 C/S 生效的侧向俯视角）；
   - 右键移动；右键敌方单位攻击；攻击有动画（Attack1/Attack2 交替）与投掷物；
   - Q 蓄力（按下进蓄力、左键施放；**蓄力期间右键仍可移动**）、W 开关、E 污染区域、R 蔓延；
   - HUD：生命/法力、属性栏（数值格式）、技能图标/冷却、技能点升级按钮、Buff 栏（含层数）、
     Ping 值、计分板 KDA/补刀、队伍比分；
   - 击杀/死亡/复活（复活点重生）、助攻结算、被动 P 触发；
   - 商店（当前装备目录为空，页面可打开、无商品）；
   - 长时间运行无校验码报错、无单端卡死。

## 5. 日志信息

### 5.1 日志位置

| 进程 | 日志文件 |
|---|---|
| 服务端 | `Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_server.log` |
| Client 0 | `Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_client0.log` |
| Client 1 | `Builds/LocalNgo/Logs/cs<N>_<yyyyMMdd>_client1.log` |

如果没有带 `-logFile`：客户端会写默认共享日志
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\FraneSyncMobaDemo\Player.log`
（两个客户端混写一个文件），服务端日志只输出到控制台、进程结束后丢失。

### 5.2 关键日志标记

| 标记 | 含义 |
|---|---|
| `[Checksum] Client local Tick N checksum=... commands=0` | 启动期本地校验基线（正常） |
| `[Rollback] tick=N anchor=... replayEnd=...` / `[Rollback] replay done ... match=True` | 预测回滚重演，`match=True` 正常 |
| `[Checksum] Tick N local=... expected(server)=... actual(client)=...` | **校验码不一致**（Debug.LogError），客户端会反复重演卡死 |
| `[CmdSend] local=N sync=M` | 指令发送；`local - sync` = 模拟落后 Tick（30 Tick/s 下 1 Tick ≈ 33ms） |
| `[Scoreboard] rank=... breakdown=[uid:k/d/a/c]` | 计分板，已改为**仅内容变化时**打印 |
| `[VfxManager] VFX N: prefab not found` / `[AudioManager] SFX N: clip not found` | VFX/音频绑定缺失（表现层警告） |
| `[HudBuffs]` / `[Indicator]` / `[AttackSfx]` | 表现层诊断 |
| 分配器 dump（`Failed Allocations` / `##utp:MemoryLeaks`） | 进程退出时的内存报告，`Failed Allocations` 是分配器桶重试计数，不代表系统内存不足 |

### 5.3 历史问题与已修复项（排查参考）

- `DeterministicSimulationException: Authority replay checksum mismatch ...`：
  回滚重演分叉。已修复小兵仇恨刷新 Tick 未进快照、回滚恢复时静态事件订阅泄漏。
- `KeyNotFoundException: 'HealingReceivedRatio'`：`StatHandler.Restore` 未为快照里运行期
  新增属性重建 config，已修复（回滚重建后按 definitionTable 懒建）。
- `Projectile hit memory references missing UnitUid ...`：弹体命中记忆引用已销毁目标，
  快照捕获时剪枝，已修复。
- `Transition 'AnyState -> INVALID' in state 'AnyState'`：控制器 AnyState 引用了无效目标，
  TestHero 的坏过渡已移除。
- 每帧 `[Scoreboard]` 刷屏：已改为仅变化时打印（同时缓解 Ping 虚高与 CPU 高）。

## 6. 常见问题排查

| 现象 | 排查方向 |
|---|---|
| 两个客户端卡在匹配页 | 检查启动参数是否分别带 `--LocalPlayerSlot=0/1` |
| 一个客户端大量报错卡死 | 读对应 client 日志，找第一条 Exception / `[Checksum]` 不一致点 |
| 服务端/客户端日志没落盘 | 启动时漏带 `-logFile` |
| 本地 Ping 200-300ms | 传输 RTT 受帧卡顿与日志刷屏影响；先确认无每帧刷屏日志 |
| 英雄攻击动画不显示 | `TestHero.controller` 需含 Attack1/Attack2 状态（AnyState `AttackStart` 进入） |
| exe 时间戳未变 | 已知正常，见 2.3 |

## 7. 资源布局（正式 / 测试）

- `Assets/Config/Formal/`：唯一运行时链，即打包内容（GlobalGameplayData ->
  GlobalPrefabTable -> 目录/预制体/动画/流场/VFX/地图等），C/S 测试与此一致。
- `Assets/Config/Tests/`：测试专用配置（如 HeroTestMapConfig）。
- `Assets/Resources/`：C/S 实际使用的 UI / 弹道 / 指示器 / VFX / 材质 / 数据。
- 英雄即韦鲁斯：`Formal/Prefabs/TestHeroRuntime.prefab` + `Formal/Abilities/Varus*`。
