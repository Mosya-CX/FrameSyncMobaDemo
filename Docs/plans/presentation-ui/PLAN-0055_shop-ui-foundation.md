# 商店界面基础

## 本次执行范围

本计划对应原编码 0055 的一次执行：商店界面基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [卖出撤销与交易失效](../../requirements/equipment-shop/REQ-FEAT-056_shop-undo.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/UI/EquipmentSlotView.cs`：`EquipmentSlotView`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：`EquipmentShopRuntime`、`ShopTraderRuntime`、`ShopOperationRecord`、`EquipmentShopOperationType`、`EquipmentShopFailureReason`、`CombatParticipationFlags`、`EquipmentPurchasePlan`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentDatabase.cs`：`EquipmentDatabase`。
- `Assets/Scripts/Bootstrap/LuaDataCache.cs`：`LuaDataCache`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。
- `Assets/Scripts/LuaBridge/LuaBridge.cs`：`LuaBridge`。

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

`Assets/Scripts/Bootstrap/UI/EquipmentSlotView.cs`：

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Per-slot reusable UI controller for equipment display.
    /// Used in both the shop catalog list and the owned equipment grid.
    /// Presentation-only — never writes deterministic Gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text stackText;
        [SerializeField] private GameObject highlightFrame;

        public int EquipmentId { get; private set; }
        public int SlotIndex { get; private set; }

        private System.Action<EquipmentSlotView> _onClick;

        public void Initialize(
            int equipmentId,
            string displayName,
            int price,
            int stackCount,
            int slotIndex,
            Sprite icon,
            System.Action<EquipmentSlotView> onClick)
        {
            EquipmentId = equipmentId;
            SlotIndex = slotIndex;
            _onClick = onClick;

            if (nameText != null) nameText.text = displayName ?? "";
            if (priceText != null) priceText.text = price > 0 ? price.ToString() : "";
            if (stackText != null) stackText.text = stackCount > 1 ? stackCount.ToString() : "";
            if (iconImage != null) iconImage.sprite = icon;
            if (highlightFrame != null) highlightFrame.SetActive(false);

            // Code-driven creation when no prefab is assigned
            if (iconImage == null && nameText == null && priceText == null && stackText == null)
                BuildDefaultHierarchy();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightFrame != null)
                highlightFrame.SetActive(highlighted);
        }

        public void OnSlotClicked()
        {
            _onClick?.Invoke(this);
        }

        private void BuildDefaultHierarchy()
        {
            // Simple code-driven layout: horizontal row
            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(4, 4, 2, 2);

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

**HUD 数值技能与小地图**

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。

**商店商品详情与余额刷新**

Shop.lua 直接调用 IEquipmentShopView 与 Request 接口；CurrentAvailableGold 来自 GoldIncomeRuntime 确认累计加商店增量。

预测收入不提前可用；普通回滚和 AuthorityRecovery 后修订通知重刷；界面不暴露 ProcessCommand 或写交易状态。

**购买合成与 Command 二次校验**

EquipmentShopRuntime 从装备数据库计算 EquipmentPurchasePlan；UI RequestCheck 是预检查，ProcessCommand 再以当前确定性状态验证，组件选择和剩余价格稳定。

不能从 UI 预检查直接扣款；懒创建 Trader 不形成第二金币权威；部分堆叠、重复标签和不足余额明确拒绝。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/EquipmentShopRuntimeSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `RestoreResolve_PreservesValidControlledUnitReference`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RestoreResolve_PreservesValidControlledUnitReference()
        {
            UnitWorld world = CreateWorldWithUnit(out Unit unit);
            EquipmentShopRuntime source = CreateRuntime(world);
            source.GetOrCreateTrader(0, unit.UnitUid);
            EquipmentShopRuntimeSnapshot snapshot = default;
            source.Capture(ref snapshot);

            EquipmentShopRuntime restored = CreateRuntime(world);
            restored.Restore(snapshot);
            restored.Resolve(default);

            Assert.AreEqual(
                unit.UnitUid,
                restored.GetTrader(0).ControlledUnitUid);
        }
```
- `Assets/Scripts/LuaBridge/Tests/LuaBridgeTests.cs`：EditMode，程序集 `FrameSyncMoba.LuaBridge.Tests`，函数 `SetGlobal_Int_CanRetrieve`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SetGlobal_Int_CanRetrieve()
        {
            var rt = new LuaRuntime();
            rt.SetGlobal("TestValue", 42);
            Assert.That(rt.TryGetGlobal<int>("TestValue", out int val), Is.True);
            Assert.That(val, Is.EqualTo(42));
        }
```
- `Assets/Scripts/FrameSync/Tests/GoldIncomeRuntimeContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 500);

            runtime.BeginTick(0);
            GoldIncomeRecordBatch empty = runtime.SealTick(0);
            Assert.AreNotEqual(0UL, empty.Digest.Value);
            Assert.AreEqual(0, empty.Records.Length);
            runtime.ConfirmAcceptedTick(0);

            runtime.BeginTick(1);
            runtime.RequestGoldIncome(1, 25, GoldIncomeReason.UnitKill);
            GoldIncomeRecordBatch income = runtime.SealTick(1);
            Assert.AreEqual(0, income.Records[0].IncomeSequenceInTick);
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => runtime.ConfirmAcceptedTick(2));
            runtime.ConfirmAcceptedTick(1);
            Assert.AreEqual(525, runtime.GetConfirmedAvailableGold(1));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
