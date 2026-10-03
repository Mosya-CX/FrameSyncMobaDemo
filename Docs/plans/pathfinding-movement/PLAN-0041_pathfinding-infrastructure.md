# 寻路基础设施

## 本次执行范围

本计划对应原编码 0041 的一次执行：寻路基础设施。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [A 星路径搜索与简化](../../requirements/pathfinding-movement/REQ-FEAT-064_astar-search.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Pathfinding/LocomotionResult.cs`：`RouteEvaluationStatus`、`LocomotionResult`。
- `Assets/Scripts/Gameplay/Pathfinding/MovementTask.cs`：`MovePurpose`、`MovementTaskState`、`MoveTarget`、`MovementTask`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/Gameplay/Pathfinding/PathGridMap2D.cs`：`PathGridMap2D`、`walkability`、`or`、`and`。
- `Assets/Scripts/Gameplay/Pathfinding/RouteRuntime.cs`：`RouteKind`、`RouteRuntime`。
- `Assets/Scripts/Gameplay/Pathfinding/PathNode.cs`：`PathNode`。
- `Assets/Scripts/Gameplay/Pathfinding/PathResult.cs`：`PathStatus`、`PathResult`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnitCollisionParticipant
    {
        [Header("Deterministic composition")]
        [Tooltip("Authoritative 2D physics component owned by this Unit prefab.")]
        [SerializeField] private PhysicsEntity2D physicsEntity;
        [SerializeField] private StatHandler statHandler;
        [SerializeField] private MovementHandler movementHandler;
        [SerializeField] private AttackHandler attackHandler;
        [SerializeField] private AbilityHandler abilityHandler;
        [SerializeField] private BuffHandler buffHandler;
        [SerializeField] private CrowdControlHandler crowdControlHandler;
        [SerializeField] private EquipmentHandler equipmentHandler;

        private CapabilityState capabilityState;
        private UnitAbilityMask abilityMask;
        private readonly List<UnitTag> tags =
            new List<UnitTag>();

        /// <summary>Deterministic runtime identity (SpawnLogicTick /
        /// prefab id / spawn sequence). Displayed in the Inspector for
        /// debugging spawned unit instances.</summary>
        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-120)]
        public UnitUid UnitUid { get; private set; }
        public GameplayParticipantId GameplayParticipantId { get; private set; }
        public UnitWorld World { get; internal set; }
        public UnitUid OwnerUid { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public ushort UnitSubKindId { get; private set; }
        public TeamId TeamId { get; private set; }
        public int UnitPrototypeId { get; private set; }
        public int BaseGoldValue { get; private set; }
        public int BaseExperienceValue { get; private set; }
        public int BaseCreepScoreValue { get; private set; }
        public LifeState LifeState { get; private set; }
        public ref readonly CapabilityState CapabilityState => ref capabilityState;
        public UnitAbilityMask AbilityMask => abilityMask;

        public PhysicsEntity2D PhysicsEntity => physicsEntity;
        public StatHandler StatHandler => statHandler;
        public CombatModifierSet CombatModifiers { get; private set; }
        public MovementHandler MovementHandler => movementHandler;
        public AttackHandler AttackHandler => attackHandler;
        public AbilityHandler AbilityHandler => abilityHandler;
        public BuffHandler BuffHandler => buffHandler;
        public CrowdControlHandler CrowdControl => crowdControlHandler;
        public EquipmentHandler EquipmentHandler => equipmentHandler;
        public UnitEventBus EventBus { get; private set; }

        public UnitIntent Intent { get => Planner?.CurrentIntent ?? UnitIntent.None; internal set => Planner?.SetIntent(value); }
        public BehaviorPlanner Planner { get; private set; }
        public ActionArbiter Arbiter { get; private set; }
        public ActionRuntimeSet ActionRuntimes { get; private set; }

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;
        /// <summary>
        /// The deterministic home spawn position captured when this runtime
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Pathfinding/LocomotionResult.cs`：

```csharp
        public static LocomotionResult Idle(UnitUid uid) => new LocomotionResult
        {
            UnitUid = uid,
            AllowRVO = false,
            Status = RouteEvaluationStatus.Idle,
        };
```

### 输入输出与边界

**旋转网格地图与半径通行**

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

旋转、边界、不可走起终点和过大半径可见处理；NavMask 与内容配置版本有一致来源。

**路线选择与跟随状态**

UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。

控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。

**A 星路径搜索与简化**

AStarPathService 采用显式 OpenSet 排序、固定邻居访问与启发式成本；候选终点和路径简化仍受半径通行验证。

相同代价稳定平局；不可达不伪造直达路线；禁止随机/集合枚举顺序影响路径。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `AStar_OpenGrid_ReturnsCorrectPath`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AStar_OpenGrid_ReturnsCorrectPath()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(10, 10);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True, "A* should find a path on an open grid.");
            Assert.That(result.PathCellIndices, Is.Not.Null);
            Assert.That(result.PathCellIndices.Length, Is.GreaterThan(0));
        }
```
- `Assets/Scripts/Gameplay/Tests/IntegratedPathfindingPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `LaneAdvance_SelectsTeamFlowField`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void LaneAdvance_SelectsTeamFlowField()
        {
            PathGridMap2D grid = CreateGrid();
            FlowFieldRegistry registry =
                CreateRegistry(
                    grid,
                    1,
                    RadiusClass.Small);
            Unit unit = CreateUnit(
                1,
                new fp2((fp)2, (fp)10),
                new TeamId(1));
            var locomotion =
                new UnitLocomotionAgent(
                    unit,
                    grid);
            locomotion.SetFlowFieldRegistry(
                registry);

            RouteMoveRequest request =
                RouteMoveRequest.ToPosition(
                    new fp2((fp)18, (fp)10));
            request.Purpose =
                MovePurpose.LaneAdvance;
            request.AllowRVO = true;
            Assert.That(
                locomotion.AcceptRouteRequest(
                    request),
                Is.EqualTo(
                    MoveAcceptResult.Accepted));

            LocomotionResult result =
                locomotion.Evaluate();

            Assert.That(
                locomotion.Route.Kind,
                Is.EqualTo(
                    RouteKind.FlowField));
            Assert.That(
                locomotion.Route.FlowFieldKey,
                Is.EqualTo(
                    new FlowFieldKey(
                        1,
                        RadiusClass.Small)
                        .Packed));
            Assert.That(
                result.Status,
                Is.EqualTo(
                    RouteEvaluationStatus.Moving));
            Assert.That(
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Gameplay/Tests/LocomotionSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CaptureRestore_ActiveRoute_RoundTripPreservesTask`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CaptureRestore_ActiveRoute_RoundTripPreservesTask()
        {
            // This test verifies that after Capture + Restore,
            // the locomotion agent still has the active task.
            // Since UnitLocomotionAgent requires a real Unit (with World, etc.),
            // we test the snapshot struct directly.

            var snap = new LocomotionAgentSnapshot();
            var task = new MovementTask
            {
                Purpose = MovePurpose.MoveToPosition,
                Target = MoveTarget.FromPosition(new fp2((fp)10m, (fp)5m)),
                StopDistance = (fp)0.5m,
                AllowRVO = true,
                AllowRepath = true,
                State = MovementTaskState.Active,
            };
            var route = new RouteRuntime
            {
                Kind = RouteKind.AStar,
                NeedRepath = false,
                LastPathTargetPosition = new fp2((fp)10m, (fp)5m),
                AStarPathCellIndices = new int[] { 0, 17, 34 },
            };

            snap.HasActiveTask = true;
            snap.Task = task;
            snap.Route = route;

            // Round-trip through the struct
            var restored = snap;
            Assert.That(restored.HasActiveTask, Is.True);
            Assert.That(restored.Task.Purpose, Is.EqualTo(MovePurpose.MoveToPosition));
            Assert.That(restored.Task.AllowRVO, Is.True);
            Assert.That(restored.Task.State, Is.EqualTo(MovementTaskState.Active));
            Assert.That(restored.Route.Kind, Is.EqualTo(RouteKind.AStar));
            Assert.That(restored.Route.AStarPathCellIndices, Is.Not.Null);
            Assert.That(restored.Route.AStarPathCellIndices.Length, Is.EqualTo(3));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
