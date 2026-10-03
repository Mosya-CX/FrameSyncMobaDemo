# 对局流程启动

## 本次执行范围

本计划对应原编码 0090 的一次执行：对局流程启动。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [应用启动与跨场景流程](../../requirements/match-flow/REQ-FEAT-001_application-flow.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [比赛结束与全端统计](../../requirements/match-flow/REQ-FEAT-016_match-statistics.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/MatchFlowStateMachine.cs`：`MatchFlowStateMachine`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/MatchResultSnapshot.cs`：`MatchResultSnapshot`、`MatchStatisticsResult`、`MatchStatisticsResultEntry`。
- `Assets/Scripts/FrameSync/MatchRuleRuntime.cs`：`MatchPhase`、`MatchEndReason`、`MatchTopologyRole`、`MatchStatisticsEntry`、`MatchStatisticsRuntimeSnapshot`、`MatchStatisticsRuntime`、`StatisticKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/MatchFlowStateMachine.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Observes MatchRuleRuntime from the bootstrap layer, gates Gameplay
    /// commands during PreGame/Countdown, and captures the final
    /// MatchResultSnapshot after the authority owner finishes the match.
    ///
    /// Design: FrameSync_Flow_Integrated_System_Design_v10_2 sections 2, 14
    /// </summary>
    public sealed class MatchFlowStateMachine
    {
        private readonly MatchRuleRuntime _rule;
        private bool _resultCaptured;

        /// <summary>
        /// True when the match has reached the Finished phase
        /// and the result is available.
        /// </summary>
        public bool HasFinished => _rule.CurrentPhase == MatchPhase.Finished;

        /// <summary>
        /// The final match result. Only valid when HasFinished is true.
        /// </summary>
        public MatchResultSnapshot Result { get; private set; }

        public MatchFlowStateMachine(MatchRuleRuntime rule)
        {
            _rule = rule ?? throw new System.ArgumentNullException(nameof(rule));
        }

        /// <summary>
        /// Observes a Tick already advanced by SimulationTickPipeline.
        /// This application-layer object never performs authority evaluation.
        /// </summary>
        public void ObserveTick()
        {
            if (_rule.CurrentPhase == MatchPhase.Finished && !_resultCaptured)
            {
                Result = new MatchResultSnapshot
                {
                    WinningTeamId = _rule.WinningTeamId,
                    EndReason = _rule.EndReason,
                    GameOverTick = _rule.GameOverTick,
                    FinishTick = _rule.FinishTick,
                    Statistics = CaptureStatistics(),
                };
                _resultCaptured = true;
            }
        }

        /// <summary>
        /// Whether the current phase allows Gameplay commands.
        /// Commands are gated during Preparing and Countdown.
        /// </summary>
        public bool AcceptsGameplayCommands =>
            _rule.CurrentPhase == MatchPhase.Running ||
            _rule.CurrentPhase == MatchPhase.Ending;

        private MatchStatisticsResult CaptureStatistics()
        {
            var entries = _rule.Statistics?.Entries;
            if (entries == null) return default;

            var result = new MatchStatisticsResult
            {
                Entries = new MatchStatisticsResultEntry[entries.Count],
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private async void Start()
        {
            try
            {
                await initializationTask;
                if (!UsesNetworkSimulation) return;
                Debug.Log(
                    $"[GB] Start role={dedicatedServer} managed=" +
                    $"{GameSessionContext.FlowManagedExternally} " +
                    $"mode={GameSessionContext.FlowMode}");
                BindFrameSyncNetworkRuntime();
                if (GameSessionContext.FlowManagedExternally)
                {
                    Debug.Log(
                        "[GB] External flow start: " +
                        (dedicatedServer
                            ? "server"
                            : "client"));
                    HandleExternalFlowStart();
                    return;
                }
                if (localDevelopmentNetworkFlow)
                    return;
                if (dedicatedServer)
                    await ApplicationFlow.DedicatedServer.BootAsync();
                else
                {
                    await ApplicationFlow.Client
                        .InitializeAccountAsync(
                            Environment.GetCommandLineArgs());
                    ClientAccountSession session =
                        ApplicationFlow.Client
                            .AccountSession;
                    GameFlowLuaBridge.AccountDisplayName =
                        session.TestAccountId;
                    uiManager?.RefreshLuaHost(
                        UIPageId.Main);
                }
            }
            catch (Exception exception)
            {
                driveSimulationFromUnityUpdate = false;
                Debug.LogException(exception, this);
            }
        }
```

### 输入输出与边界

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

**应用启动与跨场景流程**

GameApplicationFlowManager 管理逻辑状态，GameSessionContext 负责跨场景交接；NGO 根保持单一生命周期。客户端 ClientBootstrap、服务器 ServerBootstrap 均依次进入 Lobby、GameScene。

本地直连与 UOS 在线各有唯一连接生命周期负责人；模式变化不能让两套回调同时通知连接。

**按对局加载内容闭包**

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

**比赛结束与全端统计**

MatchRuleRuntime 管理阶段与预测结束候选；MatchStatisticsRuntime 在所有模拟端消费 FormalDeathResult。

预测结果不先落为最终结果；统计不只在 Dedicated Server 执行；账户持久化不反写 Gameplay。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/FrameSync/Tests/MatchTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `InitialTopology_RegistersTwoStructureBasesInStableRoles`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InitialTopology_RegistersTwoStructureBasesInStableRoles()
        {
            var world = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
            ConfigureBase(world, 101);
            ConfigureBase(world, 102);
            var rule = new MatchRuleRuntime(3);
            rule.BeginCountdown(0, 0);
            var pipeline = new SimulationTickPipeline(
                world,
                world.PhysicsWorld)
            {
                MatchRule = rule,
            };
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    101,
                    GameplayParticipantId.InitialSpawn(101),
                    new TeamId(1),
                    new fp2(-10, 0),
                    new fp2(1, 0)),
                MatchTopologyRole.BlueBase);
            pipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    102,
                    GameplayParticipantId.InitialSpawn(102),
                    new TeamId(2),
                    new fp2(10, 0),
                    new fp2(-1, 0)),
                MatchTopologyRole.RedBase);
            var controller =
                new SimulationTickContextController();

            pipeline.ExecuteTick(
                controller,
                ExecutionMode.ClientPrediction);

            Assert.That(
                rule.BlueBaseUnitUid.IsValid(),
                Is.True);
            Assert.That(
                rule.RedBaseUnitUid.IsValid(),
                Is.True);
            Assert.That(
                rule.BlueBaseUnitUid,
                Is.Not.EqualTo(
                    rule.RedBaseUnitUid));
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
