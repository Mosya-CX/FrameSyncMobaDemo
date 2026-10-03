# 定点形状与范围查询

## 目标实现

点、圆、线段、矩形与扫掠查询得到稳定结果。

## 技术方案

PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。

## 边界情况

不是 Unity 物理权威；边缘接触、零半径、退化线段、旋转矩形和候选去重都有明确结果。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/ProjectileHitResolver.cs`：当前关联实现定义 ProjectileHitResult、ProjectileHitResolver（以源码为实际命名）。
- `Assets/Scripts/Gameplay/TargetFilter/RangeQueryService.cs`：当前关联实现定义 RangeQueryService、QueryCandidate（以源码为实际命名）。
- `Assets/Scripts/Physics/Geometry/PhysicsGeometry2D.cs`：当前关联实现定义 PhysicsGeometry2D（以源码为实际命名）。
- `Assets/Scripts/Physics/Geometry/PhysicsShape2D.cs`：当前关联实现定义 PhysicsShape2D（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/RangeQueryServiceTests.cs`：Query_EmptyGrid_ReturnsEmpty、Query_SingleUnitInRange_ReturnsIt、Query_UnitOutOfRange_ReturnsEmpty、Query_EnemyOnly_FiltersCorrectly、Query_AllyOnly_FiltersCorrectly、Query_SelfOnly_ReturnsOnlySelf、Query_LifeStateFilter_DeadUnitExcluded。
- `Assets/Scripts/Physics/Tests/PhysicsGeometry2DTests.cs`：Facing_UsesFixedPointNormalizationAndClockwisePerpendicular、BelowThresholdFacing_IsRejectedWithoutDivision、PointBounds_UseOffsetAdjustedCurrentPoint、SweptPointBounds_UseFormalPrevPositionToCurrentWorldPoint、CircleBounds_ExpandOffsetAdjustedCenterByRadius、SweptCircleBounds_UnionExpandedSweepAndCurrentCircle、SegmentWorld_UsesOffsetCenterForwardLengthAndWidth。
- `Assets/Scripts/Physics/Tests/PhysicsShape2DTests.cs`：PointFactory_PreservesFormalFieldsAndZerosUnusedFields、CircleFactory_PreservesFormalFieldsAndZerosUnusedFields、NegativeCircleRadius_IsRejected、SegmentFactory_PreservesFormalFieldsAndZerosUnusedFields、RectFactory_PreservesFormalFieldsAndZerosUnusedFields、NegativeSegmentDimensions_AreRejected、NegativeRectHalfExtents_AreRejected。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/ProjectileFalloffTests.cs`：PiercingFalloff_ReducesDamagePerExtraHit、PiercingFalloff_OverridePath_MatchesStaticConfig、PiercingFalloff_ClampsAtMinDamageRatio。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 支持形状

本版必须支持：

```text
Point
Circle
Segment
Rect
```

```text
PhysicsShapeKind
    Point
    Circle
    Segment
    Rect
```

形状用途：

| 形状 | 用途 |
|---|---|
| `Point` | 远程普攻、小型飞弹、点状检测源。 |
| `Circle` | 单位占位、圆形飞弹、圆形区域。 |
| `Segment` | 直线射线、激光、细长命中带。 |
| `Rect` | 框形区域、矩形技能区域。 |

---

### `PhysicsShape2D` 数据

```text
PhysicsShape2D
    PhysicsShapeKind Kind

    fp2 LocalOffset

    fp Radius
    fp Length
    fp Width
    fp2 HalfExtents

    bool SweepFromPrev
```

不同形状使用字段：

| Shape | 使用字段 |
|---|---|
| `Point` | `LocalOffset`, `SweepFromPrev` |
| `Circle` | `LocalOffset`, `Radius`, `SweepFromPrev` |
| `Segment` | `LocalOffset`, `Length`, `Width`, `SweepFromPrev` |
| `Rect` | `LocalOffset`, `HalfExtents`, `SweepFromPrev` 可选 |

`LocalOffset` 表示形状中心相对实体位置的偏移，偏移按实体 `Forward / Right` 解释。

---

### Point 形状语义

`Point` 表示：

```text
实体自身没有命中半径。
命中成立条件由点与目标形状决定。
```

常见命中：

| 情况 | 检测方式 |
|---|---|
| 静止 Point vs 单位圆 | 点是否落在单位圆内。 |
| 移动 Point vs 单位圆 | `PrevPosition -> Position` 线段是否穿过单位圆。 |
| Point 查询范围 | 查询点所在格，候选单位由单位圆插入网格保证覆盖。 |
| Point Sweep 查询范围 | 查询 Sweep 线段 AABB 覆盖格。 |

点状飞弹不要再用“极小 Circle”硬凑。  
例如远程普攻弹体推荐：

```text
Shape.Kind = Point
Shape.SweepFromPrev = true
```

---

### 形状世界参数计算

所有形状都从 `PhysicsEntity2D.Transform2D` 推导世界参数。

#### Point

```pseudo
function GetPointWorld(entity):
    return entity.Transform2D.Position
         + entity.Transform2D.Right   * entity.Shape.LocalOffset.x
         + entity.Transform2D.Forward * entity.Shape.LocalOffset.y
```

#### Circle

```pseudo
function GetCircleWorld(entity):
    center = GetPointWorld(entity)
    radius = entity.Shape.Radius
    return center, radius
```

#### Segment

```pseudo
function GetSegmentWorld(entity):
    center = GetPointWorld(entity)
    half = entity.Shape.Length / 2

    a = center - entity.Transform2D.Forward * half
    b = center + entity.Transform2D.Forward * half

    width = entity.Shape.Width
    return a, b, width
```

#### Rect

```pseudo
function GetRectWorld(entity):
    center = GetPointWorld(entity)
    right = entity.Transform2D.Right
    forward = entity.Transform2D.Forward
    halfExtents = entity.Shape.HalfExtents

    return center, right, forward, halfExtents
```

---

### AABB 更新

`PhysicsEntity2D` 每次位置或形状改变后，需要更新 `Bounds`。

```pseudo
function UpdateBounds(entity):
    switch entity.Shape.Kind:
        case Point:
            p = GetPointWorld(entity)
            if entity.Shape.SweepFromPrev:
                prev = entity.Transform2D.PrevPosition
                Bounds = AabbFromSegment(prev, p)
            else:
                Bounds = AabbFromPoint(p)

        case Circle:
            center, radius = GetCircleWorld(entity)
            Bounds = AabbFromCircle(center, radius)

            if entity.Shape.SweepFromPrev:
                prevCenter = entity.Transform2D.PrevPosition
                sweepBounds = AabbFromSegment(prevCenter, center).Expand(radius)
                Bounds = Union(Bounds, sweepBounds)

        case Segment:
            a, b, width = GetSegmentWorld(entity)
            Bounds = AabbFromSegment(a, b).Expand(width / 2)

        case Rect:
            Bounds = AabbFromOrientedRect(entity)
```

---

### 定位

服务只负责：

```text
1. 读取已绑定 SourceEntity 的 Shape / Bounds
2. 查询 UnitFinalGrid
3. 去重
4. 根据 ProjectileTargetFilter 过滤 Unit
5. 做精确形状测试
6. 稳定排序
7. 返回候选
```

它不 Tick 投掷物，不维护命中记忆，不执行效果。

---

### 通用单位目标过滤

投掷物命中与普通范围查询复用：

```text
UnitTargetFilter
    TeamQueryRule TeamRule

    UnitKindMask UnitKindMask

    bool RequireSubKind
    ushort UnitSubKindId

    bool RequirePrototype
    int UnitPrototypeId

    UnitLifeStateMask LifeStateMask
    bool RequireTargetable
```

单位框架 v23 不使用运行时 `UnitTags`，也没有采用 `UnitQueryTraitMask`。  
主要分类改为：

```text
UnitKind
ushort UnitSubKindId
```

具体单位原型使用：

```text
UnitPrototypeId
```

所有字段从 `Unit` 读取。

---

### 输入输出

```text
ProjectileHitQueryInput
    PhysicsEntity2D SourceEntity
    UnitTargetFilter TargetFilter
    TempBuffer Candidates

ProjectileHitCandidate
    PhysicsEntity2D TargetEntity
    Unit TargetUnit
    fp HitDistance
    fp2 HitPosition
```

---

### 查询流程

```pseudo
function QueryProjectileHits(source, filter, temp):
    temp.Clear()

    if source.QueryInfo.Kind != Projectile:
        return temp.Empty

    raw = UnitFinalGrid.QueryAabb(source.Bounds)
    unique = DeduplicateByUid(raw)

    for target in unique:
        unit = target.QueryInfo.Owner as Unit

        if unit is null:
            continue

        if not PassUnitTargetFilter(
            requesterUid = source.QueryInfo.UidSnapshot,
            requesterTeam = source.QueryInfo.TeamSnapshot,
            unit = unit,
            filter = filter
        ):
            continue

        if not ShapeOverlap(
            source,
            target,
            out hitPoint,
            out hitDistance
        ):
            continue

        temp.Add(
            ProjectileHitCandidate(
                target,
                unit,
                hitDistance,
                hitPoint
            )
        )

    SortProjectileHits(temp, source)
    return temp
```

---

### 形状精确测试

#### Point vs Unit Circle

```pseudo
function PointVsUnitCircle(point, unitCircle):
    d = point - unitCircle.center
    return Dot(d, d) <= unitCircle.radius * unitCircle.radius
```

#### Swept Point vs Unit Circle

```pseudo
function SweptPointVsUnitCircle(prev, curr, unitCircle):
    closest = ClosestPointOnSegment(
        unitCircle.center,
        prev,
        curr
    )

    d = unitCircle.center - closest
    return Dot(d, d) <= unitCircle.radius * unitCircle.radius
```

#### Circle vs Unit Circle

```pseudo
function CircleVsUnitCircle(circle, unitCircle):
    r = circle.radius + unitCircle.radius
    d = circle.center - unitCircle.center
    return Dot(d, d) <= r * r
```

#### Segment vs Unit Circle

```pseudo
function SegmentVsUnitCircle(a, b, width, unitCircle):
    closest = ClosestPointOnSegment(unitCircle.center, a, b)
    r = unitCircle.radius + width / 2
    d = unitCircle.center - closest
    return Dot(d, d) <= r * r
```

#### Rect vs Unit Circle

```pseudo
function RectVsUnitCircle(rect, unitCircle):
    local = WorldToRectLocal(unitCircle.center, rect)
    clamped = Clamp(
        local,
        -rect.halfExtents,
        rect.halfExtents
    )

    d = local - clamped
    return Dot(d, d) <= unitCircle.radius * unitCircle.radius
```

---

### 稳定排序

ProjectileHitQueryService 使用 UnitFinalGrid、扫掠形状和正式 TargetFilter；ProjectileHitMemory 保存跨 Tick 命中事实，命中结果进入 Combat 或所属效果端口。

距离为主键；等距按动作/参与者纯哈希；记忆不能依赖候选枚举顺序；结构技能效果由中央准入兜底拒绝。

### 定位

`RangeQueryService` 面向技能、AI、战斗和 Buff 等模块查询单位。  
默认读取移动与墙体修正后的 `UnitFinalGrid`。

---

### 查询描述

```text
RangeQueryDesc
    PhysicsShape2D Shape
    PhysicsTransform2D Transform

    UnitTargetFilter TargetFilter

    RangeQuerySortMode SortMode
    int MaxResult
```

复用的 `UnitTargetFilter`：

```text
TeamQueryRule TeamRule
UnitKindMask UnitKindMask

bool RequireSubKind
ushort UnitSubKindId

bool RequirePrototype
int UnitPrototypeId

UnitLifeStateMask LifeStateMask
bool RequireTargetable
```

没有：

```text
IncludeTags
ExcludeTags
UnitQueryTraitMask
```

---

### TeamQueryRule

```text
TeamQueryRule
    Any
    EnemyOnly
    AllyOnly
    AllyOrSelf
    SelfOnly
```

| 规则 | 说明 |
|---|---|
| `Any` | 不限制阵营。 |
| `EnemyOnly` | 仅敌方有效。 |
| `AllyOnly` | 仅友方有效，不包含自己。 |
| `AllyOrSelf` | 友方和自己有效。 |
| `SelfOnly` | 仅自己有效。 |

---

### 单位分类过滤

```pseudo
function PassUnitClassification(unit, filter):
    if not filter.UnitKindMask.Contains(unit.UnitKind):
        return false

    if filter.RequireSubKind:
        if unit.UnitSubKindId != filter.UnitSubKindId:
            return false

    if filter.RequirePrototype:
        if unit.UnitPrototypeId != filter.UnitPrototypeId:
            return false

    return true
```

语义：

| 字段 | 用途 |
|---|---|
| `UnitKind` | Hero / Minion / Monster / Structure 等宽泛大类。 |
| `UnitSubKindId` | EpicMonster / Tower / CloneHero 等主要子分类。 |
| `UnitPrototypeId` | 指定某个具体 Gameplay 原型。 |

---

### 完整过滤

```pseudo
function PassUnitTargetFilter(
    requesterUid,
    requesterTeam,
    unit,
    filter
):
    if not PassTeamRule(
        requesterUid,
        requesterTeam,
        unit.UnitUid,
        unit.TeamId,
        filter.TeamRule
    ):
        return false

    if not filter.LifeStateMask.Contains(unit.LifeState):
        return false

    if filter.RequireTargetable:
        if not unit.Capability.IsTargetable:
            return false

    if not PassUnitClassification(unit, filter):
        return false

    return true
```

---

### 查询顺序

必须先完成全部候选处理，再截断：

```text
网格候选
    -> 按 UidSnapshot 去重
    -> Team / LifeState / Targetable / 分类过滤
    -> 精确形状测试
    -> 计算排序键
    -> 稳定排序
    -> 截取 MaxResult
```

错误顺序：

```text
遍历候选时达到 MaxResult 就 break
    -> 再排序
```

这会让结果受网格桶遍历顺序影响。

---

### 查询伪代码

```pseudo
function QueryUnits(
    desc,
    requesterUid,
    requesterTeam,
    result,
    scratch
):
    result.Clear()
    scratch.Clear()

    queryAabb = BuildAabb(desc.Transform, desc.Shape)
    raw = UnitFinalGrid.QueryAabb(queryAabb)
    unique = DeduplicateByUid(raw)

    for entity in unique:
        unit = entity.QueryInfo.Owner as Unit

        if unit is null:
            continue

        if not PassUnitTargetFilter(
            requesterUid,
            requesterTeam,
            unit,
            desc.TargetFilter
        ):
            continue

        if not ShapeOverlap(
            desc.Transform,
            desc.Shape,
            entity
        ):
            continue

        sortKey = BuildSortKey(
            desc.SortMode,
            desc.Transform.Position,
            entity.Transform2D.Position,
            entity.QueryInfo.UidSnapshot
        )

        scratch.Add(unit, sortKey)

    StableSort(scratch)

    count = Min(desc.MaxResult, scratch.Count)

    for i from 0 to count - 1:
        result.Add(scratch[i].Unit)
```

---

### 排序模式

```text
RangeQuerySortMode
    Uid
    Distance
    DistanceThenUid
```

建议默认：

```text
DistanceThenUid
```

`Uid` 始终作为最终稳定 Tie Break。


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

