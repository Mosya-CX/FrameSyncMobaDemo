# 命令合并转发与幂等重发

## 目标实现

同一输入重复送达时不产生第二个 Gameplay 事实。

## 技术方案

CommandCollector 保持 TargetTick、CommandSeq 与来源身份，GameplayCommandBundle 和 AcceptedCommandRelay 保留规范顺序。

## 边界情况

已执行预测 Tick 收到 Relay 时走正常脏 Tick 纠错；相同标识但不同字节必须报冲突。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/AuthorityReplication.cs`：当前关联实现定义 GameplayCommandBundle、AcceptedCommandRelay、CommandRelayBuffer、TickRelayState、AuthorityRecoveryArchive（以源码为实际命名）。
- `Assets/Scripts/FrameSync/CommandCollector.cs`：当前关联实现定义 CommandCollector、CommandMergeKey、UseItemMergeKey、GameplayCommandCanonicalComparer（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayCommandSendLedgerTests.cs`：UnchangedCollector_BuildsOnlyOneReliableBundleCandidate、AdjacentToggleInputs_SendTheirDistinctCommandSequences、RebuiltCollector_DoesNotResendSuccessfulIdentity。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientFrameworkSmokeSceneTests.cs`：ClientFixture_BindsAssignedUnitAndAdvances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：HoldReleaseDefault_RightClickMoveThenLeftClickCommitsOnce、UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands、LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim、ToggleNoAim_SimulatedWPressCommitsImmediately、VarusWThenQ_PendingFocusKeepsIndicatorAndBothCommands。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 本地采集

```text
输入意图
    -> CommandCollector
    -> 构建目标 Tick Command
    -> CommandMergePolicy
    -> 发送 GameplayCommandBundle
    -> 写入本地预测 Command Buffer
```

预测暂停时仍然采集和发送 Command，但暂不执行新的本地 Gameplay Tick。

### 合并规则

| 命令 | 规则 |
|---|---|
| `Move` | 同玩家、单位和 TargetTick 只保留最后一条 |
| `Attack` | 同玩家、单位和 TargetTick 只保留最后一条 |
| `CastAbility / CancelAbility` | 按技能输入规则合并 |
| `AllocateAbilitySkillPoint` | 不合并，按 CommandSeq 保留 |
| `EquipmentShop` | 不合并，按 CommandSeq 保留 |
| `SwapEquipmentSlot` | 不合并，按 CommandSeq 保留 |
| `UseItem` | 同槽位同 Tick 通常保留最后一条 |

重复网络包按 `ClientId + CommandSeq` 去重，不能按 Payload 去重。

### 规范顺序

```text
TargetTick
PlayerSlot
ControlledUnitUid
CommandSeq
```

服务端必须反序列化后再次执行相同合并和排序，再生成权威字节。

### `GameplayCommandBundle`

```text
GameplayCommandBundle
    ClientId
    BundleSequence
    SendLocalTick
    MinTargetTick
    MaxTargetTick
    CommandCount
    CanonicalCommandBytes
```

### `AcceptedCommandRelay`

```text
AcceptedCommandRelay
    TargetTick
    RelayRevision
    CanonicalCommandBytesForTick
```

Relay 是该 Tick 当前命令集合的完整替换版本。

客户端预测帧保存：

```text
PredictedRelayRevision
PredictedCanonicalCommandBytes
```

AuthorityFrame 携带 `FinalCommandRevision`。

### 已执行预测 Tick 的 Relay 更新

```text
Relay.TargetTick >= LocalSimulationTick
    -> 直接替换未来预测 Command。

Relay.TargetTick < LocalSimulationTick
    -> EarliestDirtyPredictionTick =
       min(当前值, Relay.TargetTick)。
```

同一 Unity Update 内收到多个已执行 Tick 的 Relay 更新时，在网络包处理结束后只从最早 Dirty Tick 回滚一次。

---


## 需求演进

### 2026-10-02

变动内容：权威帧必须校验完整规范命令字节及共享校验，包含金币批次摘要。

legacyDecision：D-002

