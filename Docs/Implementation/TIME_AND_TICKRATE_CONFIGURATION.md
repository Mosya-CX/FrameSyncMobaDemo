# 时间配置与 TickRate 使用说明

> Document class: Operational Configuration Guide
> Default read: only when TickRate or authored Gameplay timing is in scope

> 当前契约：D-045，2026-08-20。

## 1. 配置边界

- 离线 `TickRate` 支持 `10..120`，且必须是 5 的倍数。
- 正式默认仍为 30 Tick/s；推荐回归档位为 20、30、60。
- Inspector 中表示现实时间的 Gameplay 内容统一填写整数毫秒，例如前摇
  `450 ms`、控制 `1500 ms`、冷却 `8000 ms`。
- Gameplay 运行态、Snapshot、Checksum、Command 和 AuthorityFrame 仍只保存
  `Tick`，不把毫秒带入确定性状态。
- 网络等待、加载进度、Ping 采样和 Unity 帧调度使用整数毫秒与单调时钟。
  本机 UTC 只允许出现在日志时间戳、日志文件名和构建产物命名中。

## 2. Bake 规则

统一转换公式为：

```text
numerator = DurationMilliseconds * TickRate
Ceil    = (numerator + 999) / 1000
Nearest = (numerator + 500) / 1000
Floor   = numerator / 1000
```

转换使用受检整数运算。正持续时间默认使用 `Ceil`，保证效果不会比配置更短；
周期近似可显式选择 `Nearest`，只有确需提前边界时才使用 `Floor`。

示例：

| 配置 | 20 Tick/s | 30 Tick/s | 60 Tick/s |
|---:|---:|---:|---:|
| 450 ms，Ceil | 9 Tick | 14 Tick | 27 Tick |
| 1000 ms，Ceil | 20 Tick | 30 Tick | 60 Tick |
| 1500 ms，Ceil | 30 Tick | 45 Tick | 90 Tick |

## 3. 启动与本机时间

启动顺序仍为：

```text
SceneLoaded
→ 服务端广播 Bootstrap
→ 客户端 Restore / Resolve / Rebuild 并完成本地绑定
→ BootstrapApplied
→ 服务端等待全部客户端确认
→ LaunchCommit
→ 各端开始 Tick
```

`LaunchCommit` 传输 `LaunchServerTimeMilliseconds`，其时间域来自 NGO
同步服务器时间。客户端不比较 `DateTime.UtcNow`，到达启动阈值后以本地单调时钟
建立节拍锚点。晚到客户端只能依据连续收到的 AuthorityFrame 积压受控追赶，
不能根据时间戳差值凭空推导几十秒 Gameplay 积压。

协议版本：

- `MatchLaunchWireCodec`：v2；
- `BootstrapPayloadWireCodec`：v3；
- `GameplayDataVersion`：3。

不同版本客户端/服务端会在入口被明确拒绝，不能混连。

## 4. 内容迁移范围

当前已迁移全局流程、技能与 Stage、Buff、装备、投射物、单位生命周期、兵线、
野区、AI/寻路周期以及相关表现时长。旧 30 Hz 直接 Tick 配置、原值、等价时间和
迁移毫秒见：

- `Docs/Implementation/LEGACY_30HZ_TIME_AUTHORING_INVENTORY.md`

以下内容必须继续使用 Tick：Command 目标帧/合法窗口、预测提前量、Snapshot
历史容量、AuthorityFrame 恢复帧间隔、`CurrentTick`、`StartTick`、
`RemainingTicks`、`SnapshotTick` 等离散模拟状态。

## 5. 验收要求

每次调整 `TickRate` 后至少验证：

1. 同一毫秒配置在 20/30/60 档 Bake 成预期 Tick；
2. 同一初始状态和 Command 序列重复执行结果一致；
3. 连续执行与 Snapshot/Restore/Replay 结果一致；
4. 启动、加载和 Ping 不受操作系统时钟前后跳变影响；
5. 客户端追赶始终受连续 AuthorityFrame 与每帧最大执行 Tick 数限制。
