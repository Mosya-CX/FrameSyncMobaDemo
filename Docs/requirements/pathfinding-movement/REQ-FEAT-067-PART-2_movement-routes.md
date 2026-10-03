# 移动运行数据与普通路线

## 本功能范围

本案细化“普通移动冲刺与强制位移”中的移动运行数据与普通路线，仅覆盖下列明确接口与边界。

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

### 运行数据

```text
MovementHandler
    Unit Owner                            【查询引用】
    PhysicsEntity2D Entity                【查询引用】

    DashRuntime Dash                      【需要帧同步保存】
    ForcedMoveRuntime ForcedMove          【需要帧同步保存】
```

不保存：

```text
A* 路径
PathCursor
FlowFieldKey
LocomotionResult
RvoResult
MovementMode
控制优先级
```

第一版每 Tick 直接由当前结果计算位移，不引入跨 Tick `CurrentVelocity` 惯性状态。

---

### Tick 执行分流

```pseudo
function MovementHandler.Advance(
    locomotionResult,
    rvoResult
):
    // Handler Tick 在生成 Tick 仍然执行。
    // 外部强制位移属于已生效的被动控制结果，优先正常推进。
    if ForcedMoveRuntime.IsActive:
        AdvanceForcedMove()
        return

    // 普通主动移动与主动 Dash 从 SpawnLogicTick + 1 开始。
    if not Owner.CanRunActiveGameplayThisTick:
        ApplyStationaryPose()
        return

    if DashRuntime.IsActive:
        AdvanceDash()
        return

    if locomotionResult.HasMovement:
        ApplyRouteMovement(
            locomotionResult,
            rvoResult
        )
        return

    ApplyStationaryPose()
```

`MovementMode` 每 Tick 由当前运行状态和 `LocomotionResult` 推导，不作为独立保存字段。

这里不能在函数入口直接因为生成 Tick而 `return`，否则会错误跳过：

```text
已经生效的外部强制位移
墙体修正
传送
生命周期空间初始化
```

主动 Gameplay 门禁只约束普通 RouteMove 与主动 Dash，不阻止 Handler Tick 自身执行。

---

### 普通路线移动

```pseudo
function ApplyRouteMovement(
    locomotion,
    rvo
):
    if not locomotion.HasMovement:
        ApplyStationaryPose()
        return

    finalVelocity =
        locomotion.AllowRVO
        ? rvo.FinalVelocity
        : locomotion.DesiredVelocity

    desiredDelta =
        finalVelocity
        * MovementSettings.LogicSecondsPerTick
        * SimulationTickContext.Current.DeltaTick

    correctedDelta = ResolveStaticWall(
        start = Entity.Position,
        desiredDelta = desiredDelta,
        shape = Entity.Shape
    )

    newPosition =
        Entity.Position
        + correctedDelta

    newForward = ResolveForward(
        correctedDelta,
        Entity.Forward
    )

    Entity.SetLogicPose(
        newPosition,
        newForward
    )
```

`MovementHandler` 不知道当前路线是 A*、流场还是 Direct。  
`SetLogicPose()` 的 `PrevPosition` 锁存和派生空间数据刷新由物理系统正式实现。

### 静态墙体约束

```pseudo
function ResolveStaticWall(start, desiredDelta, shape):
    target = start + desiredDelta

    if Map.IsCircleWalkable(target, shape.Radius):
        return desiredDelta

    localDelta = Map.WorldVectorToLocal(desiredDelta)

    rightDelta =
        Map.AxisRight2D * localDelta.x

    forwardDelta =
        Map.AxisForward2D * localDelta.y

    candidateRight = start + rightDelta
    if Map.IsCircleWalkable(
        candidateRight,
        shape.Radius
    ):
        return rightDelta

    candidateForward = start + forwardDelta
    if Map.IsCircleWalkable(
        candidateForward,
        shape.Radius
    ):
        return forwardDelta

    return zero
```

该算法是 `MovementHandler` 内部无状态逻辑，不作为独立运行状态节点。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

