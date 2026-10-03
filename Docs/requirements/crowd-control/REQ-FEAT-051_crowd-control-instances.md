# 控制实例与模块参数

## 目标实现

配置型控制通过静态模块表和有界参数块执行。

## 技术方案

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

## 边界情况

不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlDefinition.cs`：当前关联实现定义 CrowdControlParamAuthoring、CrowdControlModuleAuthoring、CrowdControlDefinition、ControlTagBits（以源码为实际命名）。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：当前关联实现定义 CrowdControlHandler（以源码为实际命名）。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlInstance.cs`：当前关联实现定义 CrowdControlInstance（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：Add_CreatesIndependentInstances_NoMerge、Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh、Cleanse_RemovesMatchingNonHigh_RespectsCount、Unstoppable_SuppressesOutput_AndRejectsForcedMove、DamageTakenSignal_RemovesSleepInstance、Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration、Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/UnitAnimationAssetTests.cs`：FullMatchAnimationFixtures_HaveCompleteBindableControllers、RuntimeUnits_HaveCompleteRuntimeComposition、FormalAnimatedUnits_BindAttackAndMovePlaybackToGameplayStats、FormalStructures_HaveNoAttackAnimation。
- `Assets/Scripts/Gameplay/Tests/ActionArbiterConcurrencyTests.cs`：LockedMainCast_AllowsAuthoredDashInBaseSlot、MovableHold_AllowsMove_ReleasePreemptsMove、HoldTimeout_ReconcilesReleaseResourcesAndRestores、SameAbilityStageTransition_MigratesMainToBaseSlot、AutomaticDashTransition_MigratesWithoutCancellingSession、SequentialRecastWindow_ReleasesMainRuntimeButKeepsSession、Planner_DoesNotResubmitEquivalentActiveMove。
- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_ClearsChaseRouteAndAttackIntentBeforeSnapshot、DespawnTarget_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_DoesNotRevokeCommittedAttack、DespawnTarget_DoesNotRevokeCommittedAttack、FormalDeathInvalidation_RejectsNonIncreasingSequence、BeginAndCancel_DoNotConsumeSequence。

## 细化功能与技术附录

- [控制处理器装配与生命周期](REQ-FEAT-051-PART-1_control-handler-lifecycle.md)：接口、数据流、公式、边界与配置。
- [控制实例增删与信号查询](REQ-FEAT-051-PART-2_control-instance-signals.md)：接口、数据流、公式、边界与配置。
- [控制定义Bake与模块执行](REQ-FEAT-051-PART-3_control-bake-execution.md)：接口、数据流、公式、边界与配置。
- [控制实例身份与参数块](REQ-FEAT-051-PART-4_control-identity-parameters.md)：接口、数据流、公式、边界与配置。
- [标准控制效果与复合实例](REQ-FEAT-051-PART-5_standard-control-effects.md)：接口、数据流、公式、边界与配置。
- [控制数字规则与快照恢复](REQ-FEAT-051-PART-6_control-snapshot-rules.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

