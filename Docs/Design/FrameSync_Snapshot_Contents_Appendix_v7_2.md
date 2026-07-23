# 帧同步快照内容附录 v7.2

> 配套文档：`FrameSync_Flow_Integrated_System_Design_v10_2.md`。  
> 本附录冻结帧同步总控直接依赖的快照层级、统一接口、恢复阶段、金币确认层边界和 AuthorityRecovery 所需的本地恢复点。各系统内部字段仍由对应正式设计案负责。

---

# 一、统一接口

```csharp
public interface IRollback<TState>
{
    void Capture(ref TState state);
    void Restore(in TState state);
    void Resolve(in RollbackContext context);
    void Rebuild(in RollbackContext context);
}
```

```text
Capture
    捕获稳定 Gameplay 状态。

Restore
    恢复稳定字段和对象集合。

Resolve
    使用稳定 UID 修复运行时引用。

Rebuild
    重建派生数据。
```

没有引用或派生数据的系统可以空实现对应方法。

顶层协调器显式调用各强类型聚合根，不维护异构泛型列表。

---

# 二、总体快照树

```text
RollbackFrameSnapshot
    SnapshotTick
    SnapshotSchemaVersion
    GameplaySnapshot
        MatchRuleRuntimeSnapshot
            MatchStatisticsRuntimeSnapshot
        UnitWorldSnapshot
        ProjectileWorldSnapshot
        CombatSystemSnapshot
        EquipmentShopRuntimeSnapshot
        PhysicsRuntimeSnapshot
        DeterministicRandomSnapshot
```

不进入 GameplaySnapshot：

```text
ServerTick
LatestAuthorityFrameTick
LocalSimulationTick
AuthorityFrameBuffer
AcceptedCommandRelayBuffer
GameplayCommandBuffer

GoldIncomeRuntime
    CurrentBatchBuilder
    UnconfirmedBatchHistory
    GoldIncomeBatchDigestHistory
    ConfirmedEarnedGoldTotalByPlayer[]
    ConfirmedIncomeThroughTick

LocalFrameVerificationRecordByTick
ConfirmedGoldSettlementSink
PredictionPauseReasons
网络连接
UOS SDK
UnitEventBus 固定路由
Unity 表现对象
```

---

# 三、`RollbackFrameSnapshot`

```text
SnapshotTick =
    恢复后下一次应该执行的 Gameplay Tick
```

Tick `T` 完成后保存：

```text
SnapshotTick = T + 1
```

恢复后：

```text
LocalSimulationTick = SnapshotTick
```

第一版：

```text
SnapshotIntervalTicks = 1
```

每 Tick 保存一次快照。

---

# 四、`MatchRuleRuntimeSnapshot`

```text
CurrentPhase
PhaseEnterTick
RunningStartTick
BlueBaseUnitUid
RedBaseUnitUid
GameOverTick
FinishTick
WinningTeamId
EndReason
MatchStatisticsRuntimeSnapshot
```

客户端预测阶段不会提交结束状态。客户端只在候选 Tick 的 AuthorityFrame 权威重演确认结束后写入 Ending 状态。

---

# 五、`UnitWorldSnapshot`

```text
UnitWorldSnapshot
    UnitSnapshot[]
    PendingUnitLifecycleQueue

    MinionSystemSnapshot
        WaveIndex
        NextWaveLogicTick
        PendingTickets[]
        NextTicketCursor
        ManagedMinionUids[]

    JungleCampSnapshot[]
        CampId
        State
        MemberUidsBySlot[]
        MemberAliveBySlot[]
        MainMonsterDead
        PrimaryTargetUid
        LastHostileActionLogicTick
        NextRespawnLogicTick
        ResetBeginLogicTick

    UnitAIControllerSnapshot[]
        ControllerKind
        OwnerUnitUid
        MinionState
        MonsterState
        TowerState

    RuntimeRevision
```

字段与 Non-Hero v4 的正式运行状态对齐。

## 5.1 AI 主动生效

不保存：

```text
FirstAITickLogicTick
FirstActiveLogicTick
CommonState
```

统一推导：

```text
CurrentTick > Owner.UnitUid.SpawnLogicTick
```

具体 Controller 只保存自己真实存在的状态分支。

## 5.2 Unit 金字塔聚合

UnitWorld 按稳定 UnitUid 捕获 Unit。Unit 再聚合：

```text
LifeState
Locomotion
Behavior / Intent / Action
AttackHandler
AbilityHandler
BuffHandler
CrowdControlHandler
StatHandler
EquipmentHandler
事件处理后形成的 RuntimeState
```

正式死亡不会全量清空 StatHandler 或 CombatModifiers。各来源系统通过自己的 Handle 精确清理临时效果，快照保存清理后的真实状态。

UnitEventBus 自身不保存。

## 5.3 非英雄死亡后的管理状态

小兵死亡后：

```text
ManagedMinionUids
```

反映已经注销的管理关系。

野怪死亡后：

```text
MemberAliveBySlot
MainMonsterDead
State
NextRespawnLogicTick
```

反映营地成员死亡与复活计划。

死亡非英雄的 AIController 从 UnitWorld 注册表移除，因此对应快照中不再存在活动 Controller；营地或管理系统仍保存复活所需的稳定业务状态。

---

# 六、`ProjectileWorldSnapshot`

正式与 Projectile v18 对齐：

```text
ProjectileWorldSnapshot
    PendingSpawnRecordSnapshot[] PendingSpawns
    ProjectileSnapshot[] ActiveProjectiles
```

恢复后重建：

```text
PendingSpawnByUid
ActiveRegistry
ActiveProjectiles 稳定排序索引
查询缓存
```

不进入 Tick 末快照：

```text
PendingHitBuffer
PendingEndBuffer
PendingDestroyRequests
NextSpawnSequenceInTick
CurrentSequenceTick
销毁历史
```

ProjectileWorld 自己保证每个 LogicTick 的 `SpawnSequenceInTick` 从 0 开始。

---

# 七、`CombatSystemSnapshot`

正式与 Combat v13.2 对齐。

## 7.1 正式结构

```csharp
public struct CombatSystemSnapshot
{
    public DamageContributionTrackerSnapshot[]
        DamageContributionTrackers;

    public DeferredCombatRequestSnapshot[]
        DeferredRequests;
}
```

```csharp
public struct DamageContributionTrackerSnapshot
{
    public UnitUid VictimUnitUid;

    public DamageContributionRecordSnapshot[]
        Records;
}
```

```csharp
public struct DamageContributionRecordSnapshot
{
    public UnitUid ContributorHeroUid;
    public int LastContributionLogicTick;
    public fp ContributionValue;
    public int ExpireLogicTick;
}
```

```csharp
public struct DeferredCombatRequestSnapshot
{
    public int ExecuteLogicTick;
    public int SourceLogicTick;
    public ushort DeferredSequenceInSourceTick;
    public CombatRequestKind RequestKind;

    public ShieldRequestSnapshot Shield;
    public DamageRequestSnapshot Damage;
    public HealRequestSnapshot Heal;
}
```

宽联合只能有与 `RequestKind` 对应的一个有效 Payload。

## 7.2 Tick 末不保存

```text
ShieldQueue
DamageQueue
HealQueue
PendingDyingRecord
DeferredLifeDamageCache
DyingReviveCandidateRuntime
DeathResolution 临时集合
DeathRewardContext 临时集合
FormalDeathResult 构建缓存
CombatTickResult 构建态
CurrentSequenceLogicTick
NextSequenceInTick
SequenceExhausted
NextDeferredSequenceInSourceTick
DeferredSequenceExhausted
DeathSequenceInTick
DeferredRequestBuildScope
DyingResolutionScope
```

## 7.3 Capture 断言

```text
ShieldQueue empty
DamageQueue empty
HealQueue empty
PendingDyingRecordSet empty
DeferredLifeDamageCache empty
DyingResolutionScope closed
CombatReactionSchedulingScope closed
DeferredRequestBuildScope closed
CombatTickResult 已冻结
MatchStatisticsRuntime 已完成消费
GoldIncomeAllocations 已提交
```

同时验证：

```text
所有 DeferredRequest.ExecuteLogicTick == CurrentTick + 1
同一 SourceLogicTick 内 DeferredSequenceInSourceTick 不重复
允许合法序列缺号
删除记录后不得重新编号
禁止自然回绕
按 ExecuteLogicTick、SourceLogicTick、
    DeferredSequenceInSourceTick 稳定升序保存
DamageContributionTracker 不包含重复 ContributorHeroUid
```

## 7.4 Restore / Resolve / Rebuild

```text
Restore
    清空 Tick 内活动队列和瞬态 Scope。
    直接恢复 Tracker 与 DeferredRequestBuffer。
    不发布事件，不提交新请求。

Resolve
    验证 Victim、Contributor、Deferred Source / Target
    和静态 Recipe。
    无效稳定引用产生确定性恢复错误。
    不静默删除、不补建、不重新计算。

Rebuild
    重建 Tracker 与 DeferredRequest 查询索引。
    不重放 UnitDeath / UnitKill。
    不重新创建历史请求。
```

下一次 `CombatSystem.BeginTick(SnapshotTick)` 导入到期延迟请求。

## 7.5 UnitWorld 清理接缝

非死亡 Despawn 或永久销毁时，UnitWorld 清理：

```text
该 UnitUid 作为 Victim 的 Tracker
该 UnitUid 作为 Contributor 的记录
以该 UnitUid 为 Target 的 DeferredRequest
```

作为 Source 的 DeferredRequest 不能静默删除。必须等待：

```text
CombatSystem.HasDeferredRequestFrom(UnitUid)
    == false
```

再最终注销、回池或 Destroy。

---

# 八、`EquipmentShopRuntimeSnapshot`

```text
EquipmentShopRuntimeSnapshot
    ShopTraderRuntimeSnapshot[]
```

每个交易者至少保存：

```text
PlayerSlot
ControlledUnitUid
NextOperationSequence

OperationLog[]
    OperationSequence
    OperationType
    LogicTick
    GoldDelta
    Reverted
    RevertedLogicTick
    SlotChanges
    EquipmentRevisionBefore
    EquipmentRevisionAfter

UndoableOperationStack
LastUndoInvalidReason
LastCombatParticipationFlags
RuntimeRevision
```

不保存：

```text
ConfirmedEarnedGoldTotal
CurrentAvailableGold
TotalShopExpenditure
EffectiveShopGoldDelta
CachedEffectiveShopGoldDelta
```

其中：

```text
EffectiveShopGoldDelta
    从 OperationLog 中 Reverted == false 的 GoldDelta 求和。

CurrentAvailableGold
    = ConfirmedEarnedGoldTotal
      + EffectiveShopGoldDelta。
```

`CachedEffectiveShopGoldDelta` 只是派生缓存，必须在 `Rebuild` 阶段从 OperationLog 重建。

---

# 九、`GoldIncomeRuntime` 与快照边界

`GoldIncomeRuntime` 是以下状态的唯一所有者：

```text
CurrentBatchBuilder
UnconfirmedBatchHistory
GoldIncomeBatchDigestHistory
InitialEarnedGoldByPlayer[]
ConfirmedEarnedGoldTotalByPlayer[]
ConfirmedIncomeThroughTick
BuildState
```

整体不进入 GameplaySnapshot。

回滚前：

```text
GoldIncomeRuntime.DiscardUnconfirmedFromTick(T)
```

删除 Tick `T` 及之后的未确认批次和摘要，保留确认累计与确认进度。

确认金币：

```text
不扫描商店 Command。
不生成 Dirty Tick。
不主动重演预测后缀。
```

商店恢复：

```text
恢复 OperationLog 和 Undo Stack。
重建 EffectiveShopGoldDelta。
读取 ConfirmedEarnedGoldTotal。
计算 CurrentAvailableGold。
```

不需要每 Tick 收入镜像或金币余额历史。

`IConfirmedGoldSettlementSink` 只接收已确认批次，不进入 GameplaySnapshot，也不维护比赛内金币总量。

---

# 十、`PhysicsRuntimeSnapshot`

必须保存：

```text
UnitCollisionEventBufferSnapshot
    PreviousPairs[]
```

不保存：

```text
RvoGrid
UnitFinalGrid
Bounds
Cell Buckets
CurrentPairs
查询缓存
Unity Transform
```

恢复顺序：

```text
Restore PhysicsEntity 逻辑状态
Rebuild Bounds / RvoGrid / UnitFinalGrid
恢复并激活 PreviousPairs
进入下一 Tick Collision Detect
```

`PhysicsEntity2D.LateUpdate` 属于 Presentation Sync，只把最终逻辑姿态写到 Unity Transform。

---

# 十一、`DeterministicRandomSnapshot`

```text
State
CallCount optional
```

恢复后必须产生相同随机序列。

---

# 十二、显式恢复顺序

```text
阶段一：Restore
    MatchRuleRuntime
    MatchStatisticsRuntime
    UnitWorld
    ProjectileWorld
    CombatSystem
    EquipmentShopRuntime
    PhysicsWorld
    DeterministicRandomService

阶段二：Resolve
    UnitUid -> Unit
    ProjectileUid -> Projectile
    AIController.OwnerUnitUid -> Unit
    DamageContribution Victim / Contributor -> Unit
    DeferredCombatRequest Source / Target -> Unit
    静态 Combat Recipe
    其它稳定引用

阶段三：Rebuild
    Physics Bounds、RvoGrid、UnitFinalGrid
    Projectile Registry 与稳定索引
    Combat Tracker 与 DeferredRequest 查询索引
    Buff / Equipment 派生索引
    Modifier 聚合
    CapabilityState
    CrowdControlStateView
    EffectiveShopGoldDelta
    CurrentAvailableGold
    PreviousPairs
    表现镜像
```

无效 Combat 稳定引用产生确定性恢复错误，禁止静默删除。

---

# 十三、快照缓冲与未确认锚点

```text
SnapshotIntervalTicks = 1
```

```text
RollbackAnchorTick =
    LatestAuthorityFrameTick + 1
```

禁止淘汰：

```text
SnapshotTick >= RollbackAnchorTick
```

容量不足时暂停预测，不能删除未确认恢复起点。

---

# 十四、`AuthorityRecovery` 与本地确认历史

当前版本只补发缺失 AuthorityFrame。

客户端必须保留最早缺失 Tick 对应的：

```text
Gameplay Snapshot
Command 历史
GoldIncomeRuntime 未确认批次和摘要
LocalFrameVerificationRecord
```

恢复流程：

```text
缓存缺口之后的 AuthorityFrame。
暂停预测。
补发缺失帧。
按 Tick 连续完成：
    Command 对账
    SharedGameplayChecksum 对账
    必要权威纠错重演
    GoldIncomeRuntime.ConfirmAuthorityFrame
    LatestAuthorityFrameTick 推进
```

金币确认本身不主动重演后续预测 Tick。

不提供 BaseSnapshot、金币种子、客户端进程重启恢复或中途加入。本地恢复点不存在时终止该客户端对局连接。

---

# 十五、预测结束候选

```text
PredictedMatchEndCandidateTick
```

属于客户端 PredictionRollbackCoordinator，不进入 GameplaySnapshot。

规则：

```text
预测 Tick T 完整结算后基地正式 Dead
    -> 暂停预测。

连续 AuthorityFrame 到达 T 并权威重演：
    基地未死 -> 清除候选，恢复预测。
    基地确实死亡 -> MatchRuleRuntime 进入 Ending，
                     LocalSimulationTick 停在 T + 1。
```

最终 Result 数据以服务端 `MatchResultState` 为准。

---

# 十六、验收标准

```text
1. LocalSimulationTick 在恢复后等于 SnapshotTick。
2. 第一版每 Tick 保存一次快照。
3. MatchStatisticsRuntimeSnapshot 正确恢复。
4. AI 主动生效 Tick 从 SpawnLogicTick 推导。
5. ProjectileWorldSnapshot 只保存 PendingSpawns 和 ActiveProjectiles。
6. Projectile 序列分配器状态不进入 Tick 末快照。
7. CombatSystemSnapshot 只保存 DamageContributionTrackers 和 DeferredRequests。
8. Combat Capture 时三条活动队列为空。
9. DeferredRequest 允许合法序列缺号且不重新编号。
10. Combat Resolve 遇到无效稳定引用时失败。
11. UnitDeath / UnitKill 新普通战斗请求在下一 Tick 导入。
12. 正式死亡不全量清空 Modifier。
13. CurrentAvailableGold 不进入快照。
14. GoldIncomeRuntime 是未确认金币批次和摘要唯一所有者。
15. GoldIncomeRuntime 不进入 GameplaySnapshot。
16. SharedGameplayChecksum 必填。
17. GoldIncomeBatchDigest 强制纳入 Checksum。
18. LocalFrameVerificationRecord 在重演时正确覆盖。
19. AuthorityFrame 不携带具体金币结果。
20. 权威 Command 和 Checksum 一致时直接接受。
21. 不一致时执行普通权威纠错重演。
22. 金币确认不主动重演预测后缀。
23. RequestCheck 失败不在收入确认后追溯生成 Command。
24. 未确认 Snapshot、Gold Batch、Command 和 Checksum 历史不提前淘汰。
25. AuthorityRecovery 只补发缺失 AuthorityFrame。
26. 本地恢复点丢失时终止客户端对局连接。
27. 相同快照、Command、配置和随机状态重演时，
    Combat DeferredRequests、GoldIncomeBatchDigest、
    SharedGameplayChecksum、交易结果和表现事件完全一致。
```

