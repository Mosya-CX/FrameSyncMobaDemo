# 帧同步 MOBA 战斗系统设计 v13.3 — 同 Tick 公平结算修正案

> Status: Current amendment to `moba_combat_system_design_v13_2.md`
>
> Authority: D-049. Where this amendment conflicts with v13.2, this amendment wins.

## 1. 目的与边界

本修正案消除 `UnitUid`、`RuntimeEntityPrefabId`、单位注册顺序、阵营内容编号和
Handler 遍历位置对同一 LogicTick 已接受、非随机且不存在正式完全平局的 Combat
请求集合的隐式先手影响，同时保持帧同步、回滚、正式死亡、助攻窗口、金币唯一
所有权与 UnitWorld 生命周期边界。

技术稳定排序可以保证各端一致，但不能独立充当玩法优先级。只有正式玩法阶段、
动作内效果序号与因果波次可以决定非交换操作的先后。`UnitUid` 仍是实体身份、查询、
规范记录/序列化顺序，并可作为设计明确指定的完全平局裁决键；本修正案不要求把
所有事件身份和 UID 完全解绑。

## 2. 全局 Handler 子阶段

`SimulationTickPipeline` 不再逐 Unit 执行完整 Handler 组。固定阶段为：

```text
All Unit.TickTags
All BuffHandler.Advance
All EquipmentHandler.AdvanceEffects
All HitReaction.TickUpdate
All AbilityHandler.TickUpdate
All MovementHandler.TickUpdate
All AttackHandler.TickUpdate
```

每一子阶段内部继续按 `UnitRegistry` 的规范 UnitUid 顺序遍历；该顺序只负责确定性，
不能决定最终的同批生命、护盾、死亡或非平局击杀归属。

## 3. 收集、封存与结算波次

普通 `SubmitShield / SubmitDamage / SubmitHeal` 分为：

```text
Collect -> SealCurrentWave -> SettleTargetBatches
```

Collect 完成基本合法性验证并保存内部 Pending envelope，不分配最终
`SequenceInTick`。Seal 按正式阶段、因果波次、动作来源与动作内效果序号形成规范
目标批次，再分配当前 Tick 的最终 `SequenceInTick`。

```text
Wave 0: 当前 Tick 的基础攻击、技能、投射物和已导入 Deferred 请求
Wave N+1: Wave N 的 Damage/Heal/OnHit/Buff/Equipment Reaction 新请求
```

同一 Wave 全部目标批次提交完成后才进入下一 Wave。`UnitDeath / UnitKill` Reaction
产生的普通战斗请求仍依 D-010 延迟到下一 Tick。超过
`MaxCombatSettlementCyclesPerTick` 产生确定性错误。

## 4. 同目标批次起始状态

同一 Target、同一 Wave 的请求冻结批次起始：

```text
Health / MaxHealth / LifeState
CurrentShield instances
本批公式需要的来源与目标 Stat / CombatModifier 读取结果
```

同批请求不能读取兄弟请求刚写入的生命、护盾或 Modifier 状态。正式多段机制若要求
后一段读取前一段结果，必须使用不同 `EffectOrdinal` 阶段或下一 Wave 明确表达。

## 5. 护盾、治疗与伤害提交

同一目标批次固定语义：

1. 根据批次起始状态计算各有效 Shield、Heal、Damage 结果。
2. 有效治疗先合并并以 MaxHealth 截断，不保存超额治疗。
3. 本批有效护盾加入可吸收集合；护盾类型匹配与实例消费采用正式稳定策略。
4. 伤害按类型与策略分配护盾吸收，再计算进入生命的候选伤害。
5. 对目标一次性提交最终护盾与生命状态。
6. 按封存后的规范事件序发布逐请求 Result；其 Reaction 进入下一 Wave。

```text
HealedHealth = min(MaxHealth, BatchStartHealth + TotalEffectiveHeal)
FinalHealth = max(0, HealedHealth - TotalActualLifeDamage)
```

若同批候选生命伤害总量超过可损失生命，则按每条请求候选生命伤害的固定点比例分配
`ActualLifeDamage`。分配必须守恒、与插入顺序无关；最小表示余数使用第 7 节的中性
平局分值分配。

## 6. 正式死亡与反应

只有目标批次提交后生命为零才进入 PendingDying。全部当前活动 Wave 结束后继续使用
UnitWorld 的 `RequestEnterDying / RequestRecoverFromDying / ConfirmUnitDeath` 正式
生命周期。已经封存的请求不因来源在同 Tick 进入 Dying 而失效。

UnitDying 生存决议继续在当前 Tick 完成；由其产生的普通 Combat 请求进入下一结算
Wave。UnitDeath / UnitKill 的普通请求继续进入 T+1 Deferred buffer。

## 7. 击杀者：最高有效生命伤害与中性平局

对使目标进入正式死亡候选的致死批次：

1. 将每条 Damage 的 `ActualLifeDamage` 解析并汇总到最终所属 Hero。
2. 只接受敌对、有效、非 Victim 本人的 Hero 候选。
3. `ActualLifeDamage` 总和最高的 Hero 为 `KillerHeroUid`。
4. 只造成护盾伤害、免疫/零伤害、纯 Overkill 或无法解析到 Hero 的值不进入竞争。
5. 助攻仍来自正式助攻窗口内的其它有效 Damage contributor。

若最高值完全相同，计算不消耗随机流的平局分值：

```text
TieScore64 = StableHash64(
    InitialMatchSeed,
    DeathLogicTick,
    VictimSpawnIdentity(SpawnLogicTick, SpawnSequenceInTick),
    CandidateHeroSpawnIdentity(SpawnLogicTick, SpawnSequenceInTick),
    CombatKillerTieDomain)
```

这里的 SpawnIdentity 从 UnitUid 提取，但明确排除 `RuntimeEntityPrefabId`；正式 Spawn
Sequence 在同一 Spawn Tick 内全局唯一。最小分值获胜。分值不得包含请求提交序号、
PrefabId、阵营、Handler 遍历位置或可通过
增加请求次数刷新的请求局部序号。不得调用 `DeterministicRandomService.Next*`。
哈希完全碰撞时才按完整 HeroUid（包含其 Prefab 字段）作最终确定性兜底。

本节替代 Combat v13.2 §7.14.3 和 D-035 的“最后有效 Damage 事件即击杀者”条款。
D-041 的“最后击杀英雄”统一解释为本节产生的 `KillerHeroUid`。

## 8. Sequence、事件日志与快照

`SequenceInTick` 保留为 Seal 后的规范结算/事件身份，不再代表 Submit API 的调用顺序。
`CombatContributionEventLog` 继续保存逐事件 Damage/Shield/Heal 事实与助攻窗口，但缓存的
LastHit 不再拥有击杀权威。

活动 Pending envelopes、Sealed batches、Wave scratch、比例分配缓存和致死批次候选都必须
在 Tick Capture 前清空。若 Deferred 请求需要保存新的正式来源/阶段语义，则这些字段进入
`DeferredCombatRequestSnapshot` 的规范序列化、SharedGameplayChecksum 与新的 Snapshot
schema；不得从当前 Unit 状态猜测修复。

## 9. 公平性与确定性验收

必须覆盖：

- 重复执行逐位等价；
- 连续与 Snapshot/Restore/Replay 等价；
- 请求插入顺序无关；
- 对同一已接受的非随机请求集合，改变 Submit/注册顺序、技术 Prefab/阵营编号，或仅
  重标不会改变正式目标选择与随机样本归属的 UID 后，生命、护盾、死亡和非平局击杀不变；
- 人为重标 UID 的测试只用于证明 UID 不会通过中间状态写入形成隐式先手权，不要求
  随机 Crit 样本继续映射到相同标签，也不要求改变正式的完全同距离投射物目标平局规则；
- 同 Tick 相互致死仍允许双方正式死亡；
- Shield+Damage、Heal+Damage、多来源 Overkill、Reaction Wave；
- 方案 A 的最高有效生命伤害；
- 方案 C 同一 seed 重演一致、跨固定 seed 语料不固定偏向阵营或 PrefabId；
- 非法配置、波次耗尽和结算循环上限确定性失败。

## 10. UID、随机与显式平局边界

本修正案采用以下已裁决边界：

1. `UnitUid` 保留实体身份、查询、事件身份、规范遍历/序列化顺序以及设计明确指定的
   完全平局裁决职责。禁止的是 UID 通过逐请求中间状态写入变成未声明的玩法先手权，
   不是禁止任何执行顺序或 UID 比较。
2. Combat v13.2 的 Crit 继续使用唯一、可快照恢复的
   `DeterministicRandomService`。同一实际对局在相同 seed、UID/请求规范顺序和随机流
   状态下必须跨端、Snapshot/Restore/Replay 等价；人为交换 UID 后，不要求每个随机
   样本仍归属于交换前的动作标签。因此无需为本修正案新增 OriginActionId、EffectOrdinal
   或 GameplayParticipantId 公共合同。
3. Projectile v19 §13.18.1 的完全同距离候选继续按 `TargetUnitUid` 排序。这是目标选择
   阶段正式声明的完全平局规则，不属于 Combat 的隐式中间状态优先权；最大命中数、
   穿透和 `TotalHitCount` 衰减继续以该正式命中集合/顺序为准。
4. 暴击与投射物完成各自随机/目标选择并提交 Combat 请求后，所有已封存请求统一进入
   本修正案的批结算边界，不能再因 Source/Target UID 的先后写入生命、护盾或 Dying
   状态而获得额外优势。

由此，第 1/9 节的镜像验收只约束非随机 Combat 批结算与非平局结果，不构成对随机样本
标签不变性或正式目标完全平局规则的要求。方案 A 击杀归属和方案 C 平票保底保持不变。
