# 路线选择与跟随状态

## 目标实现

移动目的选择直达、A* 或队伍流场并维护路径进度。

## 技术方案

UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。

## 边界情况

控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs`：当前关联实现定义 PathFollower2D、PathFollowerState（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/RouteRuntime.cs`：当前关联实现定义 RouteKind、RouteRuntime（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：当前关联实现定义 UnitLocomotionAgent、value（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/LocomotionSnapshotTests.cs`：CaptureRestore_ActiveRoute_RoundTripPreservesTask、CaptureRestore_IdleAgent_SnapshotHasNoActiveTask、CaptureRestore_AStarPath_PreservesDeepCopy、CaptureRestore_FlowFieldRoute_PreservesKind、Snapshot_AfterClearForDeath_HasNoActiveTask、RouteRuntime_Empty_DefaultValuesCorrect。
- `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs`：AStar_OpenGrid_ReturnsCorrectPath、AStar_WalledCorridor_FindsPathAroundWall、AStar_BlockedTarget_NeighborExpansionFindsNearbyCell、AStar_EmptyOpenSet_ReturnsNoPath、AStar_MaxIterationReached_ReturnsMaxIterationReached、AStar_SameCellStartTarget_ReturnsSingleCell、AStar_LOS_Smoothing_ReducesNodeCountOnStraightLine。
- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_ClearsChaseRouteAndAttackIntentBeforeSnapshot、DespawnTarget_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_DoesNotRevokeCommittedAttack、DespawnTarget_DoesNotRevokeCommittedAttack、FormalDeathInvalidation_RejectsNonIncreasingSequence、BeginAndCancel_DoNotConsumeSequence。
- `Assets/Scripts/Gameplay/Tests/IntegratedPathfindingPipelineTests.cs`：LaneAdvance_SelectsTeamFlowField、Chase_FirstTickBuildsPathBeforeRepathCooldown、Chase_DoesNotCompleteWhileOutsideAttackRange_AtPathDestinationCell、ChaseForCast_DoesNotCompleteAtAttackBoundaryDistance、PointMove_UsesDirectOrAStarByGrid、FlowFieldRvoMovement_ProducesRepeatableMotion、RadiusAwareLineOfSight_BlocksLargeUnit。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 设计原则

上层表达：

```text
为什么移动
```

对应系统决定：

```text
UnitLocomotionAgent：
    普通路线请求是否使用 Direct、A* 或 FlowField。

MovementHandler：
    如何执行当前 Tick 的普通移动结果、Dash、强制位移、传送与修正。

CrowdControlHandler：
    哪个强制位移控制实例生效。
```

`MoveOrder` 不直接携带 `RouteKind`。

---

### `MovePurpose`

```text
MovePurpose
    PointMove
    ChaseForAttack
    ChaseForCast
    LaneAdvance
    ReturnToCamp
    ControlMove
    Dash
    ForcedMove
```

| 来源 | `MovePurpose` | 正式入口 |
|---|---|---|
| 玩家点地移动 | `PointMove` | `UnitLocomotionAgent`，由其选择 A* 或 Direct。 |
| 攻击追踪 | `ChaseForAttack` | `UnitLocomotionAgent`，通常使用 A*。 |
| 施法追踪 | `ChaseForCast` | `UnitLocomotionAgent`，通常使用 A*。 |
| 小兵兵线推进 | `LaneAdvance` | `UnitLocomotionAgent`，使用 FlowField。 |
| 野怪回营地 | `ReturnToCamp` | `UnitLocomotionAgent`，选择 A* 或 Direct。 |
| 恐惧到目标点并允许绕墙 | `ControlMove` | 行为/控制系统向 `UnitLocomotionAgent` 提交路线请求。 |
| 持续朝指定方向失控移动 | `ForcedMove` | `CrowdControlHandler` 仲裁后交给 `MovementHandler`。 |
| Dash | `Dash` | 技能/行为系统批准后交给 `MovementHandler`。 |
| 击退 / 拉扯 | `ForcedMove` | `CrowdControlHandler` 仲裁后交给 `MovementHandler`。 |

`MovePurpose` 是统一语义分类，不表示所有类型都通过同一个函数入口。

---

### 请求类型

```text
RouteMoveRequest
    MovePurpose Purpose
    MoveTarget Target
    MoveRequestSource Source
    int IssuedTick
    fp StopDistance
    bool AllowRVO
    bool AllowRepath

DashRequest
    DashDesc Desc
    MoveRequestSource Source
    int IssuedTick

ResolvedForcedMove
    CrowdControlHandle SourceControlHandle
    ForcedMoveConfigId ConfigId
    int DurationTicks
    fp2 Direction
    fp2 TargetPosition
    ForcedMoveWallPolicy WallPolicy
```

边界：

```text
RouteMoveRequest
    进入 UnitLocomotionAgent。

DashRequest
    进入 MovementHandler。

原始 ForcedMoveRequest
    进入 CrowdControlHandler。
    仲裁后转换成 ResolvedForcedMove，再进入 MovementHandler。
```

---

### `MoveTarget`

```text
MoveTarget
    MoveTargetType Type
    fp2 Position
    UnitUid TargetUnitUid
    fp2 Direction
    FlowFieldKey FlowFieldKey
```

| 类型 | 用途 |
|---|---|
| `Position` | 点地移动、回营地、允许寻路的控制目标点。 |
| `Entity` | 攻击或施法追踪。 |
| `Direction` | Direct 路线或受控方向。 |
| `FlowField` | 小兵队伍级流场。 |

---

### 分流伪代码

```pseudo
function DispatchMovementPurpose(request):
    switch request.Purpose:
        case PointMove:
        case ChaseForAttack:
        case ChaseForCast:
        case LaneAdvance:
        case ReturnToCamp:
            return UnitLocomotionAgent.AcceptRouteRequest(
                request.AsRouteMoveRequest()
            )

        case ControlMove:
            if request.AllowPathfinding:
                return UnitLocomotionAgent.AcceptRouteRequest(
                    request.AsRouteMoveRequest()
                )

            return CrowdControlHandler.Add(
                request.AsForcedMoveControlRequest()
            )

        case Dash:
            return MovementHandler.StartDash(
                request.AsDashRequest()
            )

        case ForcedMove:
            return CrowdControlHandler.Add(
                request.AsForcedMoveControlRequest()
            )
```

`MovementHandler` 不作为普通寻路请求入口；  
`UnitLocomotionAgent` 不作为强制位移执行入口。

---

### 核心数据

```text
UnitLocomotionAgent
    Unit Owner                              【查询引用】
    PhysicsEntity2D Entity                  【查询引用】

    MovementTask CurrentTask                【需要帧同步保存】
    RouteRuntime Route                      【需要帧同步保存】
    PathFollower2D PathFollower             【其跨 Tick 运行状态需要帧同步保存】
```

`UnitLocomotionAgent` 每 Tick 读取：

```text
Entity.Position
Entity.Forward
Entity.Shape
```

它不保存单位位置副本，不写入空间状态。

---

### `RouteKind`

```text
RouteKind
    None
    Direct
    AStar
    FlowField
```

`Dash / ForcedMove` 不属于路线类型。  
它们由 `MovementHandler` 执行，也不把轨迹写入 `UnitLocomotionAgent`。

---

### 核心接口

```text
UnitLocomotionAgent
    MoveAcceptResult AcceptRouteRequest(RouteMoveRequest request)
    void CancelRoute(MoveCancelReason reason)
    LocomotionResult Evaluate()
```

不提供：

```text
ApplyPosition
CommitMove
SetRvoVelocity
向 MovementHandler 传完整路径
```

---

### 路由选择伪代码

```pseudo
function RouteResolver.Resolve(agent, request):
    switch request.Purpose:
        case LaneAdvance:
            return RoutePlan(
                Kind = FlowField,
                FlowFieldKey = ResolveTeamFlowField(agent.Owner.Team)
            )

        case ChaseForAttack:
        case ChaseForCast:
            return RoutePlan(
                Kind = AStar,
                Target = request.Target
            )

        case PointMove:
        case ReturnToCamp:
            if CanUseDirect(
                start = agent.Entity.Position,
                end = request.Target.Position,
                shape = agent.Entity.Shape
            ):
                return RoutePlan(Kind = Direct)

            return RoutePlan(Kind = AStar)

        case ControlMove:
            if request.AllowPathfinding:
                return RoutePlan(Kind = AStar)

            return RoutePlan(Kind = Direct)

    return RoutePlan(Kind = None)
```

普通强制位移控制不通过 `ControlMove` 寻路。  
`ControlMove` 只表示确实需要路线决策的受控移动，例如允许绕墙的恐惧目标点移动。

---

### `CanUseDirect`

```pseudo
function CanUseDirect(start, end, shape):
    if DistanceSq(start, end) > DirectMaxDistanceSq:
        return false

    if not GridLineOfSightWalkable(
        start,
        end,
        shape.RadiusClass
    ):
        return false

    return true
```

只使用 `PathGridMap2D` 的确定性格子检测，禁止 Unity Physics。

---

### 每 Tick 寻路评估

```pseudo
function UnitLocomotionAgent.Evaluate():
    tick = SimulationTickContext.Current.Tick

    if not Owner.CanRunActiveGameplayThisTick:
        return LocomotionResult.Idle

    if not HasValidTask(CurrentTask):
        return LocomotionResult.Idle

    position = Entity.Position

    UpdateDynamicTarget()
    UpdateChaseRepathSchedule(tick)

    switch Route.Kind:
        case Direct:
            return EvaluateDirectRoute(position)

        case AStar:
            return EvaluateAStarRoute(position)

        case FlowField:
            return EvaluateFlowFieldRoute(position)

        default:
            return LocomotionResult.NoRoute
```

`Evaluate()` 同时负责：

```text
读取当前实际位置
验证现有路线
推进 PathCursor
检测路径偏离
必要时重新寻路
判断任务是否到达
计算当前 Tick 的期望移动方向和速度
```

因此，不需要由 `MovementHandler` 保存路径，也不需要强制位移结束后设计“恢复旧路径”策略。

生成 Tick 返回 `Idle` 只禁止普通主动寻路输出，不等于跳过各 Handler 的被动状态推进。  
外部强制位移由 `MovementHandler` 根据已生效的 `ForcedMoveRuntime` 独立执行。

---

### A* 路线评估与偏离检测

```pseudo
function EvaluateAStarRoute(position):
    if Route.NeedRepath:
        if not RebuildAStarPath(position, ResolveCurrentTargetPosition()):
            return LocomotionResult.NoRoute

    PathFollower.AdvanceCursor(
        position,
        Route.AStarPathCellIndices
    )

    if IsTaskReached(position, CurrentTask):
        CompleteCurrentTask()
        return LocomotionResult.Reached

    if PathFollower.IsOutsideRemainingPathCorridor(
        position,
        Route.AStarPathCellIndices,
        PathCorridorTolerance
    ):
        Route.NeedRepath = true

        if not RebuildAStarPath(position, ResolveCurrentTargetPosition()):
            return LocomotionResult.NoRoute

        PathFollower.ResetCursorForNewPath()

    return PathFollower.BuildAStarLocomotionResult(
        position,
        Route.AStarPathCellIndices,
        ResolveMoveSpeed()
    )
```

偏离检测是正常路径跟随的一部分，不是只为强制位移设置的补丁。

检测范围只覆盖当前游标附近和前方有限路径段：

```pseudo
function IsOutsideRemainingPathCorridor(position, path, tolerance):
    nearestSegment = FindNearestSegmentAroundCursor(
        position,
        path,
        cursor = PathCursor,
        backwardCount = CorridorBackwardCheckCount,
        forwardCount = CorridorForwardCheckCount
    )

    if nearestSegment not found:
        return true

    return DistanceSqToSegment(position, nearestSegment)
        > tolerance * tolerance
```

---

### Direct 与流场评估

```pseudo
function EvaluateDirectRoute(position):
    target = ResolveCurrentTargetPosition()

    if IsTaskReached(position, CurrentTask):
        CompleteCurrentTask()
        return LocomotionResult.Reached

    if not CanUseDirect(position, target, Entity.Shape):
        SwitchRouteToAStar()
        return EvaluateAStarRoute(position)

    direction = NormalizeDeterministic(target - position)
    return LocomotionResult.Moving(direction, ResolveMoveSpeed())
```

```pseudo
function EvaluateFlowFieldRoute(position):
    cell = Map.WorldToCell(position)

    if not Map.IsValidCell(cell):
        return LocomotionResult.NoRoute

    if IsFlowTaskReached(position, cell, CurrentTask):
        CompleteCurrentTask()
        return LocomotionResult.Reached

    direction = TeamFlowFieldService.GetDirection(
        FlowFieldKey = Route.FlowFieldKey,
        Cell = cell,
        RadiusClass = Entity.Shape.RadiusClass
    )

    if direction == zero:
        return LocomotionResult.Blocked

    return LocomotionResult.Moving(direction, ResolveMoveSpeed())
```

流场每 Tick 读取当前位置格子，因此外部位移后自然使用新区域的方向。

---

### 控制打断与路线生命周期

大多数强制位移控制会通过行为系统打断原 `Action / Intent`，使单位回到受控状态或 `Idle`：

```text
CrowdControlHandler
    -> ActionArbiter / ActionRuntime 执行中断
    -> 原路线任务被 CancelRoute
    -> MovementHandler 执行强制位移
```

因此第一版不设计：

```text
RouteResumePolicy
PauseAndValidateAfterEnd
恢复旧 A* 路径
```

如果某个特殊规则保留移动意图，应由行为层明确保留该意图。控制结束后 Planner 重新提交寻路请求，`UnitLocomotionAgent` 从当前 `PhysicsEntity2D.Position` 重新规划。

---

### 帧同步定位

| 数据 | 标记 |
|---|---|
| `CurrentTask` | `【需要帧同步保存】` |
| `Route.Kind / NeedRepath / NextRepathTick` | `【需要帧同步保存】` |
| `AStarPathCellIndices` | `【需要帧同步保存】` |
| `PathCursor / RouteFinished` | `【需要帧同步保存】` |
| `FlowFieldKey` | 运行时选择会影响未来路线时 `【需要帧同步保存】` |
| `LocomotionResult` | `【单 Tick 临时】` |
| 当前 waypoint 世界坐标 | `【可确定性重建】` |
| A* OpenSet / ClosedSet / 搜索版本号 | `【单次算法临时】` |

---

### 定位

`PathFollower2D` 是 `UnitLocomotionAgent` 内部的路径运行模块。  
它拥有路径游标和路线完成状态，读取 `PhysicsEntity2D.Position`，并帮助构建当前 Tick 的 `LocomotionResult`。

它不写位置，也不向 `MovementHandler` 暴露完整路径。

---

### A* 路径游标推进

```pseudo
function AdvanceCursor(position, path):
    while PathCursor < path.Count:
        waypoint = Map.CellToWorldCenter(path[PathCursor])

        if HasPassedWaypoint(
            position,
            waypoint,
            path,
            PathCursor
        ):
            PathCursor += 1
            continue

        break
```

`HasPassedWaypoint` 同时考虑：

```text
到路径点距离
沿路径前进方向的投影
```

避免单位从路径点侧面经过后游标无法推进。

---

### A* 当前 Tick 结果

```pseudo
function BuildAStarLocomotionResult(position, path, speed):
    if PathCursor >= path.Count:
        RouteFinished = true
        return LocomotionResult.Reached

    waypoint = Map.CellToWorldCenter(path[PathCursor])
    direction = NormalizeDeterministic(waypoint - position)

    return LocomotionResult.Moving(
        desiredDirection = direction,
        desiredSpeed = speed
    )
```

`MovementHandler` 只接收这里产生的单 Tick 结果。

---

### 路径走廊偏离检测

```pseudo
function IsOutsideRemainingPathCorridor(position, path, tolerance):
    nearest = FindNearestSegmentAroundCursor(
        position,
        path,
        PathCursor,
        CorridorBackwardCheckCount,
        CorridorForwardCheckCount
    )

    if nearest not found:
        return true

    return DistanceSqToSegment(position, nearest)
        > tolerance * tolerance
```

RVO 绕行、墙体约束、传送或其它外部位移造成偏离时，下一次 `UnitLocomotionAgent.Evaluate()` 会自然检测到并决定是否重寻路。

---

### Direct 跟随

```pseudo
function BuildDirectLocomotionResult(position, target, stopDistance, speed):
    delta = target - position

    if LengthSq(delta) <= stopDistance * stopDistance:
        return LocomotionResult.Reached

    return LocomotionResult.Moving(
        NormalizeDeterministic(delta),
        speed
    )
```

Direct 是否还能继续使用，由 `UnitLocomotionAgent` 的 `CanUseDirect()` 判断。

---

### 追踪重寻路

```pseudo
function TickChaseRepath(agent, route):
    if SimulationTickContext.Current.Tick < route.NextRepathTick:
        return

    target = ResolveTarget(route.TargetUnitUid)

    if target is null:
        route.Finish(TargetLost)
        return

    targetPos = target.PhysicsEntity.Position

    if DistanceSq(targetPos, route.LastPathTargetPosition)
        >= RepathThresholdSq:
        route.NeedRepath = true

    route.NextRepathTick =
        SimulationTickContext.Current.Tick + ChaseRepathIntervalTicks
```

---

### 流场跟随

```pseudo
function BuildFlowFieldLocomotionResult(agent, route):
    position = agent.Entity.Position
    cell = Map.WorldToCell(position)

    direction = FlowField.GetDirection(
        FlowFieldKey = route.FlowFieldKey,
        Cell = cell,
        RadiusClass = agent.Entity.Shape.RadiusClass
    )

    if direction == zero:
        return LocomotionResult.Blocked

    return LocomotionResult.Moving(
        direction,
        GetMoveSpeed(agent)
    )
```

---

### 帧同步定位

```text
PathCursor
RouteFinished
    【需要帧同步保存】
```

不保存：

```text
当前 waypoint 世界坐标
当前路径段
当前流场方向
LocomotionResult
```

这些均可从路径、游标、当前位置和静态配置确定性计算。

第一版不引入跨 Tick 有状态转向平滑，因此不存在 `SmoothedDirection` 状态。

---

### `UnitUid`

单位运行时统一使用：

```text
UnitUid
    int SpawnLogicTick
    int RuntimeEntityPrefabId
    byte SpawnSequenceInTick
```

比较规则：

```text
SpawnLogicTick 小优先
相同则 RuntimeEntityPrefabId 小优先
仍相同则 SpawnSequenceInTick 小优先
```

`UnitUid` 的权威来源是 `Unit / UnitWorld`；移动系统只读取并用于稳定排序、任务目标和查询键。

移动系统不定义通用 `UnitUid`。  
投掷物使用独立类型 `ProjectileUid`，二者结构可以一致，但语义类型分离。

---

### `RadiusClass`

```text
RadiusClass
    Small
    Medium
    Large
```

与物理系统正式提供的单位 `Radius` 查询值同源配置。  
A*、流场、普通移动和墙体修正必须使用一致半径语义。

---

### `MovementMode`

```text
MovementMode
    Idle
    RouteMove
    Dash
    ForcedMove
```

它是派生值：

```text
ForcedMove.IsActive
Dash.IsActive
LocomotionResult.HasMovement
```

传送和一次性修正不是持续模式。  
`MovementMode` 不作为独立跨 Tick 状态保存。

---

### `PathResult`

```text
PathResult
    bool Success
    PathStatus Status
    int[] PathCellIndices
```

```text
PathStatus
    Success
    InvalidStart
    InvalidEnd
    EndBlocked
    NoPath
    MaxIterationReached
    SystemNotReady
```

运行时保存格子索引，不长期保存重复的世界坐标路径点。

---

### `MovementTask`

```text
MovementTask
    MovePurpose Purpose
    MoveTarget Target

    fp StopDistance
    bool AllowRVO
    bool AllowRepath

    MovementTaskState State
```

路线细节放在 `RouteRuntime`，避免任务语义与算法状态重复。

---

### `RouteRuntime`

```text
RouteRuntime
    RouteKind Kind

    bool NeedRepath
    int NextRepathTick
    fp2 LastPathTargetPosition

    int[] AStarPathCellIndices
    FlowFieldKey FlowFieldKey
```

`PathCursor` 由 `PathFollower2D` 持有。  
不重复保存单位阵营；队伍级流场由 `FlowFieldKey` 明确选择。

---

### `LocomotionResult`

```text
LocomotionResult
    UnitUid UnitUid

    bool HasMovement
    bool AllowRVO

    fp2 DesiredDirection
    fp DesiredSpeed
    fp2 DesiredVelocity

    RouteEvaluationStatus Status
```

```text
RouteEvaluationStatus
    Idle
    Moving
    Reached
    Blocked
    NoRoute
    TargetLost
    Cancelled
```

`LocomotionResult` 是 `UnitLocomotionAgent` 输出的单 Tick 值，不进入跨 Tick 状态。

---

### `RvoResult`

```text
RvoResult
    UnitUid UnitUid
    fp2 FinalVelocity
```

`RvoResult` 是单 Tick 值，不进入跨 Tick 状态。

---

### `DashRuntime`

```text
DashRuntime
    bool IsActive
    int StartTick
    DashConfigId ConfigId

    fp2 StartPosition
    fp2 Direction
    fp2 TargetPosition

    DashWallPolicy WallPolicy
```

`ElapsedTicks` 由：

```text
SimulationTickContext.Current.Tick - StartTick
```

确定性计算，第一版不重复保存。

---

### `ResolvedForcedMove`

`CrowdControlHandler` 完成控制仲裁后，向 `MovementHandler` 提交：

```text
ResolvedForcedMove
    CrowdControlHandle SourceControlHandle
    ForcedMoveConfigId ConfigId

    int DurationTicks
    fp2 Direction
    fp2 TargetPosition

    ForcedMoveWallPolicy WallPolicy
```

它不包含：

```text
Priority
Immunity
StackRule
RouteResumePolicy
```

这些不属于移动轨迹执行。

---

### `ForcedMoveRuntime`

```text
ForcedMoveRuntime
    bool IsActive
    CrowdControlHandle SourceControlHandle

    int StartTick
    int DurationTicks

    fp2 StartPosition
    fp2 Direction
    fp2 TargetPosition

    ForcedMoveConfigId ConfigId
    ForcedMoveWallPolicy WallPolicy
```

`ElapsedTicks` 由 `SimulationTickContext.Current.Tick - StartTick` 计算。  
控制优先级只存在于 `CrowdControlHandler`。

---


## 需求演进

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

