# 旧 30 Hz Gameplay 时间配置清单

> Document class: Historical Migration Reference
> Default read: only when auditing D-045 time-authoring migration

> 审计基线：2026-08-20，迁移资产之前。旧项目固定 `TickRate = 30`，因此精确现实时间为 `Tick / 30` 秒。迁移到整数毫秒时，为确保重新 Bake 到 30 Hz 后 Tick 数不增加，正 Tick 使用 `floor(Tick × 1000 / 30)` 毫秒；运行时仍使用整数 Tick。

## 分类原则

- “内容时间”表示前摇、持续、冷却、周期、窗口、延迟和生命周期，必须迁移成 Inspector 整数毫秒。
- “协议/模拟 Tick”表示预测提前量、快照窗口、命令目标 Tick、Authority 恢复周期等，它们描述离散帧关系，不是现实时间内容，必须继续配置为 Tick。
- `resourcePerTick`、`m_ScrollDeltaPerTick` 等是每 Tick 数值或 UI 输入步长，不是时间字段，不参与时间迁移。

## 技能内容

| 资产 | 旧字段和值 | 旧 30 Hz 精确时间 | 迁移毫秒 |
|---|---:|---:|---:|
| `AatroxE.asset` | `durationTicks = 15` | 0.5 s | 500 ms |
| `AatroxQ.asset` | 三段 `ImpactDurationTicks = 30` | 1 s | 1000 ms |
| `AatroxQ.asset` | 两段 `RecastWindowDurationTicks = 120` | 4 s | 4000 ms |
| `AatroxQ.asset` | 两段 `MinimumRecastDelayTicks = 30` | 1 s | 1000 ms |
| `AatroxQ.asset` | 三段 `sweetSpotControlDurationTicks = 8` | 0.266666… s | 266 ms |
| `AatroxQ.asset` | 三段 `fixedPassiveHitReductionTicks = 60` | 2 s | 2000 ms |
| `AatroxQ.asset` | 三段 `fixedPassiveSweetHitReductionTicks = 120` | 4 s | 4000 ms |
| `AatroxQ.asset` | 三段 `impactDelayTicks = 30` | 1 s | 1000 ms |
| `AatroxR.asset` | `durationTicks = 6` | 0.2 s | 200 ms |
| `AatroxR.asset` | `controlDurationTicks = 45` | 1.5 s | 1500 ms |
| `AatroxR.asset` | `applyDelayTicks = 6` | 0.2 s | 200 ms |
| `AatroxW.asset` | `durationTicks = 14` | 0.466666… s | 466 ms |
| `AatroxW.asset` | `spawnDelayTicks = 14` | 0.466666… s | 466 ms |
| `VarusE.asset` | `durationTicks = 8` | 0.266666… s | 266 ms |
| `VarusQ.asset` | `holdDurationTicks = 120` | 4 s | 4000 ms |
| `VarusQ.asset` | `releaseDurationTicks = 0` | 0 s | 0 ms |
| `VarusQ.asset` | `maxChargeTicks = 45` | 1.5 s | 1500 ms |
| `VarusQ.asset` | `consumeToggleCooldownTicks = 1200` | 40 s | 40000 ms |
| `VarusR.asset` | `durationTicks = 8` | 0.266666… s | 266 ms |
| `VarusW.asset` | `durationTicks = 360000` | 12000 s（200 min，近似常驻） | 12000000 ms |

`AatroxE.asset` 的 `speedPerTick = 0.2` 不是时间，但依赖 30 Hz；迁移为 `speedPerSecond = 6`，运行时再乘当前 `LogicSecondsPerTick`。

## Buff、装备与控制

| 资产 | 旧字段和值 | 旧 30 Hz 精确时间 | 迁移毫秒 |
|---|---:|---:|---:|
| `AatroxWorldEnder.asset` | `DecayTicks = 60` | 2 s | 2000 ms |
| `AatroxWorldEnder.asset` | `ExtendTicks = 150` | 5 s | 5000 ms |
| `AatroxWorldEnder.asset` | `MaximumRemainingTicks = 300` | 10 s | 10000 ms |
| `AatroxWorldEnder.asset` | `RestartBurstTicks = 60` | 2 s | 2000 ms |
| `AatroxWTether.asset` | `PullDurationTicks = 9` | 0.3 s | 300 ms |
| `Buff_CorruptionVines.asset` | `PeriodicIntervalTicks = 1` | 0.033333… s | 33 ms |
| `Buff_CorruptionVines.asset` | `SpreadTagTicks = 180` | 6 s | 6000 ms |
| `Buff_CorruptionVines.asset` | `ContactTicks = 30` | 1 s | 1000 ms |
| `Buff_CorruptionVines.asset` | `SpreadCrowdControlTicks = 60` | 2 s | 2000 ms |
| 其余正式 Buff | `PeriodicIntervalTicks = 0` | 0 s（关闭旧周期入口） | 0 ms |
| `SunderedSky.asset` | 模块 `CooldownTicks = 300` | 10 s | 10000 ms |
| 其余正式装备冷却字段 | `CooldownTicks/InternalCooldownTicks = 0` | 0 s | 0 ms |

原本以秒配置的 Buff 生命周期、周期反应和 VFX 时长也会同时迁移为整数毫秒；它们不属于“直接 Tick”清单，但使用同一 Bake 管线。

## 兵线、投射物与单位生命周期

| 资产 | 旧字段和值 | 旧 30 Hz 精确时间 | 迁移毫秒 |
|---|---:|---:|---:|
| `FullMatchMinionWaveConfig.asset` | `waveIntervalTicks = 900` | 30 s | 30000 ms |
| 同上 | `firstWaveTick = 900` | 30 s | 30000 ms |
| 同上，近战组 | `FirstSpawnOffsetTicks = 0` | 0 s | 0 ms |
| 同上，近战组 | `SpawnStepTicks = 24` | 0.8 s | 800 ms |
| 同上，远程组 | `FirstSpawnOffsetTicks = 48` | 1.6 s | 1600 ms |
| 同上，远程组 | `SpawnStepTicks = 24` | 0.8 s | 800 ms |
| `FullMatchProjectileRuntimeCatalog.asset`，普通投射物 7 项 | `MaxLifetimeTicks = 180` | 6 s | 6000 ms |
| 同上，普通投射物 7 项 | `QueryIntervalTicks = 1` | 0.033333… s | 33 ms |
| 同上，普通投射物 7 项 | `SameTargetCooldownTicks = 0` | 0 s | 0 ms |
| 同上，范围投射物 | Buff/CC `DurationTicks = 60` | 2 s | 2000 ms |
| 同上，范围投射物 | `MaxLifetimeTicks = 120` | 4 s | 4000 ms |
| 同上，范围投射物 | 查询/同目标冷却 `5 Tick` | 0.166666… s | 166 ms |
| 同上，短投射物 | Buff/CC `DurationTicks = 8` | 0.266666… s | 266 ms |
| 同上，短投射物 | `MaxLifetimeTicks = 15` | 0.5 s | 500 ms |
| 同上，束缚投射物 | Buff/CC/寿命 `45 Tick` | 1.5 s | 1500 ms |
| `FullMatchUnitDisposePolicyTable.asset` | `DeathPresentationTicks = 0/60/90` | 0/2/3 s | 0/2000/3000 ms |
| `FullMatchUnitRuntimeCatalog.asset` | 10 项 `RespawnDelayTicks = 0` | 0 s | 0 ms |

## 动画表现配置

以下字段虽然属于表现层，但也是 Inspector 时间，按同一规则迁移；它们不进入 Gameplay Checksum。

| 资产 | 旧攻击分段 Tick | 旧 30 Hz 精确时间 | 迁移毫秒 |
|---|---:|---:|---:|
| `AatroxAnimationProfile.asset` | `2 / 5 / 2 / 8` | 0.0667 / 0.1667 / 0.0667 / 0.2667 s | `66 / 166 / 66 / 266` ms |
| `TestUnitAnimationProfile.asset` | `2 / 5 / 2 / 8` | 同上 | `66 / 166 / 66 / 266` ms |
| `MinionAnimationProfile.asset` | `2 / 5 / 2 / 8` | 同上 | `66 / 166 / 66 / 266` ms |
| `TurretBlueAnimationProfile.asset` | `2 / 5 / 2 / 8` | 同上 | `66 / 166 / 66 / 266` ms |
| `TurretRedAnimationProfile.asset` | `2 / 5 / 2 / 8` | 同上 | `66 / 166 / 66 / 266` ms |

## 全局内容时间

| 资产 | 旧字段和值 | 旧 30 Hz 精确时间 | 迁移毫秒 |
|---|---:|---:|---:|
| `GlobalGameplayData.asset` | `PeriodicGoldIntervalTicks = 15` | 0.5 s | 500 ms |
| 同上 | `AttackSequenceResetIntervalTicks = 90` | 3 s | 3000 ms |

同一资产中原来以秒配置的倒计时、Launch 延迟、结束阶段、英雄复活、兵线周期、野怪重置/重生、自然恢复间隔会直接转成等价整数毫秒。

## 明确保留为 Tick 的配置

以下不是现实时间内容，改成毫秒会破坏帧同步语义，因此保留：

| 位置 | 字段和值 | 保留原因 |
|---|---:|---|
| `GlobalGameplayData.asset` | `MinCommandLeadTicks = 1` | Command 目标帧相对量 |
| 同上 | `MaxFutureCommandTicks = 12` | Command 合法未来帧窗口 |
| 同上 | `SnapshotWindowTicks = 180` | 快照历史容量，以帧数量定义 |
| 同上 | `MaxPredictionLeadTicks = 6` | 客户端最多预测到 Authority 前多少帧 |
| 同上 | `AuthorityRecoveryRetryTicks = 15` | AuthorityFrame 连续性恢复检查周期 |
| 同上/`Lobby.unity` | `StartLeadTicks = 3` | LaunchCommit 后预留的确定性帧头部空间 |
| `MinionTowerLongRunTest.unity` | `logEverySimulationTick = 1` | 测试诊断采样帧间隔 |

运行态的 `CurrentTick`、`StartTick`、`RemainingTicks`、`ElapsedTicks`、`SnapshotTick`、AuthorityFrame Tick、Command 目标 Tick 同样继续使用 Tick；本清单只迁移离线内容 authoring。
