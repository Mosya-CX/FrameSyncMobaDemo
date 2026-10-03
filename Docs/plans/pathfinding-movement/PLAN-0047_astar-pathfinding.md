# 确定性 A星寻路

## 本次执行范围

本计划对应原编码 0047 的一次执行：确定性 A星寻路。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [A 星路径搜索与简化](../../requirements/pathfinding-movement/REQ-FEAT-064_astar-search.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs`：`PathFollower2D`、`PathFollowerState`。
- `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs`：`AStarPathService`。
- `Assets/Scripts/Gameplay/Pathfinding/RouteRuntime.cs`：`RouteKind`、`RouteRuntime`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/Gameplay/Pathfinding/LocomotionResult.cs`：`RouteEvaluationStatus`、`LocomotionResult`。
- `Assets/Scripts/Gameplay/Pathfinding/IndexedMinHeap.cs`：`IndexedMinHeap`。
- `Assets/Scripts/Gameplay/Pathfinding/LocomotionAgentSnapshot.cs`：`LocomotionAgentSnapshot`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs`：

```csharp
        public LocomotionResult BuildLocomotionResult(fp2 currentPosition, fp moveSpeed, UnitUid unitUid, bool allowRVO = false)
        {
            if (RouteFinished || _pathCellIndices == null || PathCursor < 0 || PathCursor >= _pathCellIndices.Length)
                return LocomotionResult.Idle(unitUid);

            int targetCellIndex = _pathCellIndices[PathCursor];
            int cx = targetCellIndex % _grid.Width;
            int cy = targetCellIndex / _grid.Width;
            fp2 waypointWorld = _grid.CellToWorld(cx, cy);

            fp2 toTarget = waypointWorld - currentPosition;
            fp distSq = fpmath.dot(toTarget, toTarget);

            if (distSq <= fp.zero)
            {
                // Try next waypoint if available
                if (PathCursor < _pathCellIndices.Length - 1)
                {
                    PathCursor++;
                    return BuildLocomotionResult(currentPosition, moveSpeed, unitUid, allowRVO);
                }
                return LocomotionResult.Idle(unitUid);
            }

            fp dist = fpmath.sqrt(distSq);
            fp2 direction = toTarget / dist;

            return new LocomotionResult
            {
                UnitUid = unitUid,
                HasMovement = true,
                DesiredDirection = direction,
                DesiredSpeed = moveSpeed,
                AllowRVO = allowRVO,
                Status = RouteEvaluationStatus.Moving,
            };
        }
```

`Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs`：

```csharp
        public PathResult FindPath(fp2 start, fp2 target, int maxIterations = MaxIterationsDefault)
        {
            if (_grid.Width <= 0 || _grid.Height <= 0)
                return PathResult.Failed(PathStatus.SystemNotReady);

            (int startCx, int startCy) = _grid.WorldToCell(start);
            (int targetCx, int targetCy) = _grid.WorldToCell(target);

            // Validate start
            if (!_grid.IsPassable(startCx, startCy))
                return PathResult.Failed(PathStatus.InvalidStart);
            return FindPathImpl(startCx, startCy, targetCx, targetCy, maxIterations);
        }
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
