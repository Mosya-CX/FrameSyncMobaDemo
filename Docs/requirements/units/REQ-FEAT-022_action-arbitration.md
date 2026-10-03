# 动作仲裁与固定执行器

## 目标实现

移动、技能、普攻等并发申请按固定资源矩阵被接受或拒绝。

## 技术方案

ActionArbiter 输出类型化 ActionSubmitResult；固定 Main/Base 槽位承载资源占用，Runtime 拥有执行状态。移除旧 action 列表与反射式申请。

## 边界情况

按仲裁资源矩阵判断并发；Snapshot 保存 Main/Base 状态，reservation 由槽位派生；运行时不保存“被接受申请”的第二份权威。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Unit/Core/ActionArbiter.cs`：当前关联实现定义 ActionArbiter（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/ActionContracts.cs`：当前关联实现定义 ActionResource、ActionSlot、ActionInterruptLevel、ActionRuntimePhase、ActionSubmitOutcome（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/ActionArbiterConcurrencyTests.cs`：LockedMainCast_AllowsAuthoredDashInBaseSlot、MovableHold_AllowsMove_ReleasePreemptsMove、HoldTimeout_ReconcilesReleaseResourcesAndRestores、SameAbilityStageTransition_MigratesMainToBaseSlot、AutomaticDashTransition_MigratesWithoutCancellingSession、SequentialRecastWindow_ReleasesMainRuntimeButKeepsSession、Planner_DoesNotResubmitEquivalentActiveMove。
- `Assets/Scripts/FrameSync/Tests/SnapshotChecksumCompletenessTests.cs`：AggregateSnapshot_RestoresIntentDashAndLocomotion、SharedChecksum_ChangesForIntentDashAndLocomotionState、CombatModifierCapture_IsCanonicalAndDetachRepairsShiftedIndices、AggregateSnapshot_CapturesLiveActionRuntime、ExecuteTick_FormalDeathInvalidationCapturesRestorableBoundary、SharedChecksum_SerializesEveryActionRuntimeSlotMember、Restore_RejectsActionRuntimeWithoutOwningHandlerState。
- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_ClearsChaseRouteAndAttackIntentBeforeSnapshot、DespawnTarget_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_DoesNotRevokeCommittedAttack、DespawnTarget_DoesNotRevokeCommittedAttack、FormalDeathInvalidation_RejectsNonIncreasingSequence、BeginAndCancel_DoNotConsumeSequence。
- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：Add_CreatesIndependentInstances_NoMerge、Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh、Cleanse_RemovesMatchingNonHigh_RespectsCount、Unstoppable_SuppressesOutput_AndRejectsForcedMove、DamageTakenSignal_RemovesSleepInstance、Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration、Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。

### 动作资源与槽位的现行矩阵

普通链只有 Order/AI → UnitIntent → Planner → ActionRequest → Arbiter.Submit → Main/Base Runtime → 所属 Handler。Planner 每单位每 Tick 最多一个临时申请，不直接开始、取消或重置 Handler；缺 Planner/Arbiter 的可命令单位明确失败，不直达 Handler。CancelAbility 也经过仲裁。控制强制位移仍由 CrowdControl → MovementHandler，不是 ActionRuntime。

| 申请/阶段 | 槽位 | 资源 | 中断与额外规则 |
|---|---|---|---|
| 自主或控制路线移动 | Base | BaseAction、Movement、Facing | 可中断；锁移动阶段先拒绝自主移动 |
| 普攻 Commit 前摇 | Main | MainAction、Attack、Facing | 可中断；Commit 释放 Main，后摇留在 AttackHandler |
| 普通技能 Stage | Main | MainAction、Ability；LockMovement 时加 Facing | 按作者配置；不占 Movement 来阻止已配置特殊移动 |
| Dash Stage | Base | BaseAction、Movement | 按作者配置；可与 Main cast 并行并保持 Main 锁定朝向 |
| 连续再施法等待窗 | 无 | 无 | Session 活着，直到下一合法 Commit 才重新占 Main |
| 纯 Toggle 启用/保持/关闭 | 无 | 无 | 不主动施法、不抢占、不打断其他动作 |

同槽或资源交集为冲突；只在活动 Runtime 可中断或新请求有更强正式 InterruptLevel 时抢占。同技能推进是继续，不是自我抢占；Handler 拒绝不得生成 token。每 Handler advance 后重新描述 Stage，更新资源并可 Main/Base 迁移，不自发取消同 Session。自动切换要求的新资源若遇不可中断冲突，是无效作者配置，必须失败。

强制行为 Move/Attack 仅绕过粗粒度自主 Capability veto；仍需对应 AbilityMask、目标合法、ready/range 和细粒度 ControlMove/ControlAttack。控制攻击保留 IsControlAction，使 VoluntaryAttack 阻断不会单独取消其前摇。强制行为使用 Forced 中断级，不被普通 cast 的自主移动锁拒绝。

### 现行快照字段和恢复

Main 与 Base 各保存以下字段，按此规范次序参与 checksum：IsOccupied、Slot、Kind、Phase、OccupiedResources、Interruptible、BlocksVoluntaryMove、IsControlAction、TargetUnitUid、AbilitySlot。请求、trace、派生 reservation、Tick 副本、Handler timer、aim/route/Stage 副本不重复保存。

Restore 校验 enum、空槽形状和精确 Move/Attack 矩阵，不执行 start/cancel callback；Cast Resolve 必须匹配已恢复作者 Stage 的资源与锁。Move 无 locomotion task、Attack 无目标或未提交前摇、Cast 无 Session/action-active Stage 均可见失败。Rebuild 不派生新 Gameplay 权威状态；死亡、复活、回池清空两槽。

原仲裁修订曾使用 GameplaySnapshot schema 23、Bootstrap wire 4；动作身份修订记录 GameplaySnapshot 24、GameplayDataVersion 4，Bootstrap wire 4 保留，旧 wire 3 在头部拒绝。当前工作树由于暂缓的三狼改造已有 schema 25；这属于部分实现待验收状态，需按三狼计划复核字段与重演，不能宣称已验证完成。


## 需求演进

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

