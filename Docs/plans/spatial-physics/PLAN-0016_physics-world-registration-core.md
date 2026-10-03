# 空间世界注册内核

## 本次执行范围

本计划对应原编码 0016 的一次执行：空间世界注册内核。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [空间实体注册与写入](../../requirements/spatial-physics/REQ-FEAT-058_spatial-registration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [定点形状与范围查询](../../requirements/spatial-physics/REQ-FEAT-059_spatial-geometry.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [移动前后网格与碰撞事实](../../requirements/spatial-physics/REQ-FEAT-060_collision-facts.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PhysicsEntity2D 拥有位置、朝向、形状和稳定查询信息；PhysicsWorld 注册/反注册，Movement 与投射物通过正式写入接口修改逻辑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Physics/Core/PhysicsWorld.cs`：

```csharp
        public void RegisterProjectile(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureNotRegistered(entity, unitEntities, "Projectile");
            EnsureNotRegistered(entity, projectileEntities, "Projectile");

            projectileEntities.Add(entity);
        }
```

`Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：

```csharp
        internal void ClearRuntime()
        {
            Transform2D = default;
            Shape = default;
            Bounds = default;
            QueryInfo = default;
            presentationInitialized = false;
            presentationSnapRequested = true;
        }
```

### 输入输出与边界

**空间实体注册与写入**

PhysicsEntity2D 拥有位置、朝向、形状和稳定查询信息；PhysicsWorld 注册/反注册，Movement 与投射物通过正式写入接口修改逻辑。

不新增 PhysicsEntityHandle；Physics 不执行 Combat；PrevPosition 每 Tick 冻结，传送和恢复有明确语义；Unity Transform 为派生表现。

**定点形状与范围查询**

PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。

不是 Unity 物理权威；边缘接触、零半径、退化线段、旋转矩形和候选去重都有明确结果。

**移动前后网格与碰撞事实**

RvoGrid 使用移动前位置，UnitFinalGrid 在全部移动提交后构建；UnitCollisionEventBuffer 使用稳定 PairKey 发布轻量接触事实。

网格不提前按业务存活状态删候选；碰撞缓存跨 Tick 部分按快照合同；恢复后 Rebuild 派生网格。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
