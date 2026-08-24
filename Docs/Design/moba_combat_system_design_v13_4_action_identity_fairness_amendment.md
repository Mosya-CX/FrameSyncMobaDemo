# 帧同步 MOBA 战斗系统设计 v13.4 — 动作随机身份与投射物中性平局修正案

> Status: Current amendment to Combat v13.2 and v13.3
>
> Authority: D-050. Where this amendment conflicts with D-049/v13.3 §10 or
> Projectile v19 §7.7's UID equal距排序，this amendment wins.

## 1. 目的

在 v13.3 已消除 Combat 批结算先写优势的基础上，继续消除技术 UID 顺序对以下结果的
影响：

- 同 Tick 多条可暴击伤害分别获得哪个随机样本；
- 投射物完全同距离候选在命中上限、穿透或衰减下进入哪个命中集合。

本修正案不取消 `UnitUid`/`ProjectileUid` 的实体身份职责，而是为需要公平随机或平局
裁决的玩法对象增加独立、可回滚的玩法身份。

## 2. GameplayParticipantId

每个权威 Unit 必须拥有不可变且当前存活集合内唯一的 `GameplayParticipantId`。它由
稳定玩法出生来源组成，不得读取 PrefabId、UnitUid、注册顺序、Unity InstanceID 或阵营
遍历位置：

```text
Domain / Scope / Generation / Ordinal
```

正式来源：

- 初始单位：`StableSpawnOrder`；
- 小兵：`Team + Lane + SpawnLogicTick + StableEntryIndex` 的正式票据身份；
- 野怪：`CampId + SpawnLogicTick + MemberSlot`；
- 派生单位：显式父来源、生成 Tick 与子序号；
- 测试/工具：必须显式提供稳定身份，不得由 GameObject 顺序生成。

该身份进入 Unit Snapshot 与 SharedGameplayChecksum。Restore 必须精确恢复并验证唯一性，
不得根据恢复时的 UnitUid 重新推导。

## 3. OriginActionId 与 EffectOrdinal

会产生随机 Crit 或参与投射物平局裁决的动作使用：

```text
OriginActionId
    SourceParticipantId
    SourceType
    SourceId
    OriginLogicTick
    SourceLocalSequence

EffectOrdinal
    动作内稳定效果序号
```

普攻使用攻击开始 Tick 与攻击本地序号；技能使用 AbilitySession 的 StartLogicTick、
SessionUid 和 AbilityId；投射物继承生成它的 OriginActionId，每个伤害效果使用数组中的
稳定 EffectOrdinal。多目标随机结果还包含目标 `GameplayParticipantId`，因此无需依赖
TargetUnitUid 排序来分配随机样本。

事件派生伤害必须把父伤害的 `EffectOrdinal` 折入子效果序号；`OnHitEventData` 和
`DamageEventData` 均携带父序号。两个不同父效果即使触发相同 Recipe/配置，也不得
复用同一子 Crit 键。负 `EffectOrdinal` 在 Damage 提交、Deferred 和 Restore 边界
确定性失败，不能因 0%、100% 或强制暴击路径而跳过校验。

缺少动作身份的概率暴击必须确定性失败。`ForceCrit` 和 100% 暴击不需要随机样本，但
仍应在正式生产路径携带动作身份以便审计和后续效果扩展。

## 4. 动作级确定性暴击

概率暴击分值：

```text
CritRoll64 = StableHash64(
    InitialMatchSeed,
    OriginActionId,
    TargetGameplayParticipantId,
    EffectOrdinal,
    CombatCritDomain)
```

将固定 32 位映射为 `[0, 1)` 固定点值，与当前暴击概率比较。该运算不调用
`DeterministicRandomService.Next*`，因此其它系统随机调用数量和 Damage 请求规范排序
不会重新分配 Crit 样本。同一动作、目标和效果序号重复求值必须得到同一结果。

## 5. 投射物完全同距离裁决

移动 Sweep 与 AoE 候选首先继续按正式几何距离升序。距离完全相同时计算：

```text
TargetTieScore64 = StableHash64(
    InitialMatchSeed,
    ProjectileOriginActionId,
    CandidateGameplayParticipantId,
    ProjectileTieDomain)
```

排序键为：

```text
HitDistance
TargetTieScore64
GameplayParticipantId
TargetUnitUid  // 仅完整身份/分值碰撞兜底
```

因此技术 UID 重标不会改变被选中的玩法参与者。哈希只负责中性打散，不消耗全局随机
流；跨固定 seed 语料不得固定偏向 Team、PrefabId 或 UID 较小者。

## 6. Deferred、Snapshot、Checksum 与版本

- `CombatRequestHeader` 保存 `OriginActionId` 与 `EffectOrdinal`；
- `OnHitEventData` 与 `DamageEventData` 传播父 `EffectOrdinal`，事件派生伤害生成路径化子序号；
- `DeferredCombatRequest.Damage` 原样保存完整 Header；
- `ProjectileSpawnRequest`、Pending、Runtime 与 Active Snapshot 保存 OriginActionId；
- `UnitSnapshot` 保存 GameplayParticipantId；
- SharedGameplayChecksum 逐字段写入上述身份；
- GameplaySnapshot schema 升至 24，GameplayDataVersion 升至 4；
- Bootstrap payload wire 与 Command schema 不变。

## 7. 验收

必须覆盖：

- 同一动作键重复求值、Snapshot/Restore/Replay 逐位等价；
- 交换技术 UnitUid/PrefabId/注册顺序但保持 Participant/Action 身份后，Crit 归属不变；
- 在 Crit 请求前后插入其它全局随机调用不改变该 Crit；
- 投射物完全同距、最大命中数、穿透和衰减在 UID 重标后选择相同 Participant；
- 固定 seed 语料不永久偏向 Team、PrefabId 或 UID 升序；
- Deferred Damage 和 Pending/Active Projectile 身份快照/校验覆盖；
- 缺失/重复 Participant、缺失概率 Crit ActionId、非法 EffectOrdinal 确定性失败。
