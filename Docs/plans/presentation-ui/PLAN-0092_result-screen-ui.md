# 结果界面

## 本次执行范围

本计划对应原编码 0092 的一次执行：结果界面。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/MatchFlowStateMachine.cs`：`MatchFlowStateMachine`。
- `Assets/Scripts/FrameSync/MatchResultSnapshot.cs`：`MatchResultSnapshot`、`MatchStatisticsResult`、`MatchStatisticsResultEntry`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private void Update()
        {
            long nowMilliseconds =
                GetUnityMonotonicMilliseconds();
            long elapsedMilliseconds =
                lastUnityUpdateMonotonicMilliseconds < 0L
                    ? 0L
                    : Math.Max(
                        0L,
                        nowMilliseconds -
                        lastUnityUpdateMonotonicMilliseconds);
            lastUnityUpdateMonotonicMilliseconds =
                nowMilliseconds;
            if (Runtime == null)
                return;
            bool connectedFrameSyncClient =
                UsesNetworkSimulation &&
                !dedicatedServer &&
                frameSyncNetworkBridge != null &&
                frameSyncNetworkBridge.IsBound &&
                frameSyncNetworkBridge.IsConnectedClient;
            if (connectedFrameSyncClient)
            {
                // Warm the RTT estimator throughout the loaded/ready launch
                // barrier. Gameplay remains frozen until its existing launch
                // gates open, but the first Command can use fresh samples.
                frameSyncNetworkBridge.TickPresentationPing(
                    nowMilliseconds);
            }
            if (connectedFrameSyncClient &&
                IsClientGameplayActive())
            {
                frameSyncNetworkBridge.SendLocalCommands();
                recoveryAccumulatorMillisecondRateUnits =
                    checked(
                        recoveryAccumulatorMillisecondRateUnits +
                        elapsedMilliseconds * bakedConfig.TickRate);
                while (recoveryAccumulatorMillisecondRateUnits >=
                       DeterministicTimeConversion
                           .MillisecondsPerSecond)
                {
                    recoveryAccumulatorMillisecondRateUnits -=
                        DeterministicTimeConversion
                            .MillisecondsPerSecond;
                    recoveryControlTick++;
                }
                frameSyncNetworkBridge.TickRecovery(
                    recoveryControlTick);
            }
            if (driveSimulationFromUnityUpdate)
                AdvanceSimulationByElapsedMilliseconds(
                    elapsedMilliseconds);
            PublishAnimationPresentationTime();
            if (hudLaunchPending &&
                Runtime != null &&
                !IsEndpointLaunchTimeReached())
            {
                if (!launchCommitApplied)
                {
                    gameLoadProgress = 0.9f;
                    gameLoadStatus =
                        "Waiting for all players";
                }
                else
                {
                    long loadElapsedMilliseconds = Math.Max(
                        0L,
                        RequireLaunchClock()
                            .MonotonicTimeMilliseconds -
                        loadWaitStartMonotonicMilliseconds);
// 方法后续请阅读上述真实源码；这里是节选。
```

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

### 输入输出与边界

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

**HUD 数值技能与小地图**

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
