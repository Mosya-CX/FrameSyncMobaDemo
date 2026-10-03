# 旋转网格地图与半径通行

## 目标实现

不同半径单位可在二维旋转网格正确判断静态通行。

## 技术方案

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

## 边界情况

旋转、边界、不可走起终点和过大半径可见处理；NavMask 与内容配置版本有一致来源。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Pathfinding/DeterministicMapConfig.cs`：当前关联实现定义 DeterministicMapObstacleAuthoring、DeterministicSpawnPointAuthoring、BakedMapObstacle、BakedSpawnPoint、BakedDeterministicMapData（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/DynamicNavigationFrame.cs`：当前关联实现定义 DynamicNavigationFrame、DynamicNavigationQuery（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Pathfinding/PathGridMap2D.cs`：当前关联实现定义 PathGridMap2D、walkability、or、and（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/DeterministicMapConfigTests.cs`：FullMatchMap_BakesStableWalkabilityAndSpawnPoints。
- `Assets/Scripts/Bootstrap/Tests/EditMode/MurkWolfFormalContentTests.cs`：UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues、GreaterWolf_OnHitBuff_IsPermanentThreePercentCurrentHealth、MapPrefab_AuthorsTwoVisualizedThreeWolfCamps、MapPrefab_AllWolfSpawnSlotsAreWalkableForTheirRadius、MapCampUpsert_PreservesVisualAuthoringAndOtherCamps。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs`：TickContext_InitializesWithCorrectTick、TickContext_AdvancesCorrectly、DeterministicRandom_ProducesSameSequenceForSameSeed、UnitUid_ComparisonAndSorting、PathGrid_Initialise_ProducesValidGrid、AStar_FindPath_ReturnsValidPath、FlowField_BuildAndQuery_ReturnsValidDirection。
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

`PathGridMap2D` 是寻路、移动、`RvoGrid`、墙体约束共同使用的静态地图。

它负责：

```text
地图坐标转换
格子索引
静态阻挡
半径通行层
世界 2D 与格子转换
外部 3D 转换
```

---

### 地图中心 Transform 规则

编辑器中可以直接设置一个 Transform 作为地图中心点：

```text
GridCenterTransform
    Position:
        地图中心点。
        XZ 转换为 Center2D。
        Y 作为 CenterY。

    Rotation:
        决定地图二维轴向。
        地图随该 Transform 旋转。

    Scale:
        忽略。
```

运行时不读取 Transform。  
烘焙或初始化时保存：

```text
Center2D
CenterY
AxisRight2D
AxisForward2D
```

---

### 核心数据

```text
PathGridMap2D
    int Width
    int Height
    fp CellSize

    fp2 Center2D
    fp CenterY
    fp2 AxisRight2D
    fp2 AxisForward2D

    bool[] BaseWalkable
    int[] Clearance
    bool[][] WalkableByRadiusClass
```

`BaseWalkable` 表示格子本身是否为静态可行走。  
`Clearance` 表示到最近阻挡的离散余量。  
`WalkableByRadiusClass` 表示不同半径等级单位能否站在该格中心。

---

### 坐标系统

地图局部坐标以地图中心为原点：

```text
localX:
    沿 AxisRight2D

localY:
    沿 AxisForward2D
```

格子索引从左下角开始：

```text
x in [0, Width - 1]
y in [0, Height - 1]
index = y * Width + x
```

---

### 世界转局部伪代码

```pseudo
function WorldToMapLocal2D(world):
    delta = world - Center2D

    localX = Dot(delta, AxisRight2D)
    localY = Dot(delta, AxisForward2D)

    return fp2(localX, localY)
```

---

### 局部转格子伪代码

```pseudo
function LocalToCell(local):
    halfW = Width * CellSize / 2
    halfH = Height * CellSize / 2

    x = FloorToInt((local.x + halfW) / CellSize)
    y = FloorToInt((local.y + halfH) / CellSize)

    return Cell2D(x, y)
```

---

### 格子中心转世界伪代码

```pseudo
function CellToWorldCenter(cell):
    halfW = Width * CellSize / 2
    halfH = Height * CellSize / 2

    localX = -halfW + (cell.x + 0.5) * CellSize
    localY = -halfH + (cell.y + 0.5) * CellSize

    return Center2D
         + AxisRight2D * localX
         + AxisForward2D * localY
```

外部需要 3D 坐标时：

```pseudo
function ToWorld3D(pos2D):
    return fp3(pos2D.x, CenterY, pos2D.y)
```

这里的 3D Y 轴统一使用地图中心点 Y。

---

### 半径可走性

A*、流场、`MovementHandler` 的移动提交、`WallPenetrationResolver` 必须使用同一套半径语义。

```text
PathAgentShapeView
    fp Radius
    RadiusClass RadiusClass
```

第一版建议半径等级：

```text
Small
Medium
Large
```

地图烘焙时生成：

```text
WalkableByRadiusClass[Small]
WalkableByRadiusClass[Medium]
WalkableByRadiusClass[Large]
```

---

### `IsWalkableForAgent` 伪代码

```pseudo
function IsWalkableForAgent(cell, shapeView):
    if not IsValidCell(cell):
        return false

    index = GetIndex(cell.x, cell.y)

    if not WalkableByRadiusClass[shapeView.RadiusClass][index]:
        return false

    return true
```

---

### `IsCircleWalkable` 伪代码

`MovementHandler` 在提交普通移动前可以用更精确的圆形检测兜底。

```pseudo
function IsCircleWalkable(position, radius):
    span = CircleToCellSpan(position, radius)

    for cell in span:
        if not IsValidCell(cell):
            return false

        if BaseWalkable[cell.index]:
            continue

        rect = CellToLocalRect(cell)
        circleCenterLocal = WorldToMapLocal2D(position)

        if CircleIntersectsRect(circleCenterLocal, radius, rect):
            return false

    return true
```

---

### 帧同步定位

| 数据 | 标记 | 原因 |
|---|---|---|
| 地图尺寸、中心、轴向、格子大小 | `【静态配置】` | 来自 Bake 数据，对局中只读。 |
| `BaseWalkable / Clearance / WalkableByRadiusClass` | `【静态配置】` | 离线烘焙静态数据。 |
| 编辑器 `GridCenterTransform` | `【Authoring】` | 只在编辑器或初始化时读取，不进入 Gameplay Tick。 |
| 坐标转换临时结果 | `【单 Tick 临时】` | 每次调用即时计算。 |

`PathGridMap2D` 整体不进入 Gameplay 快照。恢复后继续引用同一份只读 Bake 数据。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

