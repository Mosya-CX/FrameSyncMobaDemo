# 冲刺控制位移与传送

## 本功能范围

本案细化“普通移动冲刺与强制位移”中的冲刺控制位移与传送，仅覆盖下列明确接口与边界。

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

### Dash

Dash 默认不走普通寻路，也不经过 RVO：

```pseudo
function AdvanceDash():
    delta = Dash.EvaluateDelta(
        SimulationTickContext.Current.Tick
    )

    if Dash.WallPolicy == StopAtWall:
        delta = ResolveStaticWall(
            Entity.Position,
            delta,
            Entity.Shape
        )

    newPosition = Entity.Position + delta

    newForward = ResolveForward(
        delta,
        Entity.Forward
    )

    Entity.SetLogicPose(
        newPosition,
        newForward
    )

    if Dash.IsFinished(
        SimulationTickContext.Current.Tick
    ):
        Dash.End()
        RequestPostMoveWallValidation()
```

Dash 是否打断当前 Action / Intent，由行为与技能系统决定。  
`MovementHandler` 不保存或恢复旧路径。

### 强制位移：控制系统唯一仲裁，移动系统只执行

#### 仲裁规则

同一单位同时最多只有一个强制位移控制实例生效。  
`CrowdControlHandler` 在新请求进入时执行：

```pseudo
function CrowdControlHandler.TryAddForcedMove(request):
    if ActiveForcedMoveControl is null:
        instance = AddControlInstance(request)
        ActiveForcedMoveControl = instance

        MovementHandler.StartForcedMove(
            BuildResolvedForcedMove(instance)
        )
        return Accepted

    current = ActiveForcedMoveControl

    if request.Priority < current.Priority:
        return RejectedByHigherPriority

    // 新请求优先级更高或相同，新实例替换旧实例。
    RemoveControlInstance(
        current,
        reason = Replaced
    )

    instance = AddControlInstance(request)
    ActiveForcedMoveControl = instance

    MovementHandler.ReplaceForcedMove(
        BuildResolvedForcedMove(instance)
    )

    return Accepted
```

强制位移只在控制实例 `OnAdd` 时启动一次。  
控制实例存续期间不重复向 `MovementHandler` 提交。

`MovementHandler` 不比较：

```text
控制优先级
控制免疫
控制叠加
当前应该生效哪个控制
```

#### 轨迹执行

```pseudo
function AdvanceForcedMove():
    delta = ForcedMove.EvaluateDelta(
        currentPosition =
            Entity.Position,
        tick = SimulationTickContext.Current.Tick
    )

    if ForcedMove.WallPolicy == StopAtWall:
        delta = ResolveStaticWall(
            Entity.Position,
            delta,
            Entity.Shape
        )

    newPosition =
        Entity.Position + delta

    newForward = ResolveForward(
        delta,
        Entity.Forward
    )

    Entity.SetLogicPose(newPosition, newForward)

    if ForcedMove.IsFinished(SimulationTickContext.Current.Tick):
        // 是否移除控制由 CrowdControlHandler 的生命周期负责。
        // MovementHandler 只结束轨迹执行状态。
        ForcedMove.End()
        RequestPostMoveWallValidation()
```

#### 启动、替换与停止

```pseudo
function StartForcedMove(resolved):
    assert not ForcedMove.IsActive
    ForcedMove.Begin(
        resolved,
        startPosition = Entity.Position,
        startTick = SimulationTickContext.Current.Tick
    )

function ReplaceForcedMove(resolved):
    ForcedMove.ReplaceAtomically(
        resolved,
        startPosition = Entity.Position,
        startTick = SimulationTickContext.Current.Tick
    )

function StopForcedMove(sourceHandle):
    if not ForcedMove.IsActive:
        return

    if ForcedMove.SourceControlHandle
        != sourceHandle:
        return

    ForcedMove.End()
```

替换是原子操作，中间不恢复普通路线或 Idle。

---

### 控制打断与寻路关系

普通控制效果一般会打断单位此前的 Action / Intent。  
行为系统负责调用：

```text
UnitLocomotionAgent.CancelRoute(...)
```

然后 `MovementHandler` 执行强制位移。  
因此不设计：

```text
RouteResumePolicy
恢复旧路径
强制位移结束后继续旧 PathCursor
```

如果特殊规则保留移动意图，控制结束后 Planner 重新产生寻路请求。  
`UnitLocomotionAgent` 从 `PhysicsEntity2D` 当前实际位置重新规划。

---

### 传送与位置修正

```pseudo
function ApplyTeleport(position, forward, reason):
    Entity.TeleportLogicPosition(position)

    if forward != zero:
        Entity.SetLogicForward(forward)

    RequestPostMoveWallValidation()
```

`TeleportLogicPosition()` 必须采用非连续位移语义：

```text
Position = 目标位置
PrevPosition = 目标位置
```

传送不会生成从旧位置到目标位置的普通 Sweep。

```pseudo
function ApplyMovementCorrection(
    delta,
    reason
):
    if delta == zero:
        return

    Entity.ApplyLogicPositionDelta(delta)
```

同一 Tick 先移动、再修正时，`ApplyLogicPositionDelta()` 不得覆盖本 Tick 第一次连续写入锁存的 `PrevPosition`。

寻路系统不依赖专门的“位置变化通知”来发现路径偏离。  
下一次 `UnitLocomotionAgent.Evaluate()` 会读取最新位置并执行正常路线验证。

如果传送或规则明确要求清空当前路线，由行为层调用 `CancelRoute()`。

### 正式空间提交接口

`MovementHandler` 不定义自有 `SetLogicPose()`，也不直接写物理内部空间字段。

```text
普通移动、Dash、强制位移：
    Entity.SetLogicPose(position, forward)

墙体挤出、轻量位置修正：
    Entity.ApplyLogicPositionDelta(delta)

传送：
    Entity.TeleportLogicPosition(position)
    Entity.SetLogicForward(forward)   // 需要改变朝向时

Idle：
    Entity.SetLogicPose(
        Entity.Position,
        Entity.Forward
    )
```

Idle 仍提交当前姿态，使物理系统能够按正式规则把本 Tick 的 `PrevPosition` 锁存为当前 `Position`。  
第一版固定采用该方案，不再额外引入 `BeginLogicTick()` 的第二套锁存路径。

`MovementHandler` 是单位空间变化的业务提交入口；  
`PhysicsEntity2D` 是正式空间状态与写入 API 的提供者。

### 正式死亡时的移动模块清理接缝

死亡规则、`Alive / Dying / Dead` 转换及正式死亡时机由战斗系统和 `UnitWorld` 决定。  
寻路与移动系统不维护第二份生命状态，也不自行判断单位何时死亡。

正式进入 `Dead` 的同一 Handler 清理调用链中，单位框架调用：

```text
CrowdControlHandler.ClearForDeath()
    清除当前控制实例，并停止其对应强制位移来源。

MovementHandler.ClearForDeath()
    终止 DashRuntime。
    清除仍残留的 ForcedMoveRuntime。
    清除单 Tick 移动执行缓存。
    不直接修改 LifeState。

UnitLocomotionAgent.ClearForDeath()
    取消当前 MovementTask。
    清除 A* 路径、PathCursor、NeedRepath 和追踪重寻路状态。
```

每个模块只清理自己拥有的运行状态：

```text
CrowdControlHandler：
    拥有控制实例和强制位移控制来源。

MovementHandler：
    拥有 Dash / ForcedMove 的轨迹执行状态。

UnitLocomotionAgent：
    拥有任务、路线、路径和路径跟随状态。
```

不允许：

```text
MovementHandler 清空 CrowdControlHandler 的控制列表
UnitLocomotionAgent 修改 LifeState
PhysicsEntity2D 自行决定死亡后的空间处置
移动系统注销 AIController 或实体
```

死亡发生前本 Tick 已经提交的逻辑移动不由本模块回退。  
正式死亡清理完成后，后续 Tick 不再输出或执行主动移动。

帧同步标记：

```text
ClearForDeath 后的模块状态
    【需要在该 Tick 保存的最终 Gameplay 状态中体现】

死亡调用顺序
    【由 CombatSystem / UnitWorld / 单位框架正式冻结】
```

### 帧同步定位

| 数据 | 标记 |
|---|---|
| `DashRuntime` 当前阶段所需字段 | `【需要帧同步保存】` |
| `ForcedMoveRuntime` 当前轨迹执行字段 | `【需要帧同步保存】` |
| `MovementMode` | `【可确定性重建】` |
| `LocomotionResult / RvoResult` | `【单 Tick 临时】` |
| 当前路径与游标 | 属于 `UnitLocomotionAgent`，本模块不保存 |
| 控制优先级和活动控制实例 | 属于 `CrowdControlHandler`，本模块不保存 |
| 位置与朝向 | 属于 `PhysicsEntity2D` 对应状态，本模块不重复保存 |
| `CanRunActiveGameplayThisTick` | `【可确定性推导】`，不进入快照 |
| 死亡清理后的 Dash / ForcedMove 状态 | 清理后的最终状态由帧同步系统在该 Tick 快照中体现 |

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

