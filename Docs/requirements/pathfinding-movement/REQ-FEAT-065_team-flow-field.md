# 队伍流场与兵线引导

## 目标实现

小兵共享静态目标流场，避免每个单位独立重复 A*。

## 技术方案

TeamFlowFieldService 从兵线目标成本场构建队伍合并流场，贴墙候选遵守成本递减再评分。

## 边界情况

流场不可走、方向退化与队伍配置缺失有明确回退；流场是配置派生，不引入新玩法时钟。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldService.cs`：当前关联实现定义 TeamFlowFieldService（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/FlowFieldBuildTests.cs`：BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward、BuildLaneCostField_BlockedTarget_FindsNearestWalkable、BuildTeamFlowField_MultiLane_CorrectOwnerLane、BuildTeamFlowField_UnwalkableCells_DirectionCodeNone、BuildTeamFlowField_CostDecreasingConstraint_NoUphillMove、GetFlowDirection_KnownCell_ReturnsCorrectDirection、GetFlowDirection_IsolatedCell_DirNoneReturnsZero。
- `Assets/Scripts/Gameplay/Tests/IntegratedPathfindingPipelineTests.cs`：LaneAdvance_SelectsTeamFlowField、Chase_FirstTickBuildsPathBeforeRepathCooldown、Chase_DoesNotCompleteWhileOutsideAttackRange_AtPathDestinationCell、ChaseForCast_DoesNotCompleteAtAttackBoundaryDistance、PointMove_UsesDirectOrAStarByGrid、FlowFieldRvoMovement_ProducesRepeatableMotion、RadiusAwareLineOfSight_BlocksLargeUnit。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs`：TickContext_InitializesWithCorrectTick、TickContext_AdvancesCorrectly、DeterministicRandom_ProducesSameSequenceForSameSeed、UnitUid_ComparisonAndSorting、PathGrid_Initialise_ProducesValidGrid、AStar_FindPath_ReturnsValidPath、FlowField_BuildAndQuery_ReturnsValidDirection。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientBootstrapFirstWavePlayModeTests.cs`：GameScene_FirstWaveUsesFlowFieldsAndMoves、GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin。
- `Assets/Scripts/Gameplay/Tests/LocomotionSnapshotTests.cs`：CaptureRestore_ActiveRoute_RoundTripPreservesTask、CaptureRestore_IdleAgent_SnapshotHasNoActiveTask、CaptureRestore_AStarPath_PreservesDeepCopy、CaptureRestore_FlowFieldRoute_PreservesKind、Snapshot_AfterClearForDeath_HasNoActiveTask、RouteRuntime_Empty_DefaultValuesCorrect。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

流场用于大量单位共享同一推进方向，主要服务：

```text
小兵兵线推进
召唤物按队伍推进
特殊 AI 的静态路线推进
```

运行时流场不搜索、不重建、不做动态避障。  
动态单位避让由 RVO 处理。

---

### 队伍级合并流场

保留队伍级流场，不给每条兵线独立绑定运行时 ID。

但合并方式不能使用方向向量混合。  
使用：

```text
多兵线成本场 + 离散 OwnerLane
```

每个格子只归属一条兵线，然后使用该兵线成本场的下降方向。

---

### 流场数据

```text
TeamFlowFieldData
    TeamId Team
    RadiusClass RadiusClass

    int[] Cost
    byte[] OwnerLane
    int[] NextCell
    byte[] DirectionCode
```

说明：

| 字段 | 说明 |
|---|---|
| `Cost` | 当前格子的最终成本。 |
| `OwnerLane` | 当前格子归属哪条兵线。 |
| `NextCell` | 当前格子下一步走向的格子。 |
| `DirectionCode` | `Dir8` 或 `Dir16`，第一版建议 `Dir8`。 |

---

### 构建流程

```text
1. 对每条兵线分别构建整图成本场。
2. 对每个格子选择最低成本的兵线作为 OwnerLane。
3. 成本相同按固定 LaneIndex 优先级。
4. 根据 OwnerLane 对应成本场选择下降邻居。
5. 在下降邻居里做贴墙评分和方向平滑评分。
6. 最终方向必须满足 nextCost < currentCost。
```

---

### 兵线成本场构建伪代码

```pseudo
function BuildLaneCostField(laneTargets, shapeView):
    cost.Fill(INF)
    heap.Clear()

    for target in laneTargets:
        targetCell = Map.WorldToCell(target)

        if not Map.IsWalkableForAgent(targetCell, shapeView):
            targetCell = FindNearestWalkableCell(targetCell, shapeView, TargetSearchRadius)

        if targetCell is invalid:
            continue

        index = Map.GetIndex(targetCell)
        cost[index] = 0
        heap.Push(index)

    while heap not empty:
        current = heap.PopMin()

        for dir in Dir8:
            neighbor = GetNeighbor(current, dir)

            if not CanVisitFlowNeighbor(current, neighbor, dir, shapeView):
                continue

            newCost = cost[current] + MoveCost(dir) + ExtraTerrainCost(neighbor)

            if newCost < cost[neighbor]:
                cost[neighbor] = newCost
                heap.PushOrDecreaseKey(neighbor)

    return cost
```

---

### 队伍级合并伪代码

```pseudo
function BuildTeamFlowField(team, laneCostFields, shapeView):
    for each cell in Map:
        if not Map.IsWalkableForAgent(cell, shapeView):
            DirectionCode[cell] = None
            NextCell[cell] = Invalid
            Cost[cell] = INF
            continue

        bestLane = Invalid
        bestCost = INF

        for laneIndex from 0 to laneCostFields.Count - 1:
            laneCost = laneCostFields[laneIndex][cell]

            if laneCost < bestCost:
                bestCost = laneCost
                bestLane = laneIndex

            else if laneCost == bestCost and laneIndex < bestLane:
                bestLane = laneIndex

        OwnerLane[cell] = bestLane
        Cost[cell] = bestCost

    for each cell in Map:
        if Cost[cell] == INF:
            continue

        lane = OwnerLane[cell]
        NextCell[cell] = ChooseBestDescendingNeighbor(cell, laneCostFields[lane], shapeView)
        DirectionCode[cell] = ToDirectionCode(cell, NextCell[cell])
```

---

### 贴墙优化：成本递减约束下的候选评分

保留贴墙优化，但不能后处理向量 Lerp。  
最终方向必须指向更低成本的邻居。

```pseudo
function ChooseBestDescendingNeighbor(cell, laneCost, shapeView):
    currentCost = laneCost[cell]
    bestCell = Invalid
    bestScore = -INF

    for dir in Dir8:
        n = GetNeighbor(cell, dir)

        if not CanVisitFlowNeighbor(cell, n, dir, shapeView):
            continue

        if laneCost[n] >= currentCost:
            continue

        score = 0

        score += (currentCost - laneCost[n]) * CostDropWeight
        score += WallTangentScore(cell, n) * WallAlignWeight
        score += DirectionConsistencyScore(cell, n) * SmoothWeight
        score += LaneSkeletonScore(cell, n) * LaneWeight
        score -= DirTieBreaker(dir)

        if score > bestScore:
            bestScore = score
            bestCell = n

        else if score == bestScore:
            if Map.GetIndex(n) < Map.GetIndex(bestCell):
                bestCell = n

    return bestCell
```

---

### 运行时读取方向

```pseudo
function GetFlowDirection(team, position, shapeView):
    field = GetTeamFlowField(team, shapeView.RadiusClass)
    cell = Map.WorldToCell(position)

    if not Map.IsValidCell(cell):
        return fp2.zero

    dirCode = field.DirectionCode[cell.index]

    return DirCodeToFP2(dirCode)
```

运行时不构建完整路径列表。  
完整路径由 `NextCell` 链隐式表示。

---

### 帧同步定位

| 数据 | 标记 | 原因 |
|---|---|---|
| 兵线成本场、`OwnerLane`、`DirectionCode`、`NextCell` | `【静态配置】` | 离线构建，只读静态数据。 |
| 单位当前使用的队伍、半径层、路线类型 | `【需要帧同步保存】` | 属于该单位的 `RouteRuntimeSnapshot`。 |
| 当前格子的方向查询结果 | `【单 Tick 临时】` | 每 Tick 从当前位置重新读取。 |

流场资源本体不进入 Gameplay 快照。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

