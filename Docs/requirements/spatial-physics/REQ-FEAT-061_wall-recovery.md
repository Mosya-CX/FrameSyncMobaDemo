# 墙体异常挤出与表现同步

## 目标实现

异常墙内位置可由明确修正入口恢复，Transform 不反写逻辑。

## 技术方案

WallPenetrationResolver 生成正式修正请求；PhysicsEntity2D.LateUpdate 仅同步已提交逻辑姿态，Scene Gizmo 读取相同只读数据。

## 边界情况

传送跳过路径或改变 PrevPosition 的规则明确；重演不逐 Tick 操作表现 Transform；单位侧拥有最终空间提交。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/WallPenetrationResolver.cs`：当前关联实现定义 MovementCorrectionRequest、MovementCorrectionReason、WallPenetrationResolver（以源码为实际命名）。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：当前关联实现定义 PhysicsEntity2D（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsEntity2DPlayModeTests.cs`：OrdinaryPositionAndDelta_AdvancePreviousPosition、Teleport_SetsPreviousAndCurrentToSamePosition、PoseAndForward_NormalizeFacingAndRefreshOffsetBounds、ZeroFacing_PreservesPriorFacingWhilePoseStillMoves、ShapeChange_RefreshesBoundsWithoutChangingPose、SegmentShape_UsesLogicalPoseAndWidthExpandedBounds、RectBounds_RefreshAfterFacingAndPositionChanges。
- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationAddressablesMigrationTests.cs`：ProjectilesAreSplitIntoLogicAndAddressableViews、ProjectileViewRootsAreAtWorldOrigin、MapLogicAndClientViewHaveDisjointResponsibilities、VfxAndAudioLibrariesContainAddressesNotDirectAssets、GameplayConfigurationsHaveNoDirectSpriteDependencies、UiPagesAndPresentationRootsAreAddressableAndOutsideResources、GenericSkillIndicatorsUseSupportedTransparentShader。
- `Assets/Scripts/Bootstrap/Tests/EditMode/UnitAddressablesMigrationTests.cs`：AllFormalUnitEntriesResolveLogicPrefabAndAddressableView、LogicPrefabsContainNoPresentationComponentsOrAssets、ClientViewsContainPresentationHostButNoGameplayRoot、ClientViewRootsAreAtWorldOrigin。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/AatroxPrefabPlayModeTests.cs`：RuntimePrefab_InstantiatesWithModelAndEditorGizmo、ClientUnitOutline_CoversEveryAatroxSubMesh、TetherArea_InstantiatesAsStationaryProjectile、AnimatorController_RoutesPassiveUltimateAndEmpoweredAttack、AnimatorController_LocomotionVariantsAdvanceWithoutSelfReentry、UnitAnimationDriver_UsesNewLocomotionStateOnChangeFrame、AnimatorController_UltimateEndPlaysExitClip。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/CameraControllerPlayModeTests.cs`：SharedConfig_UsesOppositeBlueAndRedViewDirections、SharedConfig_AppliesAnimationRateIndependentOfGameplayTick、LockedGameplayTarget_SmoothlyFollowsProjectedPose、LockedGameplayTarget_FacingChurnDoesNotStallFollowPosition。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

普通碰墙、滑墙和缩短位移由移动系统处理。  
本模块只处理单位异常进入静态阻挡后的兜底挤出。

---

### 触发、跳过与 Tick 读取

触发来源：

```text
Dash 结束
ForcedMove 结束
Teleport 后
移动系统标记疑似进入阻挡
上次挤出失败
```

是否处于 Dash、强制位移，以及下一次允许探测的 Tick，均从单位侧移动状态查询。

```pseudo
function ShouldProbeWall(unit):
    currentTick =
        SimulationTickContext.Current.Tick

    if unit.Locomotion.IsDashing:
        return false

    if unit.Locomotion.IsForcedMoving:
        return false

    if not unit.Locomotion.WallProbeSuspect:
        return false

    if currentTick < unit.Locomotion.NextWallProbeTick:
        return false

    return true
```

`SimulationTickContext` 不作为 `Resolve` 的传参，也不由物理系统复制成自己的 `CurrentTick`。

---

### 单位侧修正入口

`WallPenetrationResolver` 负责：

```text
确认单位确实进入阻挡
计算稳定、受上限约束的挤出方向和距离
把 correction 交给单位侧移动入口
```

它不直接散写：

```text
entity.Transform2D.Position
entity.Transform2D.PrevPosition
entity.Bounds
entity.transform
```

伪代码：

```pseudo
function ResolveWallPenetration():
    for entity in UnitEntities:
        unit = entity.QueryInfo.Owner as Unit

        if unit is null:
            continue

        if not ShouldProbeWall(unit):
            continue

        if IsCircleOutsideBlockedCells(entity):
            unit.Locomotion.WallProbeSuspect = false
            continue

        correction = ComputeDepenetration(entity)

        if not correction.Success:
            ScheduleNextProbe(unit)
            continue

        unit.MovementHandler.ApplyMovementCorrection(
            correction.Delta,
            reason = WallDepenetration
        )
```

这里的 `ApplyMovementCorrection` 代表单位侧公开修正接缝，具体方法名由单位框架和移动系统最终实现决定。  
最终空间写入必须调用 `PhysicsEntity2D.ApplyLogicPositionDelta()` 或等价统一接口。

物理系统不能在单位侧入口返回后再次叠加同一修正量。

### 全局参数

Inspector / Authoring：

```text
float WallSkin
float WallPenetrationEpsilon
float MaxWallDepenetration
float WallProbeIntervalSeconds
```

Gameplay 运行时：

```text
fp WallSkinFP
fp WallPenetrationEpsilonFP
fp MaxWallDepenetrationFP
int WallProbeIntervalTicks
```

初始化时一次性转换：

```pseudo
WallProbeIntervalTicks =
    SecondsToLogicTicks(WallProbeIntervalSeconds)
```

运行时只比较整数 Tick。  
这些属于 `PhysicsWorld.Settings`，不属于单个单位。

### 定位

`PhysicsEntity2D` 是确定性空间状态组件，也是挂在 GameObject 上的 `MonoBehaviour`。

它同时承担两件彼此单向依赖的工作：

```text
Gameplay 阶段：
    正式空间接口修改 Transform2D / Shape / Bounds
    不写 Unity Transform

Unity 表现阶段：
    PhysicsEntity2D.LateUpdate
    读取最终 Transform2D
    单向写自身 GameObject 根 Transform
    作为所有参与帧同步 GO 根 Transform 的唯一最终写入入口
```

第一版不新增：

```text
PhysicsTransformSyncSystem
PhysicsEntityPresentationSync
EntitiesToSync
Bind / Unbind Sync Entity
```

---

### `PresentationDirty`

`PhysicsEntity2D` 内部保留非 Gameplay 权威的表现脏标记：

```text
bool PresentationDirty
bool LogicStateInitialized
```

以下操作设置 `PresentationDirty = true`：

```text
SetLogicPosition
SetLogicPose
ApplyLogicPositionDelta
TeleportLogicPosition
SetLogicForward
恢复 PhysicsEntity2D 空间状态
对象池实例重新初始化
```

`PresentationDirty`：

```text
不参与范围查询
不参与命中
不进入 GameplaySnapshot
不影响确定性结果
```

它只用于避免没有姿态变化时重复写 Unity Transform。

---

### `LateUpdate` 唯一同步入口

第一版不做表现插值，直接同步准确逻辑姿态。所有参与帧同步的 GameObject，其实体根节点 `Transform` 只能在这里写入：

```pseudo
function LateUpdate():
    if not LogicStateInitialized:
        return

    if not PresentationDirty:
        return

    if SyncPosition:
        logicWorld3 = GridMap.ToWorld3D(
            Transform2D.Position
        )

        logicWorld3.y += HeightOffset

        transform.position =
            ToUnityVector3(logicWorld3)

    if SyncRotation:
        forward3 = Vector3(
            Transform2D.Forward.x,
            0,
            Transform2D.Forward.y
        )

        if forward3.sqrMagnitude > epsilon:
            transform.rotation =
                Quaternion.LookRotation(
                    forward3,
                    Vector3.up
                )

    PresentationDirty = false
```

根节点与表现子节点的写入边界：

```text
PhysicsEntity2D 所在实体根节点：
    只由 PhysicsEntity2D.LateUpdate 写 position / rotation。

RenderRoot、模型、骨骼、VFX Root 等表现子节点：
    可由动画和表现系统修改局部姿态，
    但不得把结果反向写回 Transform2D。
```

如果未来增加渲染插值，也只能在 `LateUpdate` 内计算 Unity 表现姿态，不得修改 `Transform2D / Shape / Bounds`。

---

### 回滚与多 Tick 重演

一个 Unity 渲染帧内可能执行：

```text
多个 Gameplay Tick
恢复
重演多个 Tick
Hard Resync 状态应用
```

这些过程只更新逻辑空间状态并持续标记 Dirty。

渲染帧结束时：

```text
PhysicsEntity2D.LateUpdate
    -> 只读取最终恢复或重演结果
    -> 只同步一次 Unity Transform
```

因此不会让 GameObject 在同一渲染帧中依次跳过历史位置。

---

### 编辑器初始化

编辑器模式允许从 Unity Transform 生成 Authoring 预览：

```text
PhysicsEntity2D.InitPreviewFromUnityTransform()
```

用途：

```text
Scene 摆放单位预览
Prefab 默认朝向预览
尚未初始化逻辑状态时的 Gizmo 预览
```

Gameplay Tick 中禁止反向读取：

```text
transform.position -> Transform2D.Position
transform.rotation -> Transform2D.Forward
```

运行时传送或外部设置位置必须调用 `PhysicsEntity2D` 的正式逻辑空间接口。

---

### Inspector 同步设置

Inspector 可配置：

```text
bool SyncPosition
bool SyncRotation
float HeightOffset
```

这些字段只影响 Unity 表现，不进入 Gameplay 检测。

服务端、Headless 或没有表现对象的运行环境可以禁用同步；逻辑空间查询结果不受影响。

---

### 边界总结

允许：

```text
外部系统调用正式空间接口写 Transform2D / Shape
PhysicsEntity2D 更新 Bounds
PhysicsEntity2D.LateUpdate 写自身 Unity Transform
编辑器预览读取 Unity Transform
对象池控制 GameObject 激活状态
```

禁止：

```text
Gameplay 空间接口直接写 Unity Transform
Gameplay Tick 读取 Unity Transform 作为逻辑输入
Unity Physics 结果反向覆盖 PhysicsEntity2D
外部逐字段写 Position 后忘记更新 PrevPosition / Bounds
使用 transform.position 参与范围查询或命中判定
新增第二套 PhysicsEntity2D 或 LogicTransform
```

### 定位

`PhysicsEntity2D` 自身提供 Scene 可视化能力。  
它可以直接实现：

```text
OnDrawGizmos()
OnDrawGizmosSelected()
```

第一版不为 Transform 同步或 Gizmo 新增必需组件。

运行时 Gizmo 默认绘制**逻辑姿态**，而不是当前 Unity Transform。这样即使表现层正在插值或尚未执行本帧 `LateUpdate`，仍能看到真实 Gameplay 空间状态。

---

### 绘制姿态来源

```pseudo
function GetGizmoPose():
    if Application.isPlaying
       and LogicStateInitialized:
        return Transform2D

    return BuildAuthoringPreviewFromUnityTransform()
```

可选调试开关：

```text
DrawLogicPose
DrawPresentationPose
```

其中：

```text
DrawLogicPose
    使用 Transform2D.Position / Forward

DrawPresentationPose
    使用当前 Unity transform
    仅用于比较表现同步结果
```

查询、命中和 Bounds 永远使用逻辑姿态。

---

### 显示内容

| 内容 | 来源 |
|---|---|
| 逻辑当前位置 | `PhysicsEntity2D.Transform2D.Position` |
| 上一逻辑位置 | `PhysicsEntity2D.Transform2D.PrevPosition` |
| 逻辑朝向 | `PhysicsEntity2D.Transform2D.Forward` |
| 表现位置，可选 | Unity `transform.position` |
| Point | `PhysicsShape2D.Kind == Point` |
| Circle | `PhysicsShape2D.Kind == Circle` |
| Segment | `PhysicsShape2D.Kind == Segment` |
| Rect | `PhysicsShape2D.Kind == Rect` |
| Sweep | `PhysicsShape2D.SweepFromPrev` |
| AABB | `PhysicsEntity2D.Bounds` |
| CellSpan | `PhysicsEntity2D.Bounds.CellSpan` |

---

### Inspector 参数

`PhysicsEntity2D` 上的 Inspector 参数分三类。

#### Authoring 参数

这些参数用于编辑器配置和运行时初始化，使用 `float`：

```text
PhysicsEntity2D Authoring
    PhysicsShapeKind InitialShapeKind
    float Radius
    float Length
    float Width
    Vector2 LocalOffset
    Vector2 HalfExtents
    bool SweepFromPrev
```

进入逻辑运行时后转换为：

```text
fp Radius
fp Length
fp Width
fp2 LocalOffset
fp2 HalfExtents
```

#### 表现同步参数

```text
bool SyncPosition
bool SyncRotation
float HeightOffset
```

只影响 `LateUpdate` 写入 Unity Transform。

#### Gizmo 参数

这些参数只用于显示，可以使用 `float`：

```text
PhysicsEntity2D Gizmo
    bool DrawLogicPose
    bool DrawPresentationPose
    bool DrawShape
    bool DrawPoint
    bool DrawCircle
    bool DrawSegment
    bool DrawRect
    bool DrawSweep
    bool DrawBounds
    bool DrawCellSpan
    float PointSize
    float LineWidth
```

---

### Point 绘制

`Point` 是正式形状，不是半径很小的圆。

Scene 中建议这样画：

```text
Point:
    绘制小十字或小圆点

Point + SweepFromPrev:
    绘制 PrevPosition -> Position 的线段
    同时绘制当前位置点
```

命中查询时：

```text
静止 Point:
    Point vs Unit Circle

移动 Point:
    Segment(PrevPosition, Position) vs Unit Circle
```

---

### Rect / Segment 绘制

`Rect` 使用实体的逻辑 `Forward / Right` 绘制旋转矩形。

```pseudo
center = Transform2D.Position + Forward * LocalOffset.y + Right * LocalOffset.x
halfForward = Forward * Shape.HalfExtents.y
halfRight = Right * Shape.HalfExtents.x

p0 = center - halfForward - halfRight
p1 = center - halfForward + halfRight
p2 = center + halfForward + halfRight
p3 = center + halfForward - halfRight
```

`Segment` 使用逻辑 `Forward` 和长度绘制：

```pseudo
start = center
end = center + Forward * Shape.Length
```

如果 `Width > 0`，可以绘制一条带宽线段的近似矩形。

---

### 定位

`WallPenetrationResolver` 只处理单位已经进入静态墙体的异常情况。  
普通碰壁由 `MovementHandler.ResolveStaticWall()` 在提交前阻止。

它可以属于 `PhysicsWorld`，但只能输出：

```text
MovementCorrectionRequest
```

不能直接调用 `PhysicsEntity2D` 的空间写入 API。

---

### 触发时机

```text
Dash 结束后
强制位移结束后
传送后
外部位置修正后
检测到单位圆已与阻挡格重叠
```

---

### 修正请求

```text
MovementCorrectionRequest
    UnitUid UnitUid
    fp2 Delta
    MovementCorrectionReason Reason
```

```pseudo
function DetectWallPenetration(entity):
    penetration = CalculatePenetration(
        position = entity.Position,
        radius = entity.Shape.Radius,
        map = PathGridMap2D
    )

    if not penetration.IsInsideWall:
        return None

    correction = ClampLength(
        penetration.PushOut,
        Settings.MaxWallDepenetration
    )

    return MovementCorrectionRequest(
        UnitUid = ResolveUnitUid(entity),
        Delta = correction,
        Reason = WallDepenetration
    )
```

`ResolveUnitUid()` 使用物理系统正式的查询信息，不在本文重复定义 Owner 绑定和 UID 查询层。

应用：

```pseudo
function ApplyCorrectionRequest(request):
    unit = UnitWorld.Find(request.UnitUid)
    unit.MovementHandler.ApplyMovementCorrection(
        request.Delta,
        request.Reason
    )
```

最终由 `MovementHandler` 调用：

```text
PhysicsEntity2D.ApplyLogicPositionDelta(...)
```

### 与寻路状态协作

`WallPenetrationResolver` 只生成 `MovementCorrectionRequest`。  
`MovementHandler.ApplyMovementCorrection()` 提交修正后，不需要把路径交给移动系统，也不需要单独恢复路线。

下一次 `UnitLocomotionAgent.Evaluate()` 会读取修正后的 `PhysicsEntity2D.Position`，在正常路径跟随流程中：

```text
推进 PathCursor
检测是否偏离剩余路径走廊
必要时重新寻路
判断任务是否到达
```

因此墙体修正与 RVO 绕行使用同一套路线有效性检测，不增加独立路线恢复状态。

---

### 帧同步定位

墙体穿透几何计算和修正请求应在同一 Tick 内完成，因此：

```text
候选阻挡格       【单 Tick 临时】
穿透结果         【单 Tick 临时】
修正请求         【单 Tick 临时】
```

如果物理系统保留跨 Tick 的 `WallProbeState / ProbeCountdown` 优化，则这些状态必须进入 `PhysicsWorldSnapshot`；本设计第一版不要求该优化。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

