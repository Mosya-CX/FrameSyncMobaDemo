# 移动状态快照与重建

## 本功能范围

本案细化“普通移动冲刺与强制位移”中的移动状态快照与重建，仅覆盖下列明确接口与边界。

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

### 恢复后的移动系统重建需求

本文不定义快照恢复总流程，只标记移动相关依赖：

```text
恢复 PhysicsEntity2D 空间状态
恢复 MovementHandler 的 Dash / ForcedMove 轨迹状态
恢复 UnitLocomotionAgent 的任务、路径和游标
由物理系统重建派生空间数据与 Bounds
PhysicsWorld 重新注册有效实体
重建 RvoGrid 与 UnitFinalGrid
从 `SimulationTickContext.Current.Tick` 指定的下一 Tick 继续模拟
```

如果 `PhysicsWorld` 还有单位碰撞 `PreviousPairs` 等跨 Tick 状态，应由物理与帧同步设计负责恢复。

---

### . 帧同步服务标记

> 本章只帮助帧同步设计师定位会影响回滚重演的数据。  
> 不定义顶层快照树、聚合方式、序列化格式或恢复协议。

### `PhysicsEntity2D` 依赖

```text
PhysicsEntity2D 空间状态：
    【需要由物理回滚服务恢复】

同一 Tick PrevPosition 锁存状态：
    【由物理与帧同步设计师决定保存或在恢复边界重建】

运行时形状状态：
    【仅当玩法允许跨 Tick 改变形状时需要恢复】

Bounds、物理注册索引、查询缓存：
    【可确定性重建】
```

本文不列出 `PhysicsEntity2D` 的具体快照字段，也不定义 Owner 绑定、Capture、Restore、Resolve 或 Rebuild 实现。

寻路与移动系统只要求：恢复完成后，`Position / Forward / Radius / RadiusClass / Bounds` 的读取结果与原模拟一致。

### `MovementHandler`

需要帧同步保存：

```text
DashRuntime
    IsActive
    StartTick
    ConfigId
    StartPosition
    Direction
    TargetPosition
    WallPolicy

ForcedMoveRuntime
    IsActive
    SourceControlHandle
    StartTick
    DurationTicks
    StartPosition
    Direction
    TargetPosition
    ConfigId
    WallPolicy
```

不需要保存：

```text
MovementMode
LocomotionResult
RvoResult
静态墙体计算局部变量
当前位置与朝向
控制优先级
```

原因：

```text
MovementMode 可从运行状态推导。
LocomotionResult / RvoResult 每 Tick 重算。
位置与朝向归 PhysicsEntity2D。
控制优先级归 CrowdControlHandler。
```

---

### `UnitLocomotionAgent`

需要帧同步保存：

```text
是否存在当前任务
MovementTask
    Purpose
    Target
    StopDistance
    AllowRVO
    AllowRepath
    State

RouteRuntime
    Kind
    NeedRepath
    NextRepathTick
    LastPathTargetPosition
    AStarPathCellIndices
    FlowFieldKey

PathFollower2D
    PathCursor
    RouteFinished
```

不需要保存：

```text
当前 waypoint 世界坐标
当前路径段
当前距离路径走廊的距离
当前流场方向
LocomotionResult
A* OpenSet / ClosedSet / SearchId / IndexedMinHeap
```

这些都能从已恢复的空间状态、路线状态和静态配置重建。

---

### `PhysicsWorld` 与 RVO

可确定性重建：

```text
RvoGrid
UnitFinalGrid
Cell Buckets
PhysicsEntity2D.Bounds
RVO 邻居列表
RVO 候选速度
```

单 Tick 临时：

```text
RVOInput
RvoResult
MovementCorrectionRequest
墙体穿透窄相局部结果
```

物理系统如果维护单位碰撞事件的：

```text
PreviousPairs
```

它会影响下一 Tick 的 `Enter / Exit` 判断，需要帧同步系统与物理系统保存；本文只标记该依赖，不定义其结构。

### 静态配置

不进入运行时快照：

```text
PathGridMap2D
BaseWalkable
Clearance
WalkableByRadiusClass
离线流场 Cost / OwnerLane / DirectionCode / NextCell
A* 和 RVO 配置
DashConfig
ForcedMoveConfig
地图中心、轴向和 CenterY 的 Bake 数据
```

所有客户端和服务端必须使用相同版本的 Bake 数据。

---

### 快速审计规则

| 问题 | 判断 |
|---|---|
| 恢复后该字段是否会改变下一 Tick 的寻路或移动结果？ | 是：需要帧同步保存。 |
| 能否从已恢复的权威状态和静态配置唯一重建？ | 能：可重建，不重复保存。 |
| 是否只是当前 Tick 的输出值或算法局部量？ | 是：单 Tick 临时。 |
| 是否在 `PhysicsEntity2D`、`MovementHandler`、`UnitLocomotionAgent` 中重复表达同一状态？ | 删除非权威副本。 |
| 是否属于 `CrowdControlHandler` 的控制仲裁状态？ | 不在移动模块重复保存。 |
| 是否属于静态地图、流场或配置数据库？ | 配置版本校验，不进运行时快照。 |

---

### 必须保留

| 功能 | 原因 |
|---|---|
| `MovementHandler` | 适配 Unit Handler 架构，统一消费普通移动结果并执行特殊移动。 |
| `UnitLocomotionAgent` | 单一寻路入口，每 Tick 维护路线并输出 `LocomotionResult`。 |
| `PhysicsEntity2D` 正式物理接口依赖 | 保证寻路、移动和物理系统读取、提交同一份空间状态。 |
| `PathGridMap2D` | A*、流场、静态墙体和网格坐标共用。 |
| 半径通行层 | 保证 A* 结果与实际单位体积一致。 |
| A* SearchId | 避免每次全图重置。 |
| Indexed Heap + DecreaseKey | 控制 A* 堆大小和无效弹出。 |
| 目标不可走附近搜索 | 点击目标、追踪单位和靠近建筑必需。 |
| 队伍级流场 + OwnerLane | 允许小兵跨兵线，同时避免向量融合不稳定。 |
| 成本递减贴墙评分 | 保留贴墙效果且不破坏流场正确性。 |
| 独立 `RvoGrid / UnitFinalGrid` | 分别表达移动前与移动后的空间时间切片。 |
| RVO | 处理动态单位避让。 |
| 提交前静态墙体约束 | 普通移动不能依赖穿墙后挤出。 |
| 异常墙体修正请求 | 处理传送、Dash、强制位移造成的异常进入墙体。 |
| 帧同步状态标记 | 便于帧同步设计师定位必须保存或可重建的数据。 |

---

### 删除或弱化

| 内容 | 处理 |
|---|---|
| `MovementAbilityHandler` | 删除，统一使用 `MovementHandler`。 |
| `MovementMotorState / RVOAgentState` | 删除旧快照名，按真实状态重新划分。 |
| `MovementSpatialIndex2D` | 删除旧名，统一为 `RvoGrid / UnitFinalGrid`。 |
| `UnitPhysicsWorld / UnitPhysicsAgent` | 删除旧名。 |
| `UnitLocomotionAgent` 位置写入职责 | 删除；由 `MovementHandler` 提交，`PhysicsEntity2D` 保存空间数据。 |
| 独立有状态 `MovementMotor2D` | 不再作为状态所有者；算法并入 `MovementHandler`。 |
| 流场运行时 BuildPath | 仅保留调试用途。 |
| 流场向量 Lerp / Average | 删除，改为离散成本递减候选。 |
| 运行时 Unity Physics / float / Vector3 | 禁止。 |
| 在空间网格构建时过滤 Targetable | 禁止。 |
| 完整投掷物和范围查询设计 | 不在本文展开。 |
| 寻路文档内重复的 `PhysicsEntity2D / LogicTransform / OwnerBinding / SetLogicPose` 定义 | 删除，统一引用物理系统正式契约。 |

---

### 本轮明确删除

```text
MovementHandler 保存完整路径或 PathCursor
MovementHandler 恢复旧路径
RouteResumePolicy
ForcedMoveRuntime.Priority
ForcedMoveRuntime.ElapsedTicks
DashRuntime.ElapsedTicks
跨 Tick CurrentVelocity（第一版未启用惯性）
PathFollower2D.SmoothedDirection（第一版未启用有状态平滑）
移动模块中的通用 LogicEntityUid 类型
UnitLocomotionAgent 直接写位置
寻路文档内重复定义 PhysicsEntity2D / LogicTransform / OwnerBinding / SetLogicPose 实现
强制位移期间保留并恢复旧 A* 路径的通用机制
```

控制是否保留原移动意图由行为层决定；保留时重新提交寻路任务。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

