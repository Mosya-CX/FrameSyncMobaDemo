# Lua 界面重建

## 本次执行范围

本计划对应原编码 0126 的一次执行：Lua 界面重建。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/UI/UIManager.cs`：`UIManager`、`PageRegistration`。
- `Assets/Scripts/Bootstrap/UI/UIPanel.cs`：`UIPanel`。
- `Assets/Scripts/LuaBridge/LuaManager.cs`：`LuaManager`。
- `Assets/Scripts/LuaBridge/LuaHost.cs`：`LuaHost`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。
- `Assets/Scripts/LuaBridge/UIRef.cs`：`UIRef`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/UI/UIManager.cs`：

```csharp
        public void Refresh(UIPageId pageId)
        {
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.Refresh();
        }
```

`Assets/Scripts/Bootstrap/UI/UIPanel.cs`：

```csharp
        public void Refresh()
        {
            if (!IsOpen)
                return;
            host?.Refresh();
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

- `Assets/Scripts/LuaBridge/Tests/LuaHostAndManagerTests.cs`：EditMode，程序集 `FrameSyncMoba.LuaBridge.Tests`，函数 `ManagerDispose_ReleasesOutstandingHosts`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ManagerDispose_ReleasesOutstandingHosts()
        {
            // Hosts are intentionally not disposed by the caller: the scene
            // teardown order may destroy cells after UIManager.OnDestroy, so
            // LuaManager must release every outstanding host before closing
            // the LuaEnv. Otherwise xLua throws
            // "try to dispose a LuaEnv with C# callback".
            LuaHost page =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    null);
            LuaHost cell =
                _manager.CreateCellHost(
                    "UI.Core.TestCell",
                    null);

            Assert.DoesNotThrow(
                () => _manager.Dispose());
            Assert.That(
                page.IsDisposed,
                Is.True,
                "Manager dispose must release outstanding page hosts.");
            Assert.That(
                cell.IsDisposed,
                Is.True,
                "Manager dispose must release outstanding cell hosts.");

            // TearDown must tolerate the already-disposed manager.
            _manager = null;
        }
```
- `Assets/Scripts/LuaBridge/Tests/UIDisplayConvertTests.cs`：EditMode，程序集 `FrameSyncMoba.LuaBridge.Tests`，函数 `ResourceInt_Floors_And_ClampsAtZero`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ResourceInt_Floors_And_ClampsAtZero()
        {
            Assert.That(
                UIDisplayConvert.ResourceInt((fp)100.8m),
                Is.EqualTo(100));
            Assert.That(
                UIDisplayConvert.ResourceInt((fp)(-3.2m)),
                Is.EqualTo(0));
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientFrameworkSmokeSceneTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `ClientFrameworkSmokeSceneTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/UIManagerPrefabPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `UIManagerPrefabPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
