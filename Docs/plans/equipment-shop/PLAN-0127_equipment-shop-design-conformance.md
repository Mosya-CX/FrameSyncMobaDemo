# 装备商店合同补全

## 本次执行范围

本计划对应原编码 0127 的一次执行：装备商店合同补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [六格装备配方与唯一标签](../../requirements/equipment-shop/REQ-FEAT-053_equipment-recipes.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [卖出撤销与交易失效](../../requirements/equipment-shop/REQ-FEAT-056_shop-undo.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Equipment/EquipmentDatabase.cs`：`EquipmentDatabase`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentDefinition.cs`：`EquipmentDefinition`、`EquipmentFixedStatAuthoring`、`EquipmentRecipe`、`EquipmentRecipePart`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：`EquipmentShopRuntime`、`ShopTraderRuntime`、`ShopOperationRecord`、`EquipmentShopOperationType`、`EquipmentShopFailureReason`、`CombatParticipationFlags`、`EquipmentPurchasePlan`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentTagDefinition.cs`：`EquipmentTagDefinition`。
- `Assets/Scripts/Gameplay/Equipment/UniqueEquipmentTagTable.cs`：`UniqueEquipmentTagTable`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentCatalogAsset.cs`：`EquipmentCatalogAsset`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Equipment/EquipmentDatabase.cs`：

```csharp
        public void SetUniqueTagTable(
            UniqueEquipmentTagTable table)
        {
            _uniqueTagTable = table;
            _uniqueTagTable?.Initialize();
        }
```

`Assets/Scripts/Gameplay/Equipment/EquipmentDefinition.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring ScriptableObject for one equipment item
    /// (Equipment/Gold v12 section 2.1).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Equipment",
        menuName = "MOBA/Equipment")]
    public sealed class EquipmentDefinition :
        ScriptableObject
    {
        public int Id;
        public string Name;
        [TextArea]
        public string Description;
        [HideInInspector]
        public Sprite Icon;
        public string IconAddress;
        public EquipmentTier Tier;
        public int Value;
        public int MaxStack = 1;

        public EquipmentFixedStatAuthoring[] FixedStats;
        public EquipmentEffectDef[] Effects;
        public EquipmentTagDefinition[] Tags;
        public EquipmentRecipe Recipe;

        /// <summary>
        /// Stackability is derived from Tier (design v12 2.4):
        /// only Consumable items stack.
        /// </summary>
        public bool CanStack =>
            Tier == EquipmentTier.Consumable;

        public EquipmentFixedStat[] BakedFixedStats { get; private set; } =
            Array.Empty<EquipmentFixedStat>();
        public bool IsBaked { get; private set; }

        public bool IsValid => Id != 0;

        public void Bake(int tickRate = 30)
        {
            DeterministicTimeConversion.ValidateSupportedTickRate(
                tickRate);
            EquipmentFixedStatAuthoring[] source =
                FixedStats ?? Array.Empty<EquipmentFixedStatAuthoring>();
            var baked = new EquipmentFixedStat[source.Length];
            for (int i = 0; i < source.Length; i++)
                baked[i] = new EquipmentFixedStat(source[i].Stat, (fp)source[i].Value);
            BakedFixedStats = baked;
            EquipmentEffectDef[] effects =
                Effects ?? Array.Empty<EquipmentEffectDef>();
            for (int effectIndex = 0;
                 effectIndex < effects.Length;
                 effectIndex++)
            {
                EquipmentEffectDef effect = effects[effectIndex];
                if (effect == null)
                    continue;
                EquipmentActiveSettings settings =
                    effect.ActiveSettings;
                settings.CooldownTicks =
                    settings.Cooldown.IsAuthored
                        ? settings.Cooldown.BakeTicks(tickRate)
// 方法后续请阅读上述真实源码；这里是节选。
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

**商店商品详情与余额刷新**

Shop.lua 直接调用 IEquipmentShopView 与 Request 接口；CurrentAvailableGold 来自 GoldIncomeRuntime 确认累计加商店增量。

预测收入不提前可用；普通回滚和 AuthorityRecovery 后修订通知重刷；界面不暴露 ProcessCommand 或写交易状态。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/EquipmentShopRequestTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `RequestPurchase_Allowed_SubmitsCanonicalCommand`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RequestPurchase_Allowed_SubmitsCanonicalCommand()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);

            EquipmentShopRequestCheck check =
                context.Shop.RequestPurchase(
                    0,
                    item.Id);

            Assert.That(check.Allowed, Is.True);
            Assert.That(
                context.Submitter.Purchases,
                Is.EqualTo(new[] { item.Id }));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(1));
        }
```
- `Assets/Scripts/Gameplay/Tests/EquipmentTagAndCatalogTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `UniqueTagInTable_ConflictingPurchase_Rejected`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UniqueTagInTable_ConflictingPurchase_Rejected()
        {
            EquipmentTagDefinition bootsTag =
                EquipmentTagDefinition.Create(
                    "Boots",
                    1001);
            EquipmentDefinition bootsA =
                MakeDefinition(1, "BootsA", 300, bootsTag);
            EquipmentDefinition bootsB =
                MakeDefinition(2, "BootsB", 400, bootsTag);

            TestContext context =
                CreateContext(
                    bootsA,
                    bootsB,
                    bootsTag);
            Assert.IsTrue(
                context.Handler.Add(bootsA, 0));

            Assert.IsFalse(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    bootsB.Id,
                    1000,
                    context.Handler,
                    out _,
                    out EquipmentShopFailureReason failure));
            Assert.That(
                failure,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .UniqueTagConflict));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
