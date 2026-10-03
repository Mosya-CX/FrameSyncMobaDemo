# Lua 界面桥接最初提案

## 本次执行范围

本计划对应原编码 0053 的一次执行：Lua 界面桥接最初提案。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/UiSnapshotDto.cs`：`UiSnapshotDtoAlias`。
- `Assets/Scripts/LuaBridge/UiSnapshotDto.cs`：`UiSnapshotDto`。
- `Assets/Scripts/Bootstrap/UIBindingTable.cs`：`UIBindingTable`、`BindingEntry`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/LuaBridge/LuaBridge.cs`：`LuaBridge`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/UiSnapshotDto.cs`：

```csharp
using FrameSyncMoba.LuaBridge;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Type-forward: Bootstrap uses LuaBridge's UiSnapshotDto as the
    /// canonical UI snapshot type. This file exists to avoid breaking
    /// existing references. All new code should reference
    /// FrameSyncMoba.LuaBridge.UiSnapshotDto directly.
    /// </summary>
    public static class UiSnapshotDtoAlias
    {
        public static readonly UiSnapshotDto Empty = UiSnapshotDto.Empty;
    }
}
```

`Assets/Scripts/LuaBridge/UiSnapshotDto.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Per-tick read-only snapshot of UI-relevant Gameplay state.
    /// Populated at tick-end from deterministic state.
    /// Does NOT enter GameplaySnapshot, SharedGameplayChecksum,
    /// or any deterministic path.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 1.4, 10
    /// </summary>
    public struct UiSnapshotDto
    {
        public fp CurrentHealth;
        public fp MaxHealth;
        public fp CurrentResource;
        public fp MaxResource;
        public int CurrentGold;
        public int ConfirmedGold;
        public int CooldownRemaining0;
        public int CooldownRemaining1;
        public int CooldownRemaining2;
        public int CooldownRemaining3;
        public int CooldownTotal0;
        public int CooldownTotal1;
        public int CooldownTotal2;
        public int CooldownTotal3;
        public int UnitLevel;
        public int CurrentExperience;
        public int ExperienceForNextLevel;

        // Scoreboard fields (populated from MatchStatisticsRuntime)
        public int PlayerCount;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int CreepScore;
        // Aggregated all-player stats arrays for Lua scoreboard rendering
        public System.Collections.Generic.List<int> AllPlayerKills;
        public System.Collections.Generic.List<int> AllPlayerDeaths;
        public System.Collections.Generic.List<int> AllPlayerAssists;
        public System.Collections.Generic.List<int> AllPlayerCreepScore;
        public System.Collections.Generic.List<string> AllPlayerNames;

        public static readonly UiSnapshotDto Empty = default;
    }
}
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

## 执行结果

当期接口提案已被后续执行批次替代；保留关闭边界，不重新实现已淘汰接口。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Bootstrap/Tests/EditMode/CooldownPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `UiSnapshotDto_Default_AllCooldownsZero`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UiSnapshotDto_Default_AllCooldownsZero()
        {
            var dto = UiSnapshotDto.Empty;
            Assert.That(dto.CooldownRemaining0, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining1, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining2, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining3, Is.EqualTo(0));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
