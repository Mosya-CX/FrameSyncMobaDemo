# 快照树与字段归属

## 目标实现

跨 Tick 状态都由其唯一模块拥有并可规范捕获、恢复和校验。

## 技术方案

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

## 边界情况

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Deterministic/Core/IRollback.cs`：当前关联实现定义 IRollback（以源码为实际命名）。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：当前关联实现定义 UnitSnapshot、UnitWorldSnapshot、GameplaySnapshot、RollbackFrameSnapshot（以源码为实际命名）。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：当前关联实现定义 SharedGameplayChecksum、ChecksumSegment、StatEntryField（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/ChecksumNewStateCoverageTests.cs`：Checksum_ChangesWhenProjectileOnHitOverrideDiffers、Checksum_ChangesWhenPendingLifetimeOverrideDiffers、Checksum_ChangesWhenPassiveAbilityLevelDiffers、Checksum_ChangesWhenMinionThreatRefreshTickDiffers、Checksum_ChangesWhenMinionThreatTableDiffers、Checksum_ChangesWhenEquipmentTriggerCountDiffers、Checksum_ChangesWhenGameplayParticipantIdDiffers。
- `Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs`：TestAccount_CommandLineOverridesPersistedIdentity、Lobby_RequiresEveryAssignedPlayerAtFullReadyBarrier、VersionHandshake_RejectsAnyCriticalMismatch、ClientFlow_ReachesLobbyOnlyAfterNgoConnects、ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode、ClientFlow_CancelWaitingAssignment_DeletesTicketAndReturnsMain、ClientFlow_CancelWhileTicketCreationIsPending_DeletesLateTicket。
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。
- `Assets/Scripts/FrameSync/Tests/BootstrapDeterminismProbeTests.cs`：ServerFirstTick_MatchesClientPredictionFirstTick。
- `Assets/Scripts/FrameSync/Tests/FrameSyncPipelineTests.cs`：GameplayCommand_CreateMove_WritesCanonicalBytes、CommandCollector_MergeMove_LastWins、CommandCollector_ContentRevisionTracksCanonicalMutations、Pipeline_SingleUnit_MovesWithCommand、Checksum_SameState_SameHash、Checksum_DifferentState_DifferentHash、Pipeline_Deterministic_SameCommandsSameResult。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 帧同步快照内容附录 v7.2


---

### 一、统一接口

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

### 二、总体快照树

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

### 十二、显式恢复顺序

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

### 十六、验收标准

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

### Sequence、事件日志与快照

`SequenceInTick` 保留为 Seal 后的规范结算/事件身份，不再代表 Submit API 的调用顺序。
`CombatContributionEventLog` 继续保存逐事件 Damage/Shield/Heal 事实与助攻窗口，但缓存的
LastHit 不再拥有击杀权威。

活动 Pending envelopes、Sealed batches、Wave scratch、比例分配缓存和致死批次候选都必须
在 Tick Capture 前清空。若 Deferred 请求需要保存新的正式来源/阶段语义，则这些字段进入
`DeferredCombatRequestSnapshot` 的规范序列化、SharedGameplayChecksum 与新的 Snapshot
schema；不得从当前 Unit 状态猜测修复。

### Deferred、Snapshot、Checksum 与版本

- `CombatRequestHeader` 保存 `OriginActionId` 与 `EffectOrdinal`；
- `OnHitEventData` 与 `DamageEventData` 传播父 `EffectOrdinal`，事件派生伤害生成路径化子序号；
- `DeferredCombatRequest.Damage` 原样保存完整 Header；
- `ProjectileSpawnRequest`、Pending、Runtime 与 Active Snapshot 保存 OriginActionId；
- `UnitSnapshot` 保存 GameplayParticipantId；
- SharedGameplayChecksum 逐字段写入上述身份；
- GameplaySnapshot schema 升至 24，GameplayDataVersion 升至 4；
- Bootstrap payload wire 与 Command schema 不变。


## 需求演进

### 2026-10-02

变动内容：一 Tick 一快照，恢复拆为 Restore、Resolve、Rebuild。

legacyDecision：D-004

### 2026-10-02

变动内容：战斗 Tick 末仅保存贡献跟踪和跨 Tick 延迟请求。

legacyDecision：D-011

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050

