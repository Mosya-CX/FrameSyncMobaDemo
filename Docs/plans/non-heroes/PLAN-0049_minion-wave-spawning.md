# 小兵波次生成

## 本次执行范围

本计划对应原编码 0049 的一次执行：小兵波次生成。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [小兵波次与兵线 AI](../../requirements/non-heroes/REQ-FEAT-069_minion-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [防御塔目标优先级与攻击红线](../../requirements/non-heroes/REQ-FEAT-071_tower-targeting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

MinionSystem 固定波次序列和出生配置；MinionAIController 复用 UnitOrder/Planner/Attack；兵线参数与单位参数两类配置。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：`MinionSystem`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：`UnitAIController`、`MinionAIController`、`ThreatEntry`、`MonsterAIController`、`TowerAIController`。
- `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldService.cs`：`TeamFlowFieldService`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitSpawnRequest.cs`：`UnitSpawnReason`、`UnitSpawnRequest`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class MinionSystem
    {
        private readonly UnitWorld unitWorld;
        private readonly BakedMinionWaveConfig schedule;
        private readonly LaneRuntimeData[] lanes;
        private readonly List<MinionTicket> pendingTickets =
            new List<MinionTicket>(64);
        private readonly List<UnitUid> managedMinionUids =
            new List<UnitUid>(128);
        private int waveIndex;
        private int nextWaveLogicTick;
        private int nextTicketCursor;

        public int WaveIndex => waveIndex;
        public int NextWaveLogicTick => nextWaveLogicTick;
        public IReadOnlyList<MinionTicket> PendingTickets =>
            pendingTickets;
        public IReadOnlyList<UnitUid> ManagedMinionUids =>
            managedMinionUids;

        public bool TryGetLane(
            int laneId,
            out LaneRuntimeData lane)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].LaneId != laneId)
                    continue;
                lane = lanes[i];
                return true;
            }
            lane = null;
            return false;
        }

        public MinionSystem(
            UnitWorld unitWorld,
            in BakedMinionWaveConfig schedule,
            LaneRuntimeData[] lanes)
        {
            this.unitWorld = unitWorld ??
                throw new ArgumentNullException(nameof(unitWorld));
            if (schedule.WaveIntervalTicks <= 0 ||
                schedule.FirstWaveTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(schedule));
            this.schedule = schedule;
            this.lanes = lanes ??
                Array.Empty<LaneRuntimeData>();
            ValidateStaticTopology();
            nextWaveLogicTick = schedule.FirstWaveTick;
        }

        public void TickLogic()
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            while (currentTick >= nextWaveLogicTick)
            {
                ExpandWave(nextWaveLogicTick);
                waveIndex++;
                nextWaveLogicTick = checked(
                    nextWaveLogicTick +
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitLocomotionAgent : IRollback<LocomotionAgentSnapshot>
    {
        private readonly Unit _owner;
        private readonly PathGridMap2D _grid;
        private readonly AStarPathService _aStar;
        private readonly PathFollower2D _follower;
        private readonly TeamFlowFieldService _flowFieldService;

        private MovementTask _currentTask;
        private RouteRuntime _route;

        // Flow-field registry for runtime lookup
        private FlowFieldRegistry _flowFieldRegistry;

        private int RepathCooldownTicks =>
            DeterministicTimeConversion.Legacy30HzTicksToTicks(
                10,
                _owner.World?.TickRate ?? 30);
        private static readonly fp RepathThresholdSq =
            (fp)0.25m;
        private static readonly fp DirectMaxDistanceSq =
            (fp)9m;

        public UnitLocomotionAgent(Unit owner, PathGridMap2D grid)
        {
            _owner = owner;
            _grid = grid;
            _aStar = new AStarPathService(grid);
            _follower = new PathFollower2D(grid);
            _flowFieldService = new TeamFlowFieldService(grid);
            _currentTask = MovementTask.None;
            _route = RouteRuntime.Empty;
        }

        public Unit Owner => _owner;
        public PathGridMap2D Grid => _grid;
        public ref readonly MovementTask CurrentTask => ref _currentTask;
        public ref readonly RouteRuntime Route => ref _route;

        /// <summary>
        /// Current logical position. Reads from PhysicsEntity2D per
        /// Pathfinding Design v13.1 section 1.1 contract.
        /// </summary>
        public fp2 Position => _owner.PhysicsEntity?.Transform2D.Position ?? fp2.zero;

        /// <summary>
        /// Set the flow-field registry for runtime flow-field lookups.
        /// </summary>
        public void SetFlowFieldRegistry(FlowFieldRegistry registry)
        {
            _flowFieldRegistry = registry;
        }

        public MoveAcceptResult AcceptRouteRequest(RouteMoveRequest request)
        {
            if (!request.Target.HasTarget)
                return MoveAcceptResult.Rejected_InvalidTarget;

            if (!_owner.CanRunActiveGameplayThisTick)
                return MoveAcceptResult.Rejected_NoAgent;

            if (MatchesActiveTask(request))
                return MoveAcceptResult.Rejected_AlreadyActive;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**小兵波次与兵线 AI**

MinionSystem 固定波次序列和出生配置；MinionAIController 复用 UnitOrder/Planner/Attack；兵线参数与单位参数两类配置。

同 Tick 生效门、目标失效、追击距离与英雄协防条件明确；初始 Buff 和 Participant 来源由票据固定。

**防御塔目标优先级与攻击红线**

TowerAIController 过滤范围和合法目标；TowerAttackHandler 管理周期与 Commit，TowerTargetLinePresenter 读取当前锁定状态。

塔不追击；在途炮弹不因重新索敌改目标；英雄正在攻击己方英雄的判定来源固定；结构效果准入在中央入口。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Gameplay/Tests/FlowFieldBuildTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)8m, (fp)8m) },
            };

            int[] cost = service.BuildLaneCostField(laneConfig, RadiusClass.Medium);

            (int tx, int ty) = grid.WorldToCell(new fp2((fp)8m, (fp)8m));
            int targetIdx = ty * GridWidth + tx;
            Assert.That(cost[targetIdx], Is.EqualTo(0), "Target should have cost 0.");

            (int fx, int fy) = grid.WorldToCell(new fp2((fp)14m, (fp)14m));
            int farIdx = fy * GridWidth + fx;
            Assert.That(cost[farIdx], Is.GreaterThan(0), "Far cell should have positive cost.");
            Assert.That(cost[farIdx], Is.LessThan(int.MaxValue), "Far cell should be reachable.");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
