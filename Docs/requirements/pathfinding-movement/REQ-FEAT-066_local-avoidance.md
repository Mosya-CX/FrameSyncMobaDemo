# 确定性局部避让

## 目标实现

多单位移动先收集同时间点偏好速度，再稳定求可执行速度。

## 技术方案

DeterministicRVOSystem 读取移动前 RvoGrid，以固定候选速度和稳定约束求解；全部结果计算后再应用。

## 边界情况

技术遍历不形成先移动优势；零速度、重叠、狭窄通道和速度上限有可判定边界。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/DeterministicRVOSystem.cs`：当前关联实现定义 DeterministicRVOSystem（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/RvoResult.cs`：当前关联实现定义 RvoResult（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/MapPathfindingAssetIntegrationTests.cs`：MapPrefab_OwnsGridLanesFieldsAndVisualizer、RotatedThinObstacle_DoesNotBecomeAabbSquare、MiddleLaneFlow_PullsTowardLaneProgressively、StraightLaneSkeletonCells_UseAuthoredTangents、FoundationJunctions_DistinguishDepartureFromArrivalTarget、LaneOwnershipBoundaries_HaveDirectionsAndBypassVisualizerStride、SixLaneDirections_FollowForwardWaypointsInOrder。
- `Assets/Scripts/Gameplay/Tests/MovementConformanceTests.cs`：ForcedMove_OverridesRouteMove、EqualPriorityForcedMove_ReplacesAtomically、ForcedMoveSnapshot_RoundTripsWithControlOwner、Dash_RequiresPositiveDuration_AndOverridesRoute、MovementCollision_UsesPhysicsShapeRadius、RvoGrid_IncludesIdleUnitAsObstacle、RvoNeighborSelection_IsInputOrderIndependent。
- `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs`：FiveAgents_ConvergingOnCenter_NoDeadlock、TwoUnitsHeadOn_VelocitiesDiverge、TenAgents_RandomSpread_CompletesWithoutError、RVO_Deterministic_SameInputProducesSameOutput、RVO_ZeroDesiredVelocity_ReturnsZero、FlowDirection_FromFarCell_PointsTowardTarget、FlowDirection_AtTarget_ReturnsZero。
- `Assets/Scripts/Gameplay/Tests/RVOSystemTests.cs`：SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge、SolveAvoidance_NoNeighbors_ReturnsDesired、SolveAvoidance_Deterministic_SameInputSameOutput、SolveAvoidance_ZeroDesiredVelocity_ReturnsZero、SolveAvoidance_StableOrdering_IndependentOfInputOrder。
- `Assets/Scripts/Gameplay/Tests/IntegratedPathfindingPipelineTests.cs`：LaneAdvance_SelectsTeamFlowField、Chase_FirstTickBuildsPathBeforeRepathCooldown、Chase_DoesNotCompleteWhileOutsideAttackRange_AtPathDestinationCell、ChaseForCast_DoesNotCompleteAtAttackBoundaryDistance、PointMove_UsesDirectOrAStarByGrid、FlowFieldRvoMovement_ProducesRepeatableMotion、RadiusAwareLineOfSight_BlocksLargeUnit。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 最小依赖边界

本文只规定移动系统依赖的物理接口：

```text
PhysicsWorld
    RegisterEntity(PhysicsEntity2D entity)
    UnregisterEntity(PhysicsEntity2D entity)

    BuildRvoGrid(unitEntities)
    BuildUnitFinalGrid(unitEntities)

    RvoGrid RvoGrid
    UnitFinalGrid UnitFinalGrid

    MovementCorrectionRequest DetectWallPenetration(entity)
```

`PhysicsWorld` 不直接写单位位置。

---

### 两个网格必须独立

```text
RvoGrid
    使用本 Tick 移动前位置构建。
    服务 RVO 邻居查询。

UnitFinalGrid
    使用所有移动与修正完成后的最终位置构建。
    服务本 Tick 后续空间查询。
```

二者可以复用相同容器实现，但不能共享当前桶内容，因为时间语义不同。

---

### 注册规则

所有有效单位空间实体都进入两个网格，不按以下状态提前过滤：

```text
Capability.IsTargetable
IsSelectable
CanReceiveHit
```

`PhysicsWorld` 按物理系统正式的实体有效性与注册规则构建网格。  
移动系统不重复定义 Owner 绑定、实体类型判定或 UID 查询实现。

RVO 查询只保留：

```text
有效且已注册的单位实体
排除自身 UnitUid
按稳定 UnitUid 排序
```

### RVO 输入输出

```text
RVOInput
    UnitUid SelfUnitUid
    fp2 Position
    fp2 DesiredVelocity
    fp Radius
    fp MaxSpeed

RvoResult
    UnitUid UnitUid
    fp2 FinalVelocity
```

`RVOInput / RvoResult` 均为单 Tick 数据，不进入跨 Tick状态。

---

### RVO 主流程

```pseudo
function DeterministicRVOSystem.Step(
    locomotionResults,
    rvoGrid
):
    for result in locomotionResults sorted by result.UnitUid:
        if not result.HasMovement:
            outputs[result.UnitUid] =
                RvoResult(result.UnitUid, zero)
            continue

        entity = ResolveUnitPhysicsEntity(result.UnitUid)

        input = RVOInput(
            SelfUnitUid = result.UnitUid,
            Position = entity.Position,
            DesiredVelocity = result.DesiredVelocity,
            Radius = entity.Shape.Radius,
            MaxSpeed = result.DesiredSpeed
        )

        bounds = Expand(
            entity.Bounds,
            Settings.NeighborSearchRadius
        )

        neighbors = rvoGrid.Query(bounds)

        RemoveSelf(neighbors, input.SelfUnitUid)
        SortByUnitUid(neighbors)
        TrimToMaxNeighbors(
            neighbors,
            Settings.MaxNeighbors
        )

        outputs[result.UnitUid] =
            SolveAvoidance(input, neighbors)
```

`ResolveUnitPhysicsEntity()`、`entity.Position / Shape / Bounds` 表示读取物理系统正式提供的空间查询接口，本文不定义其身份绑定实现。

RVO 统一读取所有单位的移动前位置和当前 Tick `LocomotionResult`，不能在单位逐个提交位置时边走边求解。

### 确定性候选速度求解

```pseudo
function SolveAvoidance(input, neighbors):
    best = input.DesiredVelocity
    bestPenalty = EvaluateVelocity(input, best, neighbors)

    for candidate in GenerateVelocitySamplesDeterministically(
        desired = input.DesiredVelocity,
        maxSpeed = input.MaxSpeed
    ):
        penalty = EvaluateVelocity(input, candidate, neighbors)

        if penalty < bestPenalty:
            best = candidate
            bestPenalty = penalty

        else if penalty == bestPenalty:
            if VelocityTieBreaker(candidate)
                < VelocityTieBreaker(best):
                best = candidate

    return RvoResult(
        UnitUid = input.SelfUnitUid,
        FinalVelocity = best
    )
```

邻居和候选速度的遍历顺序必须固定。

---

### 帧同步定位

| 数据 | 标记 |
|---|---|
| `RvoGrid` | `【可确定性重建】` |
| `UnitFinalGrid` | `【可确定性重建】` |
| RVO 邻居列表 | `【单 Tick 临时】` |
| `LocomotionResult / RvoResult` | `【单 Tick 临时】` |
| RVO 配置 | `【静态配置】` |

第一版 RVO 不依赖上一 Tick 速度，因此不引入 RVO 历史速度状态。

如果 `PhysicsWorld` 的单位碰撞事件模块维护 `PreviousPairs`，该数据需要帧同步负责人纳入物理运行状态；它不属于本文的移动状态定义。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

