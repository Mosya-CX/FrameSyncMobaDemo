# 单调时钟开局与 Tick率独立创作

## 本次执行范围

本计划对应原编码 0136 的一次执行：单调时钟开局与 Tick率独立创作。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [逻辑时钟与 Tick 推进](../../requirements/frame-sync/REQ-FEAT-005_simulation-tick.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Bootstrap/FrameSyncLaunchSchedule.cs`：`FrameSyncLaunchSchedule`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。

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

`Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public sealed class FrameSyncGameRuntime
    {
        private readonly SimulationTickPipeline _pipeline;
        private readonly SimulationTickContextController _tickController;
        private readonly PredictionRollbackCoordinator _rollbackCoordinator;
        private readonly CommandRelayBuffer _commandRelayBuffer;
        private readonly AuthorityRecoveryArchive _authorityRecoveryArchive;
        private readonly AuthorityFrameReplicator _authorityFrameReplicator;
        private readonly AuthorityRecoveryCoordinator _authorityRecoveryCoordinator;
        private PlayerSlotUnitMapping[] _playerSlotMappings =
            Array.Empty<PlayerSlotUnitMapping>();
        private int _naturalGoldIntervalTicks = 15;
        private int _naturalGoldAmount = 2;

        public Unit.UnitWorld UnitWorld { get; }
        public PhysicsWorld PhysicsWorld { get; }
        public Unit.CombatSystem CombatSystem { get; }
        public MatchRuleRuntime MatchRule { get; }
        public GoldIncomeRuntime GoldIncome { get; }

        public Unit.IEquipmentShopView
            CreateEquipmentShopView(
                int playerSlot)
        {
            return new Unit.EquipmentShopView(
                _pipeline.EquipmentShop,
                GoldIncome,
                playerSlot);
        }
        public CommandCollector CommandCollector => _pipeline.CommandCollector;
        public int CurrentTick => _pipeline.LocalSimulationTick;
        public int LastCompletedTick => _pipeline.LocalSimulationTick - 1;
        public int LatestSynchronizedServerTick { get; private set; } = -1;
        public int MinCommandLeadTicks { get; private set; } = 1;
        public int MaxFutureCommandTicks => _pipeline.MaxFutureCommandTicks;
        public uint LastChecksum => _pipeline.LastChecksum;
        public SimulationTickPipeline TickPipeline => _pipeline;
        public PredictionRollbackCoordinator Prediction =>
            _rollbackCoordinator;
        public AuthorityFrameReplicator AuthorityFrames =>
            _authorityFrameReplicator;
        public AuthorityRecoveryCoordinator AuthorityRecovery =>
            _authorityRecoveryCoordinator;

        /// <summary>
        /// Active composition-root runtime that Lua UI pages query. It is set by
        /// the application layer only; deterministic simulation never depends on it.
        /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 5.3, 10.11.
        /// </summary>
        public static FrameSyncGameRuntime Instance { get; private set; }

        public static void RegisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            Instance = runtime;
        }

        public static void UnregisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

**逻辑时钟与 Tick 推进**

ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。

预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameSyncLaunchScheduleTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ClientWait_SubtractsTransitAndPredictionLead`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ClientWait_SubtractsTransitAndPredictionLead()
        {
            const int tickRate = 30;
            const int leadTicks = 5;
            const long sentServerMs = 10_000L;
            const long serverLaunchMs =
                sentServerMs + 5_000L;
            const long receivedServerMs =
                sentServerMs + 1_200L;

            long clientLaunchMs = FrameSyncLaunchSchedule
                .GetClientPredictionLaunchServerTimeMilliseconds(
                    serverLaunchMs,
                    tickRate,
                    leadTicks);

            Assert.That(
                clientLaunchMs - receivedServerMs,
                Is.EqualTo(
                    5_000L -
                    1_200L -
                    leadTicks * 1_000L /
                    tickRate));
        }
```
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `CommandBundle_ProducesStablePerTickReplacementRelays`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CommandBundle_ProducesStablePerTickReplacementRelays()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand first = GameplayCommand.CreateMove(
                Header(unitUid, 3, 1),
                new fp2(fp.one, fp.zero));
            GameplayCommand replacement = GameplayCommand.CreateMove(
                Header(unitUid, 3, 2),
                new fp2((fp)2, fp.zero));
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] relays = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                command => command.ControlledUnitUid == unitUid);

            Assert.AreEqual(1, relays.Length);
            Assert.AreEqual(3, relays[0].TargetTick);
            Assert.AreEqual(1u, relays[0].RelayRevision);
            GameplayCommand[] canonical = relays[0].DecodeCommands();
            Assert.AreEqual(1, canonical.Length);
            Assert.AreEqual(2u, canonical[0].CommandSeq);
            Assert.AreEqual(new fp2((fp)2, fp.zero),
                canonical[0].MoveTargetPoint);

            AcceptedCommandRelay[] duplicate = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                null);
            Assert.AreEqual(0, duplicate.Length);
        }
```
- `Assets/Scripts/FrameSync/Tests/FrameSyncPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `GameplayCommand_CreateMove_WritesCanonicalBytes`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GameplayCommand_CreateMove_WritesCanonicalBytes()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);

            try
            {
                var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
                var cmd = GameplayCommand.CreateMove(
                    CreateHeader(unit.UnitUid, 11, 1),
                    new fp2(fp.one, fp.zero));

                var buffer = new byte[256];
                var writer = new CanonicalByteWriter(buffer);
                cmd.WriteCanonicalBytes(writer);

                Assert.Greater(writer.WrittenCount, 0);
            }
            finally
            {
                controller.EndTick();
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
