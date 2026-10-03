# 濒死批次与公平击杀归属

## 目标实现

正式死亡与击杀者在同一结算事实下确定。

## 技术方案

Combat 同步请求 UnitWorld 更新 Dying/Dead；致死批次按有效敌方英雄 ActualLifeDamage 总和取最大，纯中性分数处理最高伤害并列。

## 边界情况

旧末次伤害者方案已被修订；队伍、Prefab、提交序列不决定平局；FormalDeathResult 唯一输出给统计和奖励。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/Trackers/CombatContributionEventLog.cs`：当前关联实现定义 CombatContributionKind、CombatContributionEvent、CombatContributionEventSnapshot、CombatContributionEventLogSnapshot、CombatContributionEventLog（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CombatContributionEventLogTests.cs`：LastHit_IsMostRecentDamageEventContributor、PruneExpired_RemovesOldEventsAndClearsLastHitWhenEmpty、Capacity_DropsOldestEventFirst、ResolveAssistants_ExcludesKillerAndSortsAscending、SnapshotRoundTrip_PreservesEventsAndLastHit。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 正式死亡与反应

只有目标批次提交后生命为零才进入 PendingDying。全部当前活动 Wave 结束后继续使用
UnitWorld 的 `RequestEnterDying / RequestRecoverFromDying / ConfirmUnitDeath` 正式
生命周期。已经封存的请求不因来源在同 Tick 进入 Dying 而失效。

UnitDying 生存决议继续在当前 Tick 完成；由其产生的普通 Combat 请求进入下一结算
Wave。UnitDeath / UnitKill 的普通请求继续进入 T+1 Deferred buffer。

### 击杀者：最高有效生命伤害与中性平局

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


## 需求演进

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009

### 2026-08-06

变动内容：保留贡献事件和助攻窗口；末次伤害击杀归属已被最高有效伤害修订替代。

legacyDecision：D-035

### 2026-08-24

变动内容：战斗封存波次和冻结批次起始状态；击杀按最高有效生命伤害，取代末次伤害者。

legacyDecision：D-049

