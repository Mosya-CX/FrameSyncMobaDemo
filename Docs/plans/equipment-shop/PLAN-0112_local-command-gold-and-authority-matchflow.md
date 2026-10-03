# 本地命令金币与权威比赛流程

## 本次执行范围

本计划对应原编码 0112 的一次执行：本地命令金币与权威比赛流程。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [金币批次确认与可用余额](../../requirements/equipment-shop/REQ-FEAT-057_gold-accounting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [卖出撤销与交易失效](../../requirements/equipment-shop/REQ-FEAT-056_shop-undo.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [自适应命令目标 Tick](../../requirements/frame-sync/REQ-FEAT-009_command-timing.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/UnitIntent.cs`：`IntentKind`、`UnitIntent`。
- `Assets/Scripts/FrameSync/CommandCollector.cs`：`CommandCollector`、`CommandMergeKey`、`UseItemMergeKey`、`GameplayCommandCanonicalComparer`。
- `Assets/Scripts/FrameSync/GameplayCommand.cs`：`CommandHeader`、`GameplayCommandIdentity`、`AbilityCancelReason`、`EquipmentShopCommandOperationType`、`GameplayCommand`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/FrameSync/MatchFlowStateMachine.cs`：`MatchFlowStateMachine`。
- `Assets/Scripts/Gameplay/Unit/Core/Order.cs`：`OrderKind`、`Order`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/NaturalGoldIncomeSystem.cs`：`NaturalGoldIncomeSystem`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/UnitIntent.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §3.2 — the unit's current long-term goal.
    /// Intent persists across ticks until the goal is achieved, cleared, or replaced.
    /// It is NOT the current action, NOT the current Runtime, and NOT the current Handler state.
    /// </summary>
    public enum IntentKind : byte
    {
        /// <summary>No active intent; unit idles.</summary>
        None,

        /// <summary>Attack a specific target unit.</summary>
        AttackTarget,

        /// <summary>Move to a world position.</summary>
        MoveToPosition,

        /// <summary>Cast a specific ability (may include target).</summary>
        CastAbility,

        /// <summary>Minion lane advance behavior.</summary>
        LaneAdvance,

        /// <summary>Monster return-to-camp behavior.</summary>
        ReturnToCamp,
    }

    /// <summary>
    /// Unit Framework v27.3 §3.2 — the unit's current long-term goal.
    /// Does NOT store: whether the unit can currently attack/cast, resource
    /// reservations, remaining windup ticks, movement task handles, or
    /// crowd-control override state.
    /// </summary>
    public struct UnitIntent
    {
        /// <summary>The kind of long-term goal.</summary>
        public IntentKind Kind;

        /// <summary>
        /// When Kind is AttackTarget: the target unit's UID.
        /// When Kind is CastAbility with a unit target: the target unit.
        /// </summary>
        public UnitUid TargetUnit;

        /// <summary>
        /// When Kind is MoveToPosition or CastAbility with a ground target:
        /// the world-space target position.
        /// </summary>
        public fp2 TargetPosition;

        /// <summary>When Kind is CastAbility: the ability definition ID.</summary>
        public int AbilityId;

        /// <summary>When Kind is CastAbility: the existing Ability signal verb.</summary>
        public AbilitySignalVerb AbilityVerb;

        /// <summary>When Kind is CastAbility: the existing canonical Ability aim.</summary>
        public AimSnapshot AbilityAim;

        /// <summary>
        /// When true, the planner may generate chase MoveActionRequests
        /// if the unit is out of range for Attack/Cast.
        /// </summary>
        public bool AllowChase;

        /// <summary>
        /// When true, the planner may replan (switch to a different action)
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/CommandCollector.cs`：

```csharp
        public void BeginTick(int targetTick)
        {
            bool hadCommands = CommandCount > 0;
            moveCommands.Clear();
            attackCommands.Clear();
            useItemCommands.Clear();
            nonMergedCommands.Clear();
            if (hadCommands)
                MarkContentChanged();
        }
```

### 输入输出与边界

**金币批次确认与可用余额**

GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。

Account 不保存第二局内金币累计；派生余额不进 Snapshot；T 收入确认不主动回滚或补造本地已拒绝 Command；金币生产者待确认项单列。

**死亡奖励与贡献窗口**

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

**卖出撤销与交易失效**

OperationLog 与 UndoableOperationStack 维护 EffectiveShopGoldDelta；出售和撤销出售属于商店增量，不产生 GoldIncome 记录。

离开范围、参与战斗、使用装备等永久失效规则必须准确；金币确认不扫描后续 Purchase/Undo，也不创建金币专用脏 Tick。

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/FrameSync/Tests/LocalCommandGoldMatchFlowTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `FutureCommand_IsRetainedAndConsumedOnlyAtTargetTick`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FutureCommand_IsRetainedAndConsumedOnlyAtTargetTick()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 200, UnitKind.Hero);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld)
            {
                MaxFutureCommandTicks = 6,
            };
            var command = GameplayCommand.CreateMove(
                new CommandHeader(
                    1,
                    10,
                    0,
                    unit.UnitUid,
                    2,
                    GameplayCommandKind.Move,
                    0,
                    0),
                new fp2(6, 0));
            pipeline.SubmitCommand(command);
            var controller = new SimulationTickContextController();

            pipeline.ExecuteTick(controller);
            pipeline.ExecuteTick(controller);

            Assert.That(unit.MovementHandler.Position,
                Is.EqualTo(fp2.zero));
            Assert.That(pipeline.CommandCollector.CommandCount, Is.EqualTo(1));

            pipeline.ExecuteTick(controller);

            Assert.That(unit.MovementHandler.Position.x,
                Is.GreaterThan(fp.zero));
            Assert.That(pipeline.CommandCollector.CommandCount, Is.Zero);
        }
```
- `Assets/Scripts/FrameSync/Tests/MatchFlowStateMachineTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `MatchFlow_InitialState_Preparing`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MatchFlow_InitialState_Preparing()
        {
            var rule = new MatchRuleRuntime(60);
            var flow = new MatchFlowStateMachine(rule);
            Assert.That(flow.HasFinished, Is.False);
            Assert.That(flow.AcceptsGameplayCommands, Is.False);
            Assert.That(flow.Result.WinningTeamId, Is.EqualTo(TeamId.Neutral));
        }
```
- `Assets/Scripts/PlayerInput/Tests/PlayerCommandRequesterTests.cs`：EditMode，程序集 `FrameSyncMoba.PlayerInput.Tests`，函数 `EventBuffer_AssignsStableSequenceAndRejectsOverflow`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EventBuffer_AssignsStableSequenceAndRejectsOverflow()
        {
            var buffer = new LocalInputEventBuffer();
            for (int i = 0; i < LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame; i++)
            {
                Assert.IsTrue(buffer.Push(
                    LocalGameplayInputEventKind.AbilityKeyPressed,
                    (byte)(i % 4),
                    new Vector2(i, i)));
            }
            Assert.IsFalse(buffer.Push(
                LocalGameplayInputEventKind.PrimaryClick, 0, Vector2.zero));

            ulong previous = 0;
            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                Assert.Greater(inputEvent.LocalEventSequence, previous);
                previous = inputEvent.LocalEventSequence;
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
