# A 星路径搜索与简化

## 目标实现

点到点搜索有稳定结果和不可达反馈。

## 技术方案

AStarPathService 采用显式 OpenSet 排序、固定邻居访问与启发式成本；候选终点和路径简化仍受半径通行验证。

## 边界情况

相同代价稳定平局；不可达不伪造直达路线；禁止随机/集合枚举顺序影响路径。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs`：当前关联实现定义 AStarPathService（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/PathResult.cs`：当前关联实现定义 PathStatus、PathResult（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs`：TickContext_InitializesWithCorrectTick、TickContext_AdvancesCorrectly、DeterministicRandom_ProducesSameSequenceForSameSeed、UnitUid_ComparisonAndSorting、PathGrid_Initialise_ProducesValidGrid、AStar_FindPath_ReturnsValidPath、FlowField_BuildAndQuery_ReturnsValidDirection。
- `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs`：AStar_OpenGrid_ReturnsCorrectPath、AStar_WalledCorridor_FindsPathAroundWall、AStar_BlockedTarget_NeighborExpansionFindsNearbyCell、AStar_EmptyOpenSet_ReturnsNoPath、AStar_MaxIterationReached_ReturnsMaxIterationReached、AStar_SameCellStartTarget_ReturnsSingleCell、AStar_LOS_Smoothing_ReducesNodeCountOnStraightLine。
- `Assets/Scripts/Gameplay/Tests/MapPathfindingAssetIntegrationTests.cs`：MapPrefab_OwnsGridLanesFieldsAndVisualizer、RotatedThinObstacle_DoesNotBecomeAabbSquare、MiddleLaneFlow_PullsTowardLaneProgressively、StraightLaneSkeletonCells_UseAuthoredTangents、FoundationJunctions_DistinguishDepartureFromArrivalTarget、LaneOwnershipBoundaries_HaveDirectionsAndBypassVisualizerStride、SixLaneDirections_FollowForwardWaypointsInOrder。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。
- `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs`：FiveAgents_ConvergingOnCenter_NoDeadlock、TwoUnitsHeadOn_VelocitiesDiverge、TenAgents_RandomSpread_CompletesWithoutError、RVO_Deterministic_SameInputProducesSameOutput、RVO_ZeroDesiredVelocity_ReturnsZero、FlowDirection_FromFarCell_PointsTowardTarget、FlowDirection_AtTarget_ReturnsZero。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

A* 用于：

```text
玩家点地移动
攻击追踪
施法追踪
野怪回营地
控制移动中允许寻路的情况
```

小兵兵线推进优先用流场，不使用每个小兵单独 A*。

---

### 数据结构

```text
AStarPathService
    PathGridMap2D Map

    AStarNodeState[] NodeStates
    IndexedMinHeap OpenSet
    int SearchId
```

```text
AStarNodeState
    int G
    int H
    int ParentIndex
    int OpenedSearchId
    int ClosedSearchId
```

不使用全图 `ResetSearchState()`。  
每次搜索递增 `SearchId`，节点状态通过 SearchId 判断是否属于本次搜索。

---

### OpenSet

使用 Indexed Binary Heap + DecreaseKey。

```text
IndexedMinHeap
    int[] Heap
    int[] HeapPositions
```

比较规则：

```text
F 小优先
F 相同 H 小优先
H 相同 NodeIndex 小优先
```

这样保证确定性。

---

### A* 主流程伪代码

```pseudo
function FindPath(startPos, endPos, shapeView, options):
    SearchId += 1
    OpenSet.Clear()

    startCell = Map.WorldToCell(startPos)
    endCell = Map.WorldToCell(endPos)

    if not Map.IsWalkableForAgent(startCell, shapeView):
        startCell = FindNearestWalkableCell(startCell, shapeView, options.StartSearchRadius)

    if not Map.IsWalkableForAgent(endCell, shapeView):
        endCell = FindNearestWalkableCell(endCell, shapeView, options.EndSearchRadius)

    if startCell is invalid:
        return PathResult(InvalidStart)

    if endCell is invalid:
        return PathResult(EndBlocked)

    startIndex = Map.GetIndex(startCell)
    endIndex = Map.GetIndex(endCell)

    InitNode(startIndex, g = 0, h = Heuristic(startCell, endCell), parent = -1)
    OpenSet.Push(startIndex)

    iteration = 0

    while OpenSet not empty:
        if iteration >= options.MaxIteration:
            return PathResult(MaxIterationReached)

        iteration += 1

        current = OpenSet.PopMin()

        if IsClosed(current):
            continue

        MarkClosed(current)

        if current == endIndex:
            return BuildPathResult(startIndex, endIndex)

        for each dir in Dir8:
            neighbor = GetNeighbor(current, dir)

            if not CanVisitNeighbor(current, neighbor, dir, shapeView):
                continue

            tentativeG = G(current) + MoveCost(dir)

            if not IsOpened(neighbor):
                InitNode(
                    neighbor,
                    g = tentativeG,
                    h = Heuristic(neighbor, endIndex),
                    parent = current
                )
                OpenSet.Push(neighbor)

            else if tentativeG < G(neighbor):
                SetG(neighbor, tentativeG)
                SetParent(neighbor, current)
                OpenSet.DecreaseKey(neighbor)

    return PathResult(NoPath)
```

---

### 邻居访问伪代码

```pseudo
function CanVisitNeighbor(current, neighbor, dir, shapeView):
    if not Map.IsValidCell(neighbor):
        return false

    if not Map.IsWalkableForAgent(neighbor, shapeView):
        return false

    if dir is diagonal:
        sideA = Cell(current.x + dir.x, current.y)
        sideB = Cell(current.x, current.y + dir.y)

        if not Map.IsWalkableForAgent(sideA, shapeView):
            return false

        if not Map.IsWalkableForAgent(sideB, shapeView):
            return false

    return true
```

禁止斜穿墙必须保留。

---

### 启发函数

八方向网格使用 Octile Heuristic：

```pseudo
function Heuristic(a, b):
    dx = Abs(a.x - b.x)
    dy = Abs(a.y - b.y)

    minD = Min(dx, dy)
    maxD = Max(dx, dy)

    return 14 * minD + 10 * (maxD - minD)
```

---

### 目标不可走处理

```pseudo
function FindNearestWalkableCell(center, shapeView, radius):
    best = invalid
    bestScore = INF

    for r from 0 to radius:
        for cell on square ring(center, r):
            if not Map.IsWalkableForAgent(cell, shapeView):
                continue

            score = DistanceManhattan(center, cell) * 1000 + CellIndex(cell)

            if score < bestScore:
                bestScore = score
                best = cell

        if best is valid:
            return best

    return invalid
```

---

### 路径简化

A* 返回路径后，允许做确定性 LOS 简化：

```pseudo
function SimplifyPathByLOS(path, shapeView):
    if path.Count <= 2:
        return path

    simplified.Clear()
    anchor = 0
    simplified.Add(path[0])

    while anchor < path.Count - 1:
        farthest = anchor + 1

        for i from path.Count - 1 down to anchor + 1:
            if GridLineOfSightWalkable(path[anchor], path[i], shapeView):
                farthest = i
                break

        simplified.Add(path[farthest])
        anchor = farthest

    return simplified
```

---

### 帧同步定位

第一版 A* 查询必须在同一个逻辑 Tick 内同步完成，不做跨 Tick 的增量搜索。

| 数据 | 标记 | 原因 |
|---|---|---|
| `SearchId`、节点访问标记 | `【可确定性重建】` | 只用于避免全图清理，不影响玩法语义。恢复时可清空。 |
| `IndexedMinHeap`、OpenSet、Closed 状态 | `【单 Tick 临时】` | 单次查询局部数据。 |
| 完成后的路径格子序列 | `【需要帧同步保存】` | 若当前单位正在沿该路径移动，保存在 `RouteRuntimeSnapshot`。 |
| 简化后的世界二维路径点 | `【可确定性重建】` | 可由路径格子序列与地图确定性重建；也可直接缓存但不重复快照。 |

如果未来改为“跨多个 Tick 分摊 A* 搜索”，则 OpenSet、节点状态、父节点、剩余预算都必须进入快照；第一版不采用这种模式。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

