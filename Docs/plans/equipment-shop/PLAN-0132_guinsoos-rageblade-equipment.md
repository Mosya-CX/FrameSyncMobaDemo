# 鬼索狂暴之刃装备

## 本次执行范围

本计划对应原编码 0132 的一次执行：鬼索狂暴之刃装备。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [六格装备配方与唯一标签](../../requirements/equipment-shop/REQ-FEAT-053_equipment-recipes.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Buff/BuffConfigId.cs`：`BuffConfigId`。
- `Assets/Scripts/Gameplay/Equipment/BuffEquipmentModule.cs`：`BuffEquipmentModule`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentCatalogAsset.cs`：`EquipmentCatalogAsset`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：`EquipmentShopRuntime`、`ShopTraderRuntime`、`ShopOperationRecord`、`EquipmentShopOperationType`、`EquipmentShopFailureReason`、`CombatParticipationFlags`、`EquipmentPurchasePlan`。
- `Assets/Scripts/Gameplay/Equipment/OnHitRepeatModule.cs`：`OnHitRepeatModule`。
- `Assets/Scripts/Gameplay/Equipment/PlayerSlot.cs`：`PlayerSlot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private void Awake()
        {
            contentLoadCancellation = new CancellationTokenSource();
            initializationTask = InitializeWithCleanupAsync(
                contentLoadCancellation.Token);
        }
```

`Assets/Scripts/Gameplay/Buff/BuffConfigId.cs`：

```csharp
using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct BuffConfigId : IEquatable<BuffConfigId>, IComparable<BuffConfigId>
    {
        public int Value;

        public BuffConfigId(int value)
        {
            Value = value;
        }

        public bool Equals(BuffConfigId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BuffConfigId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(BuffConfigId other) => Value.CompareTo(other.Value);

        public static bool operator ==(BuffConfigId left, BuffConfigId right) => left.Equals(right);
        public static bool operator !=(BuffConfigId left, BuffConfigId right) => !left.Equals(right);

        public static readonly BuffConfigId Invalid = new BuffConfigId(0);
        public bool IsValid => Value != 0;

        public override string ToString() => $"BuffConfigId({Value})";
    }
}
```

### 输入输出与边界

**六格装备配方与唯一标签**

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

满栏合成先有确定购买计划；成装重复和跨装备排他显式；固定属性使用本装备持有的句柄。

**装备主动被动与可重复命中**

EquipmentEffectDef 内嵌多态 Module，Runtime 持有 EffectUid/Module state；主动一次验证全部模块，再通过仲裁瞬发；On-Hit 重复保留源动作。

每装备最多一个主动 Effect；装备使用使撤销失效；EquipmentTargetPolicy 目前只存在概念提及，待用户确认是否采用及值域。

**购买合成与 Command 二次校验**

EquipmentShopRuntime 从装备数据库计算 EquipmentPurchasePlan；UI RequestCheck 是预检查，ProcessCommand 再以当前确定性状态验证，组件选择和剩余价格稳定。

不能从 UI 预检查直接扣款；懒创建 Trader 不形成第二金币权威；部分堆叠、重复标签和不足余额明确拒绝。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `GameBootstrapPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestSceneEquipmentPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `HeroTestSceneEquipmentPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/Gameplay/Tests/EquipmentShopTransactionTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Purchase_UsesOwnedRecipePartAndFreedLowestSlot`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Purchase_UsesOwnedRecipePartAndFreedLowestSlot()
        {
            EquipmentDefinition component =
                Definition(1, 100, EquipmentTier.Basic);
            EquipmentDefinition target =
                Definition(2, 500, EquipmentTier.Finished);
            target.Recipe = new EquipmentRecipe
            {
                Components = new[]
                {
                    new EquipmentRecipePart
                    {
                        Item = component,
                        Count = 2,
                    },
                },
            };
            EquipmentDefinition filler =
                Definition(3, 10, EquipmentTier.Basic);
            TestContext context =
                CreateContext(component, target, filler);
            Assert.IsTrue(
                context.Handler.Add(component, 0));
            for (int slot = 1;
                 slot < EquipmentHandler.SlotCount;
                 slot++)
                Assert.IsTrue(
                    context.Handler.Add(filler, slot));

            Assert.AreEqual(
                400,
                context.Shop.CalculatePurchasePrice(0, target.Id));
            Assert.IsTrue(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    target.Id,
                    400,
                    context.Handler,
                    out EquipmentPurchasePlan plan,
                    out EquipmentShopFailureReason failure),
                failure.ToString());
            CollectionAssert.AreEqual(
                new[] { 0 },
                plan.ConsumedComponentSlots);
            Assert.AreEqual(0, plan.DestinationSlot);
            Assert.IsTrue(
                context.Shop.ProcessPurchase(
                    0,
                    plan,
                    context.Handler,
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/CellPrefabRefTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `HeroSelectCell_ButtonRefIsButtonAndSelectTipBound`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void HeroSelectCell_ButtonRefIsButtonAndSelectTipBound()
        {
            Validate(
                "Assets/ClientContent/UI/HeroSelectCell.prefab",
                "UI.HeroCell",
                "SelectTip");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
