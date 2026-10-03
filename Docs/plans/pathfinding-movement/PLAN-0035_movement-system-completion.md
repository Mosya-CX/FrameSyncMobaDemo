# 移动系统补全

## 本次执行范围

本计划对应原编码 0035 的一次执行：移动系统补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [普通移动冲刺与强制位移](../../requirements/pathfinding-movement/REQ-FEAT-067_movement-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [确定性局部避让](../../requirements/pathfinding-movement/REQ-FEAT-066_local-avoidance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Movement/MovementSnapshot.cs`：`MovementSnapshot`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Movement/MovementCollisionResolver.cs`：`IMovementCollisionResolver`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Movement/MoveIntent.cs`：`MoveIntent`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：

```csharp
        public void TickUpdate()
        {
            MovementMode mode = ResolveMovementMode();
            switch (mode)
            {
                case MovementMode.ForcedMove:
                    AdvanceForcedMove();
                    break;
                case MovementMode.Dash:
                    AdvanceDash();
                    break;
                case MovementMode.RouteMove:
                    AdvanceRouteMove();
                    break;
                default:
                    ApplyStationaryPose();
                    break;
            }

            _currentIntent = MoveIntent.None;
            ClearTickInputs();
        }
```

`Assets/Scripts/Gameplay/Movement/MovementSnapshot.cs`：

```csharp
namespace FrameSyncMoba.Unit
{
    public struct MovementSnapshot
    {
        public DashRuntime Dash;
        public ForcedMoveRuntime ForcedMove;

        public bool IsDashing => Dash.IsActive;

        public static readonly MovementSnapshot Default = new MovementSnapshot
        {
            Dash = default,
            ForcedMove = default,
        };
    }
}
```

### 输入输出与边界

**普通移动冲刺与强制位移**

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

死亡清理移动任务；仲裁决定占用而不让技能控制直接写 Transform；墙体约束与异常挤出分开。

**路线选择与跟随状态**

UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。

控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。

**确定性局部避让**

DeterministicRVOSystem 读取移动前 RvoGrid，以固定候选速度和稳定约束求解；全部结果计算后再应用。

技术遍历不形成先移动优势；零速度、重叠、狭窄通道和速度上限有可判定边界。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/MovementHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Constructor_SetsInitialPosition`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Constructor_SetsInitialPosition()
        {
            var h = UnitTestFactory.CreateMovementHandler(new fp2(10m, 20m), 3m);

            Assert.AreEqual(new fp2(10m, 20m), h.Position);
            Assert.AreEqual((fp)3m, h.MoveSpeed);
            Assert.AreEqual(fp2.zero, h.Velocity);
        }
```
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldRegistrationTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `RegisterUnit_AddsToUnitEntities`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RegisterUnit_AddsToUnitEntities()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            world.RegisterUnit(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
            Assert.That(world.UnitEntities[0], Is.SameAs(entity));
            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }
```
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldBuildFinalGridTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `BuildUnitFinalGrid_AllRegisteredUnits_Inserted`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void BuildUnitFinalGrid_AllRegisteredUnits_Inserted()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(100, 2, 0, (fp)15m, (fp)15m);

            world.RegisterUnit(e1);
            world.RegisterUnit(e2);

            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(2, results.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
