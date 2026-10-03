# 单位移动与空间写入接缝

## 本功能范围

本案细化“单位根与能力装配”中的单位移动与空间写入接缝，仅覆盖下列明确接口与边界。

## 目标实现

单位由明确类型、空间引用、属性与 Handler 能力组成。

## 技术方案

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

## 边界情况

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### MovementHandler

`MovementHandler` 是单位侧所有位移执行的统一入口，但必须区分三种语义：

| 类型 | 入口来源 | 是否经过 ActionArbiter | 是否检查 `CanMove` | 是否播放普通移动动画 |
|---|---|---:|---:|---:|
| 主动移动 | `MoveActionRuntime` | 是 | 是 | 通常是 |
| Dash / Mobility | `DashActionRuntime` | 是 | 读取对应技能与控制规则 | 由技能表现决定 |
| 强制位移 | `CrowdControlHandler` | 否 | 否 | 否 |

推荐接口：

```csharp
public sealed class MovementHandler : MonoBehaviour
{
    public void StartVoluntaryMove(
        in MoveGoal goal);

    public void StartDash(
        in DashSpec spec);

    public void StartForcedDisplacement(
        in ForcedDisplacementSpec spec);
}
```

职责：

| 职责 | 说明 |
|---|---|
| 表达移动能力存在 | 主动移动能力对应 `UnitAbilityMask.HasMovement` |
| 接收已仲裁主动移动 | `MoveActionRuntime` 调用 |
| 接收已仲裁 Dash | `DashActionRuntime` 调用 |
| 接收强制位移 | `CrowdControlHandler` 直接调用 |
| 桥接移动执行 | 将任务交给 `UnitLocomotionAgent` |
| 暴露移动状态 | 给 Planner 和 Runtime 查询到达、失败、移动中等状态 |
| 管理任务优先级 | 强制位移优先覆盖普通移动执行，具体叠加规则由移动系统定义 |

`Capability.CanMove` 只约束 `StartVoluntaryMove` 对应的主动移动链路，不约束 `StartForcedDisplacement`。

强制位移不代表单位“正在走路”：

```text
不创建 MoveActionRuntime。
不占用 ActionRuntimeSet。
不播放普通移动循环动画。
不要求单位拥有主动移动能力。
```

但如果预制体完全没有 `MovementHandler`，例如固定防御塔，则不能执行强制位移。

`MovementHandler` 不负责：

```text
A*
FlowField
RVO
墙体约束
空间网格
范围查询
单位形状参数
AABB 维护
控制系统不可阻挡判断
控制系统 Signal
```

### UnitLocomotionAgent

`UnitLocomotionAgent` 与 `Unit` 大致平级，不是 Unit 内部普通 Handler。

v27.1 中它的定位保持为“移动结果写入者”。

职责：

| 职责 | 说明 |
|---|---|
| 当前移动任务 | 普通移动、追踪、流场、Direct、Dash、ForcedMove |
| 路线解析 | A* / FlowField / Direct 的选择由移动系统处理 |
| RVO 接入 | 参与动态避障 |
| 速度推进 | 根据移动目的、速度和外部约束计算下一逻辑 Tick 位移 |
| 空间写入 | 通过物理系统正式接口写入 `Unit.PhysicsEntity2D` 的逻辑姿态 |
| 物理修正接收 | 接收墙体挤出、传送、外部位置修正后的结果 |
| 移动状态查询 | 暴露到达、失败、移动中等状态给 `MovementHandler` / Planner |

不再拥有：

```text
LogicPosition2D
Facing2D
Shape
Radius
Bounds
```

这些空间状态由物理系统定义的 `PhysicsEntity2D` 权威保存。  
单位框架只调用其正式读写接口，不重复定义内部 `Transform / Shape / Bounds` 结构，也不直接写 Unity `Transform`。

> **帧同步设计关注点**  
> `UnitLocomotionAgent` 的当前移动任务和会影响下一逻辑 Tick 的移动运行状态需要被帧同步设计师审查；单位框架不规定其具体快照字段。

### PhysicsEntity2D 的单位侧接入边界

单位框架只依赖物理系统公开的最小接口：

```text
读取：
    LogicPosition
    LogicForward
    Bounds 或正式查询视图

写入：
    SetLogicPosition
    SetLogicPose
    ApplyLogicPositionDelta
    TeleportLogicPosition
    SetLogicForward
    SetLogicShape
```

单位侧约定：

| 场景 | 接口 |
|---|---|
| 出生初始化 | `SetLogicPose` |
| 普通移动 / Dash / 强制位移 | `ApplyLogicPositionDelta` 或物理系统指定入口 |
| 传送 | `TeleportLogicPosition` |
| 转向 | `SetLogicForward` |
| 原型形状初始化 | `SetLogicShape` |

单位框架不直接读写：

```text
PhysicsEntity2D.Transform
PhysicsEntity2D.Shape
PhysicsEntity2D.Bounds
Unity Transform
```

空间网格、碰撞求解、Sweep、AABB 更新、查询镜像与 Unity 表现同步都归对应系统。

### 控制移动解耦

控制系统通过两个只读结果和一个直接执行入口接入单位行为框架：

```text
CrowdControlStateView
    当前控制汇总状态与动作限制

CrowdControlBehaviorOverride
    当前稳定胜出的强制行为

MovementHandler.StartForcedDisplacement
    强制位移直接执行入口
```

限制型控制流程：

```text
CrowdControlHandler.Advance
    ↓
CrowdControlHandler.Rebuild
    ↓
CrowdControlStateView
    ├── Unit 刷新粗粒度 CapabilityState
    └── ActionArbiter 在固定阶段直接读取
```

单位框架不需要状态变化回调：

```text
不保存 previous / current。
不发布 OnCrowdControlStateChanged。
不依赖事件补发。
```

因为在同一逻辑 Tick 中，控制系统会先完成汇总，行为系统随后读取最新状态。

强制行为流程：

```text
CrowdControlHandler
    汇总多个强制行为控制并稳定选出胜者
    ↓
CrowdControlBehaviorOverride
    BehaviorType
    Target
    Direction
    Priority
    SourceInstanceId
    ↓
BehaviorPlanner
    优先于普通 Intent 读取
    ↓
MoveActionRequest / AttackActionRequest
    ↓
ActionArbiter
    ↓
MovementHandler / AttackHandler
```

强制位移流程：

```text
CrowdControlHandler
    ↓
MovementHandler.StartForcedDisplacement
    ↓
UnitLocomotionAgent
    ↓
PhysicsEntity2D 正式位移接口
```

边界：

| 控制类型 | 单位框架处理 |
|---|---|
| 恐惧、魅惑、嘲讽 | 通过 `BehaviorOverride -> Planner -> ActionRequest` |
| 禁锢、沉默、眩晕等限制 | 由 `StateView` 影响 Capability 与 Arbiter 判断 |
| 击退、拉取、击飞位移 | 直接调用 `MovementHandler` |
| 不可阻挡 | 控制系统内部处理 |
| CrowdControl Signal | 控制系统内部处理 |

控制系统不会覆盖 `UnitIntent`。强制行为结束后，Planner 可以自然恢复原 Intent。

### 技能位移解耦

技能系统不直接写 `PhysicsEntity2D` 内部状态。

```text
AbilityHandler
    -> DashRequest
    -> ActionArbiter
    -> MovementHandler
    -> UnitLocomotionAgent
    -> PhysicsEntity2D 正式位移接口
```

这样技能系统不关心 A*、RVO、墙体、空间形状，也不会绕过移动仲裁。

### 防御塔

防御塔没有移动能力，但仍然需要空间实体。

| 能力 / 引用 | 状态 |
|---|---|
| `HasMovement` | false |
| `MovementHandler` | 无 |
| `UnitLocomotionAgent` | 可无，或使用静态模式 |
| `PhysicsEntity2D` | 有 |
| `PhysicsProfile2D` | 有，来自 `UnitPrototype` |

防御塔没有 `MovementHandler`，因此不能启动主动移动、Dash，也不能执行强制位移。  
它仍然持有物理系统定义的 `PhysicsEntity2D`，并通过正式注册接口进入物理模拟与范围查询。

---


## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

