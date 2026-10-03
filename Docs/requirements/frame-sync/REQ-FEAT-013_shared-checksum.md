# 共享校验与分段诊断

## 目标实现

不一致可定位到随机、单位、战斗、投射物、商店、物理或金币摘要段。

## 技术方案

SharedGameplayChecksum 规范序列化并按稳定键排序；-checksumDetail 输出分段和逐单位 Handler 摘要。

## 边界情况

诊断不改变 Gameplay，不依赖集合插入顺序；StatSeq、配置 ID 与正式序列规则一致。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：当前关联实现定义 SharedGameplayChecksum、ChecksumSegment、StatEntryField（以源码为实际命名）。
- `Assets/Scripts/FrameSync/WorldDumpBuilder.cs`：当前关联实现定义 WorldDumpBuilder（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Stats/StatHandlerSnapshot.cs`：当前关联实现定义 StatHandlerSnapshot、StatRuntimeEntrySnapshot（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/StatHandlerSnapshotTests.cs`：CaptureRestore_RoundTrip_PreservesAllState、CaptureRestore_AfterModifications_ReturnsToCapturedState、AddModifier_AfterRestoreWithoutRuntimeStatEntry_RecreatesEntry、RollbackReplay_Equivalence、Restore_DoesNotTriggerDirtyRecompute_ValuesMatchSnapshot、Rebuild_MarksAllEntriesDirty、Determinism_SameSequence_SameSnapshot。
- `Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs`：TestAccount_CommandLineOverridesPersistedIdentity、Lobby_RequiresEveryAssignedPlayerAtFullReadyBarrier、VersionHandshake_RejectsAnyCriticalMismatch、ClientFlow_ReachesLobbyOnlyAfterNgoConnects、ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode、ClientFlow_CancelWaitingAssignment_DeletesTicketAndReturnsMain、ClientFlow_CancelWhileTicketCreationIsPending_DeletesLateTicket。
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。
- `Assets/Scripts/FrameSync/Tests/BootstrapDeterminismProbeTests.cs`：ServerFirstTick_MatchesClientPredictionFirstTick。
- `Assets/Scripts/FrameSync/Tests/ChecksumNewStateCoverageTests.cs`：Checksum_ChangesWhenProjectileOnHitOverrideDiffers、Checksum_ChangesWhenPendingLifetimeOverrideDiffers、Checksum_ChangesWhenPassiveAbilityLevelDiffers、Checksum_ChangesWhenMinionThreatRefreshTickDiffers、Checksum_ChangesWhenMinionThreatTableDiffers、Checksum_ChangesWhenEquipmentTriggerCountDiffers、Checksum_ChangesWhenGameplayParticipantIdDiffers。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-08-06

变动内容：强化复仇结束后最多补一个普通 Buff；分段 checksum 和 StatSeq 排序便于定位。

legacyDecision：D-032

### 2026-08-13

变动内容：异步诊断队列有界并可编译开关，不改变确定性输出。

legacyDecision：D-043

