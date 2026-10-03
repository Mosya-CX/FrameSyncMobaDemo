# 寻路集成

## 本次执行范围

本计划对应原编码 0091 的一次执行：寻路集成。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [A 星路径搜索与简化](../../requirements/pathfinding-movement/REQ-FEAT-064_astar-search.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldData.cs`：`combination`、`TeamFlowFieldData`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs`：`AStarPathService`。
- `Assets/Scripts/Gameplay/Pathfinding/DeterministicRVOSystem.cs`：`DeterministicRVOSystem`。
- `Assets/Scripts/Gameplay/Pathfinding/Dir8.cs`：`Dir8`、`Dir8Helper`、`value`。
- `Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs`：`PathFollower2D`、`PathFollowerState`。
- `Assets/Scripts/Gameplay/Pathfinding/RVOInput.cs`：`RVOInput`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldData.cs`：

```csharp
using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Baked flow field data for one team + radius class combination.
    /// NOT in Gameplay snapshot — static configuration (Design v13.1 section 8.9).
    /// (Pathfinding Design v13.1 section 8.3)
    /// </summary>
    [Serializable]
    public struct TeamFlowFieldData
    {
        public FlowFieldKey Key;

        /// <summary>Final integrated cost per cell. INF for unwalkable/unreachable.</summary>
        public int[] Cost;

        /// <summary>Lane index owning each cell (section 8.6). 255 = none.</summary>
        public byte[] OwnerLane;

        /// <summary>Flat index of the next descending cell. -1 for sink/none.</summary>
        public int[] NextCell;

        /// <summary>Dir8 cast to byte per cell. 0 = None.</summary>
        public byte[] DirectionCode;

        public int Width;
        public int Height;
        public int CellCount;

        public bool IsValid => Cost != null && Cost.Length > 0 && Cost.Length == CellCount;

        public static readonly TeamFlowFieldData Empty = new TeamFlowFieldData();
    }
}
```

`Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：

```csharp
        public LocomotionResult Evaluate()
        {
            // D-008 spawn-Tick gate
            if (!_owner.CanRunActiveGameplayThisTick)
                return LocomotionResult.Idle(_owner.UnitUid);

            if (_currentTask.State != MovementTaskState.Active)
                return LocomotionResult.Idle(_owner.UnitUid);

            fp2 currentPos = Position;
            fp statMoveSpeed =
                _owner.StatHandler?.GetStat(StatId.MoveSpeed) ?? fp.one;
            fp moveSpeed = statMoveSpeed *
                (_owner.World?.MoveSpeedToLogicVelocityScale ?? fp.one);

            // Flow-field route: sample direction from baked field
            if (_route.Kind == RouteKind.FlowField)
            {
                return EvaluateFlowField(currentPos, moveSpeed);
            }

            // Determine target position
            fp2 targetPos = ResolveTargetPosition();
            if (_currentTask.State ==
                MovementTaskState.Cancelled)
            {
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    Status =
                        RouteEvaluationStatus.TargetLost,
                };
            }

            // Check arrival at destination
            if (CheckArrival(currentPos, targetPos))
            {
                _currentTask.State = MovementTaskState.Completed;
                _follower.Reset();
                _route.NeedRepath = false;
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    HasMovement = false,
                    AllowRVO = _currentTask.AllowRVO,
                    Status = RouteEvaluationStatus.Reached,
                };
            }

            if (_route.Kind == RouteKind.Direct)
            {
                DynamicNavigationQuery dynamicQuery =
                    BuildDynamicNavigationQuery();
                if (_currentTask.AllowRepath &&
                    dynamicQuery.HasHardBlockOnLine(
                        currentPos,
                        targetPos,
                        OwnerRadiusClass))
                {
                    _route.Kind = RouteKind.AStar;
                    _route.NeedRepath = true;
                }
                else
                {
                    return BuildDirectResult(
                        currentPos,
                        targetPos,
                        moveSpeed);
                }
            }
// 方法后续请阅读上述真实源码；这里是节选。
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
- `Assets/Scripts/Gameplay/Tests/RVOSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge()
        {
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, (fp)5m), new fp2(fp.zero, -fp.one)),
                MakeInput(2, new fp2(fp.zero, (fp)6m), new fp2(fp.zero, fp.one)),
            };

            RvoResult[] results = _rvo.Step(inputs);

            Assert.That(results.Length, Is.EqualTo(2));
            // Units should not both move directly toward each other
            fp2 v1 = results[0].FinalVelocity;
            fp2 v2 = results[1].FinalVelocity;

            // At least one unit should have a non-zero velocity
            bool hasMovement = (v1.x != fp.zero || v1.y != fp.zero)
                || (v2.x != fp.zero || v2.y != fp.zero);
            Assert.That(hasMovement, Is.True, "At least one unit should have movement.");
        }
```
- `Assets/Scripts/Gameplay/Tests/WallPenetrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Detect_UnitInBlockedCell_ReturnsCorrection`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Detect_UnitInBlockedCell_ReturnsCorrection()
        {
            var grid = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);

            // Position inside the blocked cell
            fp2 pos = new fp2((fp)7m, (fp)7m);
            MovementCorrectionRequest? req = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid);

            Assert.That(req.HasValue, Is.True, "Should detect penetration in blocked cell.");
            Assert.That(req.Value.UnitUid, Is.EqualTo(uid));
            Assert.That(req.Value.Reason, Is.EqualTo(MovementCorrectionReason.WallDepenetration));
        }
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
