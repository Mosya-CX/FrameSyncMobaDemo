# 开局载荷与运行时初始化

## 本次执行范围

本计划对应原编码 0121 的一次执行：开局载荷与运行时初始化。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [应用启动与跨场景流程](../../requirements/match-flow/REQ-FEAT-001_application-flow.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/PlayerSlot.cs`：`PlayerSlot`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：`FrameSyncSettingsAuthoring`、`CriticalDataVersionsAuthoring`、`GameModeConfigAuthoring`、`PhysicsSettingsAuthoring`、`UnitSettingsAuthoring`、`BakedGlobalGameplayData`、`GlobalGameplayData`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        public void ApplyGameStartConfig(
            in GameStartConfig config)
        {
            config.ValidateOrThrow();
            throw new InvalidOperationException(
                "GameStartConfig alone is insufficient. Apply the complete GameBootstrapPayload.");
        }
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

**应用启动与跨场景流程**

GameApplicationFlowManager 管理逻辑状态，GameSessionContext 负责跨场景交接；NGO 根保持单一生命周期。客户端 ClientBootstrap、服务器 ServerBootstrap 均依次进入 Lobby、GameScene。

本地直连与 UOS 在线各有唯一连接生命周期负责人；模式变化不能让两套回调同时通知连接。

**按对局加载内容闭包**

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/UnitUidTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SameComponents_ProduceEqualIdentity`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SameComponents_ProduceEqualIdentity()
        {
            var first = new UnitUid(1200, 1001, 7);
            var second = new UnitUid(1200, 1001, 7);

            Assert.That(first.SpawnLogicTick, Is.EqualTo(1200));
            Assert.That(first.RuntimeEntityPrefabId, Is.EqualTo(1001));
            Assert.That(first.SpawnSequenceInTick, Is.EqualTo(7));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
```
- `Assets/Scripts/FrameSync/Tests/GoldIncomeRuntimeContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 500);

            runtime.BeginTick(0);
            GoldIncomeRecordBatch empty = runtime.SealTick(0);
            Assert.AreNotEqual(0UL, empty.Digest.Value);
            Assert.AreEqual(0, empty.Records.Length);
            runtime.ConfirmAcceptedTick(0);

            runtime.BeginTick(1);
            runtime.RequestGoldIncome(1, 25, GoldIncomeReason.UnitKill);
            GoldIncomeRecordBatch income = runtime.SealTick(1);
            Assert.AreEqual(0, income.Records[0].IncomeSequenceInTick);
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => runtime.ConfirmAcceptedTick(2));
            runtime.ConfirmAcceptedTick(1);
            Assert.AreEqual(525, runtime.GetConfirmedAvailableGold(1));
        }
```
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `MinionWave_ExpandsCanonicalTeamLaneMemberOrder`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MinionWave_ExpandsCanonicalTeamLaneMemberOrder()
        {
            var schedule = new BakedMinionWaveConfig(
                30,
                0,
                new[]
                {
                    new MinionWavePhase
                    {
                        StartWaveIndex = 0,
                        CompositionCycle = new[]
                        {
                            new MinionWaveComposition
                            {
                                Members = new[]
                                {
                                    new MinionWaveMember
                                    {
                                        UnitPrototypeId = 20,
                                        Count = 2,
                                        FirstSpawnOffsetTicks = 5,
                                        SpawnStepTicks = 1,
                                    },
                                },
                            },
                        },
                    },
                });
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        new fp2(1, 2),
                        new fp2(1, 0)),
                    new LaneTeamSpawnData(
                        new TeamId(2),
                        new fp2(9, 2),
                        new fp2(-1, 0)),
                },
                new[] { fp2.zero, new fp2(10, 0) },
                (fp)2m);
            var system = new MinionSystem(
                new UnitWorld(),
                schedule,
                new[] { lane });
            BeginTick(0);

            system.TickLogic();
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
