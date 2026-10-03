# 移动改造实施顺序与帧算法

## 本功能范围

本案细化“普通移动冲刺与强制位移”中的移动改造实施顺序与帧算法，仅覆盖下列明确接口与边界。

## 目标实现

普通路线、Dash、控制位移与传送均由统一移动入口提交空间。

## 技术方案

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

## 边界情况

死亡清理移动任务；仲裁决定占用而不让技能控制直接写 Transform；墙体约束与异常挤出分开。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### 阶段一：冻结职责与接口

1. 冻结物理系统正式 `PhysicsEntity2D` 依赖契约，不在本文重复定义。
2. 冻结 `UnitLocomotionAgent` 的寻路入口与单 Tick 输出接口。
3. 冻结 `MovementHandler` 的移动提交与空间应用接口。
4. 冻结 `CrowdControlHandler -> MovementHandler` 的强制位移交接接口。
5. 所有 Tick 相关逻辑统一从 `SimulationTickContext.Current` 读取 Tick 上下文，禁止把上下文加入业务接口参数。

### 阶段二：单位装配

1. 单位挂载 `PhysicsEntity2D` 和 `UnitLocomotionAgent`。
2. `Unit` 内装配 `MovementHandler`。
3. 建立三者显式引用。
4. Presentation Sync 只读 `PhysicsEntity2D` 的最终逻辑姿态并更新 Unity Transform。

### 阶段三：地图与半径通行层

1. 完成中心 Transform、旋转轴向和忽略缩放的 Bake。
2. 完成 `Clearance / WalkableByRadiusClass`。
3. 统一 A*、流场、普通移动和墙体修正的半径语义。

### 阶段四：A* 与路径跟随

1. 完成 `IndexedMinHeap + DecreaseKey`。
2. 完成 SearchId 状态复用。
3. 完成目标不可走附近点查询。
4. 完成确定性 LOS 简化。
5. 完成 `PathCursor` 推进和路径走廊偏离检测。
6. `UnitLocomotionAgent` 只输出 `LocomotionResult`。

### 阶段五：流场

1. 完成每兵线积分成本场。
2. 完成队伍级 `OwnerLane` 合并。
3. 完成成本递减约束的贴墙候选评分。
4. 运行时只读当前格子方向。

### 阶段六：移动执行

1. `MovementHandler` 消费 `LocomotionResult / RvoResult`。
2. 完成普通移动和静态墙体约束。
3. 完成 Dash。
4. 完成强制位移轨迹执行。
5. 完成传送与异常位置修正。
6. 最终调用 `SetLogicPose / ApplyLogicPositionDelta / TeleportLogicPosition`。

### 阶段七：控制与 RVO

1. `CrowdControlHandler` 实现唯一强制位移实例仲裁。
2. 同优先级新控制替换旧控制。
3. `MovementHandler.ReplaceForcedMove()` 原子替换轨迹。
4. 构建移动前 `RvoGrid`。
5. 完成稳定 UnitUid 排序和固定候选速度求解。
6. 构建移动后 `UnitFinalGrid`。

### 阶段八：帧同步联调

1. 帧同步设计师根据第 15 章标记确定正式快照结构。
2. 验证恢复后路径、游标、Dash 和强制位移轨迹一致。
3. 验证 `RvoGrid / UnitFinalGrid / Bounds` 可确定性重建。
4. 验证 `ServerAuthority / ClientPrediction / ClientReplay` 结果一致。
5. 验证小兵传送到其它兵线后使用新区域流场。
6. 验证控制打断路线后回归 Idle，特殊保留意图时能重新规划。

---

### 附录：一帧移动伪代码总览

```pseudo
function TickUnitMovementPipeline(
    units,
    physicsWorld
):
    locomotionResults.Clear()
    rvoResults.Clear()

    // 1. Handler Tick 已由 UnitWorld 正常执行。
    // 生成 Tick 仍推进被动状态，但普通主动寻路结果必须为 Idle。
    for unit in units sorted by unit.UnitUid:
        if unit.UnitLocomotionAgent is null:
            continue

        result =
            unit.UnitLocomotionAgent.Evaluate()

        locomotionResults.Add(result)

    // 2. 使用移动前位置构建 RVO 邻居索引。
    physicsWorld.BuildRvoGrid(units)

    // 3. 基于全部单 Tick 寻路结果统一求解。
    rvoResults =
        DeterministicRVOSystem.Step(
            locomotionResults,
            physicsWorld.RvoGrid
        )

    // 4. MovementHandler 执行并提交空间变化。
    for unit in units sorted by unit.UnitUid:
        locomotion =
            locomotionResults.GetOrIdle(unit.UnitUid)

        rvo =
            rvoResults.GetOrZero(unit.UnitUid)

        // Advance 不因生成 Tick 整体跳过：
        // 已生效的外部 ForcedMove 仍会正常执行。
        unit.MovementHandler.Advance(
            locomotion,
            rvo
        )

    // 5. 异常墙体挤出只产生修正请求。
    corrections =
        WallPenetrationResolver.Detect(
            units
        )

    for correction in corrections
        sorted by correction.UnitUid:
        unit =
            ResolveUnit(correction.UnitUid)

        unit.MovementHandler.ApplyMovementCorrection(
            correction.Delta,
            correction.Reason
        )

    // 6. 构建移动完成后的最终空间索引。
    physicsWorld.BuildUnitFinalGrid(units)
```

单个 `UnitLocomotionAgent` 的 A* 评估：

```pseudo
function EvaluateAStarRoute(position):
    if Route.NeedRepath:
        RebuildPathFromCurrentPosition()

    PathFollower.AdvanceCursor(
        position,
        Route.AStarPathCellIndices
    )

    if IsTaskReached(position):
        CompleteTask()
        return LocomotionResult.Reached

    if PathFollower.IsOutsideRemainingPathCorridor(
        position,
        Route.AStarPathCellIndices,
        PathCorridorTolerance
    ):
        Route.NeedRepath = true
        RebuildPathFromCurrentPosition()
        PathFollower.ResetCursorForNewPath()

    return PathFollower.BuildAStarLocomotionResult(
        position,
        Route.AStarPathCellIndices,
        ResolveMoveSpeed()
    )
```

强制位移接入：

```pseudo
function CrowdControlHandler.OnForcedMoveAdded(
    controlInstance
):
    resolved =
        BuildResolvedForcedMove(controlInstance)

    if replacingCurrent:
        MovementHandler.ReplaceForcedMove(
            resolved
        )
    else:
        MovementHandler.StartForcedMove(
            resolved
        )
```

`MovementHandler` 只执行当前有效轨迹；  
控制优先级和唯一实例选择均由 `CrowdControlHandler` 完成。


## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

