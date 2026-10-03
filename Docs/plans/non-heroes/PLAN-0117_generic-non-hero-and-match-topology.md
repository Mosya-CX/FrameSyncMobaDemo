# 通用非英雄与比赛拓扑

## 本次执行范围

本计划对应原编码 0117 的一次执行：通用非英雄与比赛拓扑。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [比赛结束与全端统计](../../requirements/match-flow/REQ-FEAT-016_match-statistics.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [小兵波次与兵线 AI](../../requirements/non-heroes/REQ-FEAT-069_minion-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：`JungleCampSpawnSlot`、`JungleCamp`。
- `Assets/Scripts/Gameplay/NonHero/LaneAuthoring.cs`：`LaneTeamSpawnAuthoring`、`LaneTeamSpawnData`、`LaneRuntimeData`、`LaneAuthoring`。
- `Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：`MinionSystem`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Team/TeamId.cs`：`TeamId`。
- `Assets/Scripts/FrameSync/MatchRuleRuntime.cs`：`MatchPhase`、`MatchEndReason`、`MatchTopologyRole`、`MatchStatisticsEntry`、`MatchStatisticsRuntimeSnapshot`、`MatchStatisticsRuntime`、`StatisticKind`。
- `Assets/Scripts/Gameplay/Unit/Core/Order.cs`：`OrderKind`、`Order`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitWorld
    {
        private readonly UnitRegistry registry = new UnitRegistry();
        private readonly UnitPoolRegistry poolRegistry =
            new UnitPoolRegistry();
        private readonly List<UnitAIController> aiControllers = new List<UnitAIController>();
        private readonly List<JungleCamp> jungleCamps =
            new List<JungleCamp>();
        private bool isTickingAIControllers;
        private int currentSequenceLogicTick = -1;
        private byte nextSpawnSequenceInTick;
        private bool spawnSequenceExhausted;
        private int runtimeRevision;

        public GlobalUnitPrototypeTable UnitPrototypeTable { get; set; }
        public UnitDisposePolicyTable DisposePolicyTable { get; set; }
        public GlobalPrefabTable GlobalPrefabTable { get; set; }
        public StatDefinitionTable StatDefinitionTable { get; set; }
        public EquipmentDatabase EquipmentDatabase { get; set; }
        public AbilityDefinitionRegistry AbilityDefinitions { get; set; }
        public BuffDefinitionRegistry BuffDefinitions { get; set; }
        public CrowdControlDefinitionRegistry CrowdControlDefinitions { get; set; }
        public PhysicsWorld PhysicsWorld { get; set; }
        public fp StatGrowthC { get; set; }
        public fp StatGrowthD { get; set; }
        public fp MoveSpeedToLogicVelocityScale { get; set; } = (fp)0.01m;
        public fp StatDistanceToLogicDistanceScale { get; set; } = (fp)0.01m;
        public int TickRate { get; set; } = 30;
        public int AttackSequenceResetIntervalTicks { get; set; } = 90;
        /// <summary>
        /// Units with AttackRange strictly above this value are treated as
        /// ranged. Baked from GlobalGameplayData.UnitSettings.
        /// </summary>
        public int RangedAttackRangeThreshold { get; set; } = 275;
        public RespawnTimer RespawnTimer { get; set; }
        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public PathGridMap2D PathGrid { get; set; }
        public DynamicNavigationFrame DynamicNavigation { get; private set; }
        public FlowFieldRegistry FlowFieldRegistry { get; set; }
        public IMovementCollisionResolver
            MovementCollisionResolver { get; set; }
        public CombatSystem CombatSystem { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public RangeQueryService RangeQuery { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public IReadOnlyList<UnitAIController> AIControllers => aiControllers;
        public IReadOnlyList<JungleCamp> JungleCamps => jungleCamps;
        public MinionSystem MinionSystem { get; set; }
        public int RuntimeRevision => runtimeRevision;
        public UnitPoolRegistry PoolRegistry => poolRegistry;

        public void RebuildDynamicNavigation(
            int logicTick)
        {
            if (PathGrid == null)
            {
                DynamicNavigation = null;
                return;
            }
            if (DynamicNavigation == null ||
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：

```csharp
        public void Rebuild(
            in RollbackContext context)
        {
        }
```

### 输入输出与边界

**AI 注册调度与多态快照**

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

**稳定 UID 与参与者身份**

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。

**比赛结束与全端统计**

MatchRuleRuntime 管理阶段与预测结束候选；MatchStatisticsRuntime 在所有模拟端消费 FormalDeathResult。

预测结果不先落为最终结果；统计不只在 Dedicated Server 执行；账户持久化不反写 Gameplay。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

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
- `Assets/Scripts/FrameSync/Tests/SnapshotChecksumCompletenessTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `AggregateSnapshot_RestoresIntentDashAndLocomotion`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AggregateSnapshot_RestoresIntentDashAndLocomotion()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType source = Spawn(world, 100, 0);
            UnitType target = Spawn(world, 101, 0);
            source.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.AttackTarget,
                TargetUnit = target.UnitUid,
                AllowChase = true,
                AllowReplan = true,
            });

            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                source.MovementHandler.ApplyDash(
                    new fp2(fp.one, fp.zero), (fp)8, (fp)4);
                Assert.That(
                    source.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.ToPosition(new fp2(9, 3), (fp)0.5m)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
                var spec = new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);
                source.ActionRuntimes.Start(ActionKind.Move, spec);
            }
            finally
            {
                tick.EndTick();
            }

            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            source.Planner.ClearIntent();
            source.MovementHandler.Restore(MovementSnapshot.Default);
            source.Locomotion.CancelRoute(MoveCancelReason.UserCommand);
            source.ActionRuntimes.ClearWithoutCancel();

            pipeline.RestoreFromSnapshot(snapshot, 3);
// 方法后续请阅读上述真实源码；这里是节选。
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
