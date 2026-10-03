# Lua 界面桥接基础实现

## 本次执行范围

本计划对应原编码 0053 的一次执行：Lua 界面桥接基础实现。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/LuaBridge/LuaBridge.cs`：`LuaBridge`。
- `Assets/Scripts/Bootstrap/UiSnapshotDto.cs`：`UiSnapshotDtoAlias`。
- `Assets/Scripts/LuaBridge/UiSnapshotDto.cs`：`UiSnapshotDto`。
- `Assets/Scripts/LuaBridge/LuaRuntime.cs`：`LuaRuntime`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/LuaDataCache.cs`：`LuaDataCache`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/LuaBridge/LuaBridge.cs`：

```csharp
        public void PushTickData(int tick, in UiSnapshotDto dto, UnitType controlledUnit)
        {
            Runtime.Clear();

            Runtime.SetTableField("HUD", "CurrentHealth", dto.CurrentHealth);
            Runtime.SetTableField("HUD", "MaxHealth", dto.MaxHealth);
            Runtime.SetTableField("HUD", "CurrentResource", dto.CurrentResource);
            Runtime.SetTableField("HUD", "MaxResource", dto.MaxResource);
            Runtime.SetTableField("HUD", "UnitLevel", dto.UnitLevel);
            Runtime.SetTableField("HUD", "CurrentExperience", dto.CurrentExperience);
            Runtime.SetTableField("HUD", "ExperienceForNextLevel", dto.ExperienceForNextLevel);
            Runtime.SetTableField("HUD", "CurrentGold", dto.CurrentGold);
            Runtime.SetTableField("HUD", "ConfirmedGold", dto.ConfirmedGold);
            Runtime.SetTableField("HUD", "CooldownRemaining0", dto.CooldownRemaining0);
            Runtime.SetTableField("HUD", "CooldownRemaining1", dto.CooldownRemaining1);
            Runtime.SetTableField("HUD", "CooldownRemaining2", dto.CooldownRemaining2);
            Runtime.SetTableField("HUD", "CooldownRemaining3", dto.CooldownRemaining3);
            Runtime.SetTableField("HUD", "CooldownTotal0", dto.CooldownTotal0);
            Runtime.SetTableField("HUD", "CooldownTotal1", dto.CooldownTotal1);
            Runtime.SetTableField("HUD", "CooldownTotal2", dto.CooldownTotal2);
            Runtime.SetTableField("HUD", "CooldownTotal3", dto.CooldownTotal3);
            Runtime.SetGlobal("CurrentTick", tick);

            // Scoreboard
            Runtime.SetTableField("HUD", "PlayerCount", dto.PlayerCount);
            Runtime.SetTableField("HUD", "Kills", dto.Kills);
            Runtime.SetTableField("HUD", "Deaths", dto.Deaths);
            Runtime.SetTableField("HUD", "Assists", dto.Assists);
            Runtime.SetTableField("HUD", "CreepScore", dto.CreepScore);

            // All-player scoreboard arrays
            SetIntArray("Scoreboard", "Kills", dto.AllPlayerKills?.ToArray());
            SetIntArray("Scoreboard", "Deaths", dto.AllPlayerDeaths?.ToArray());
            SetIntArray("Scoreboard", "Assists", dto.AllPlayerAssists?.ToArray());
            SetIntArray("Scoreboard", "CreepScore", dto.AllPlayerCreepScore?.ToArray());
            SetStringArray("Scoreboard", "Names", dto.AllPlayerNames?.ToArray());

            if (controlledUnit != null)
                Runtime.SetGlobal("ControlledUnitName", controlledUnit.name ?? "");
        }
```

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

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
