# 预测回滚与逐帧重演

## 目标实现

预测和权威不一致时，从合法锚点恢复并重演得到权威结果。

## 技术方案

PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。

## 边界情况

不倒退已接受金币基线、不重读设备输入；失效确定性引用明确失败；每个 AuthorityFrame 形成单 Tick 接受屏障。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：当前关联实现定义 UnitSnapshot、UnitWorldSnapshot、GameplaySnapshot、RollbackFrameSnapshot（以源码为实际命名）。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：当前关联实现定义 PredictionPauseReason、LocalFrameVerificationRecord、MissingAuthorityFrameRange、AuthorityRecoveryRequest、AuthorityRecoveryResponse（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/AggregateSnapshotContractTests.cs`：Restore_UsesStableUnitUidAndRestoresRandomAndPhysicsState、Restore_RejectsNonCanonicalOrMissingUnitIdentity、Restore_RejectsDuplicateGameplayParticipantIdentity、Restore_RejectsMissingGameplayParticipantIdentity、SnapshotStore_WritesExplicitOuterSchemaAndNextTick。
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 快照和回滚锚点

```text
SnapshotTick =
    恢复后下一次应该执行的 Gameplay Tick
```

Tick `T` 完成后保存：

```text
SnapshotTick = T + 1
SnapshotIntervalTicks = 1
```

```text
RollbackAnchorTick =
    LatestAuthorityFrameTick + 1
```

普通回滚必须满足：

```text
ReplayFromTick >= RollbackAnchorTick
```

### 统一接口与聚合根

```csharp
public interface IRollback<TState>
{
    void Capture(ref TState state);
    void Restore(in TState state);
    void Resolve(in RollbackContext context);
    void Rebuild(in RollbackContext context);
}
```

顶层显式调用：

```text
MatchRuleRuntime
MatchStatisticsRuntime
UnitWorld
ProjectileWorld
CombatSystem
EquipmentShopRuntime
PhysicsWorld
DeterministicRandomService
```

不进入 GameplaySnapshot：

```text
GoldIncomeRuntime
LocalFrameVerificationRecordByTick
AuthorityFrameBuffer
AcceptedCommandRelayBuffer
GameplayCommandBuffer
PredictionPauseReasons
网络连接
```

### 普通 Relay 回滚

已执行预测 Tick 收到更新版 Relay：

```text
EarliestDirtyPredictionTick =
    min(当前值, Relay.TargetTick)
```

同一网络批次只执行一次最早 Relay Dirty Tick 回滚。

金币确认不写入 `EarliestDirtyPredictionTick`。

### 回滚前处理

```pseudo
function PrepareReplay(replayFromTick):
    GoldIncomeRuntime
        .DiscardUnconfirmedFromTick(replayFromTick)

    LocalFrameVerificationRecordByTick
        .RemoveFrom(replayFromTick)

    snapshot =
        SnapshotStore.FindLatestAtOrBefore(
            replayFromTick)

    RestoreGameplay(snapshot)
```

恢复后：

```text
LocalSimulationTick = snapshot.SnapshotTick
```

### AuthorityFrame 单 Tick 屏障

处理 `AuthorityFrame(T)`：

```text
1. 要求 T == LatestAuthorityFrameTick + 1。

2. 读取本地：
       CanonicalCommandBytes[T]
       LocalFrameVerificationRecord[T]

3. 若 Command 与 Checksum 都一致：
       接受 AuthorityFrame(T)。

4. 若任一不一致：
       保存旧 PredictedEndTick。
       从 SnapshotTick = T 恢复。
       丢弃 Tick T 及之后未确认 Gold Batch、Digest 和 Verification Record。
       使用 AuthorityFrame(T) 权威 Command，
       重演 Tick T 到旧 PredictedEndTick。
       重新生成 Tick T 的 Batch、Digest 和 Checksum。
       再次比较 Tick T SharedGameplayChecksum。

5. 权威重演后仍不一致：
       记录确定性诊断。
       终止当前客户端对局。

6. Tick T 被接受后：
       GoldIncomeRuntime.ConfirmAuthorityFrame(
           AuthorityFrame(T))。
       LatestAuthorityFrameTick = T。
       淘汰 LocalFrameVerificationRecord[T]。

7. 才处理 AuthorityFrame(T + 1)。
```

### 权威纠错和金币基线

纠错重演 Tick `T..PredictedEndTick` 时：

```text
ConfirmedEarnedGoldTotal
    仍只确认到 Tick T - 1。
```

完成重演并验证 Tick `T` 后，才确认 Tick `T` 收入。

收入确认后不再次重演预测后缀。后续预测 Tick 暂时使用较保守的确认金币基线是允许的，等其自身 AuthorityFrame 到达时再进行普通 Checksum 对账。

### 金币确认不主动重演

```text
GoldIncomeRuntime.ConfirmAuthorityFrame(T)
    不扫描 Purchase 或 Undo。
    不追溯创建 RequestCheck 失败的 Command。
    不主动重演 T + 1 之后的预测 Tick。
```

### Replay Command 优先级

```text
AuthorityFrame
最新 AcceptedCommandRelay
本地预测 Command
空命令帧
```

重演后重新保存：

```text
GoldIncomeRuntime 未确认批次和 Digest
LocalFrameVerificationRecord
GameplaySnapshot
```

### AuthorityRecovery

AuthorityRecovery 只补发缺失 AuthorityFrame。客户端保留最早缺失 Tick 的 Snapshot、Command、Gold Batch、Digest 和 Verification Record。

本地恢复点不存在时终止当前客户端对局连接，不请求 BaseSnapshot。

### CombatSystem 恢复

CombatSystem Restore 直接恢复：

```text
DamageContributionTrackerSnapshot[]
DeferredCombatRequestSnapshot[]
```

下一次 `CombatSystem.BeginTick(snapshotTick)` 导入到期延迟请求。

Resolve 遇到无效 Victim、Contributor、Source、Target 或静态 Recipe 引用时产生确定性恢复错误，不静默删除。

### 预测结束与表现

预测到基地正式死亡候选后暂停到对应 AuthorityFrame。权威否定则恢复预测，确认则停止在 `GameOverTick + 1`。

表现事件继续使用现有 `PresentationEventId`，结构不修改。

---

### 三、`RollbackFrameSnapshot`

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

### 十三、快照缓冲与未确认锚点

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


## 需求演进

### 2026-10-02

变动内容：明确所有 Tick 表示下一待执行帧，普通回滚不穿越连续权威确认边界。

legacyDecision：D-001

### 2026-10-02

变动内容：一 Tick 一快照，恢复拆为 Restore、Resolve、Rebuild。

legacyDecision：D-004

### 2026-10-02

变动内容：确认金币不主动重演后续商店命令或补建本地已拒绝命令。

legacyDecision：D-006

