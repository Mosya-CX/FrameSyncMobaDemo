# 亚托克斯英雄内容

## 本次执行范围

本计划对应原编码 0133 的一次执行：亚托克斯英雄内容。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/ClientUnitOutline.cs`：`ClientUnitOutline`。
- `Assets/Scripts/Bootstrap/CameraController.cs`：`CameraController`。
- `Assets/Scripts/FrameSync/BuffDrivenBoneVisibility.cs`：`BuffDrivenBoneVisibility`。
- `Assets/Scripts/Gameplay/Ability/AatroxAbilityZoneAuthoringGizmo.cs`：`AatroxZoneGizmoDisplayMode`、`AatroxZoneGizmoSelection`、`AatroxAbilityZoneAuthoringGizmo`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Presentation/UnitAnimationProfile.cs`：`UnitAnimationProfile`。
- `Assets/Scripts/Gameplay/Presentation/VfxEvent.cs`：`VfxEvent`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileContainmentZoneAuthoring.cs`：`ProjectileContainmentZoneAuthoring`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/ClientUnitOutline.cs`：

```csharp
        private void LateUpdate()
        {
            if (!highlighted ||
                outlineGo == null ||
                targetRenderer == null)
            {
                return;
            }
            BakeOnce();
        }
```

`Assets/Scripts/Bootstrap/CameraController.cs`：

```csharp
        private void LateUpdate()
        {
            if (!followLocked)
            {
                return;
            }

            FollowLocalHero();
            ClampToBounds();
        }
```

### 输入输出与边界

**按对局加载内容闭包**

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/AatroxPrefabPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `OutlineBakeRunsAfterWingVisibility`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void OutlineBakeRunsAfterWingVisibility()
        {
            int outlineOrder = typeof(ClientUnitOutline)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;
            int wingOrder = typeof(BuffDrivenBoneVisibility)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;

            Assert.That(outlineOrder, Is.GreaterThan(wingOrder));
        }
```
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CombinedAbilityCatalog_BakesAatroxAndVarus`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CombinedAbilityCatalog_BakesAatroxAndVarus()
        {
            AbilityRuntimeCatalogAsset catalog = Load<AbilityRuntimeCatalogAsset>(
                Root + "Abilities/FormalHeroAbilityRuntimeCatalog.asset");
            AbilityDefinitionRegistry registry = catalog.BakeOrThrow();

            for (int id = 10021; id <= 10024; id++)
                Assert.That(registry.TryGet(id, out _), Is.True, $"Ability {id}");
            Assert.That(registry.TryGetPassive(10020, out _), Is.True);
            Assert.That(registry.TryGet(10011, out _), Is.True, "Varus Q remains registered");
            Assert.That(registry.TryGetSlot(0, out AbilitySlotDef qSlot), Is.True);
            Assert.That(qSlot.AbilityIds, Does.Contain(10011));
            Assert.That(qSlot.AbilityIds, Does.Contain(10021));
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/UnitPrefabAnimatorTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `AttackSequence_MapsOntoAuthoredAnimationVariantCount`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AttackSequence_MapsOntoAuthoredAnimationVariantCount()
        {
            Assert.AreEqual(
                0,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(0, 2));
            Assert.AreEqual(
                1,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(1, 2));
            Assert.AreEqual(
                0,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(2, 2));
            Assert.AreEqual(
                1,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(255, 2));
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestSceneEquipmentPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `HeroTestSceneEquipmentPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
