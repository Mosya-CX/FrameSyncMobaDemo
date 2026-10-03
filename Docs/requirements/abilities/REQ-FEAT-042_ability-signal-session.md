# 技能信号与会话状态

## 目标实现

Focus、Commit、Cancel 等信号只通过技能门面进入单次施法状态。

## 技术方案

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

## 边界情况

HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Ability/AbilityCastView.cs`：当前关联实现定义 AbilityCastView（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：当前关联实现定义 AbilityHandler、PassiveEventKind、AbilityHandlerSnapshot、AbilityBook、AbilitySlotRuntime（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Ability/AbilityRuntime.cs`：当前关联实现定义 AbilitySession、AbilityRuntime、AbilityRuntimeSnapshot、AbilitySessionSnapshot（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：当前关联实现定义 AbilitySignal、AbilitySignalVerb、AimKind、AimSnapshot（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。
- `Assets/Scripts/Gameplay/Tests/AbilityAimStageTests.cs`：AreaDamageStage_CentersOnAimTargetPoint、AreaDamageStage_DoesNotDamageEnemyStructure、SpawnProjectileStage_FiresTowardAimDirection。
- `Assets/Scripts/Gameplay/Tests/AbilityCostAndCastTests.cs`：SessionStartCost_UsesLevelResourceAndHealth、FailedStage_DoesNotConsumeCost、HoldRelease_FirstCommitPaysOnceAndStoresAim、InvalidAimRange_ConsumesNothing、Snapshot_PreservesFirstCommitPayment、Toggle_FirstCommitActivatesAndSecondCommitTurnsOff、Toggle_TurnOffDoesNotStartCooldown。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/VarusAnimationPlayModeTests.cs`：BoundDriver_QFocusMovementChangesResolveLoopSameFrame。
- `Assets/Scripts/Gameplay/Tests/ChargeAbilityTests.cs`：ChargeStage_RatioIncreasesWithElapsedTicks、ChargeStage_SelfSlowAppliesWhileChargingAndRemovesOnRelease、ChargeStage_TimeoutCancelsAndRefundsHalfCost、ChargeStage_ConsumesActiveToggleAndStartsCooldown、ChargeProjectileStage_InterpolatesDamageRangeAndOverride、ChargeProjectile_SnapshotRoundTrip_PreservesOverride。

## 细化功能与技术附录

- [技能信号接受与施法结束](REQ-FEAT-042-PART-1_signal-cast-completion.md)：接口、数据流、公式、边界与配置。
- [技能本地指示器查询](REQ-FEAT-042-PART-2_ability-indicator-query.md)：接口、数据流、公式、边界与配置。
- [技能界面查询与单位事件](REQ-FEAT-042-PART-3_ability-ui-events.md)：接口、数据流、公式、边界与配置。
- [技能目录运行时与会话归属](REQ-FEAT-042-PART-4_catalog-session-ownership.md)：接口、数据流、公式、边界与配置。
- [技能状态快照与统一Tick](REQ-FEAT-042-PART-5_ability-snapshot-tick.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-10-02

变动内容：输入模式从施法模型离线派生，不重复 Gameplay 配置。

legacyDecision：D-016

### 2026-10-02

变动内容：松键和左键合并为一次 Commit，右键不取消蓄力。

legacyDecision：D-017

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

