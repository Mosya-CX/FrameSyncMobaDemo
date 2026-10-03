# 英雄选择界面

## 本次执行范围

本计划对应原编码 0093 的一次执行：英雄选择界面。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [大厅槽位与开局配置](../../requirements/match-flow/REQ-FEAT-002_lobby-slots.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/UI/UIManager.cs`：`UIManager`、`PageRegistration`。
- `Assets/Scripts/Bootstrap/UI/UIPanel.cs`：`UIPanel`。
- `Assets/Scripts/LuaBridge/LuaHost.cs`：`LuaHost`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/UI/UIManager.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        [Serializable]
        private struct PageRegistration
        {
            public UIPageId PageId;
            [HideInInspector]
            public GameObject Prefab;
            public string Address;
            public UIPageLayer Layer;
            public bool Preload;
            public bool OpenOnStart;
        }

        [SerializeField] private Transform pageLayer;
        [SerializeField] private Transform popupLayer;
        [SerializeField] private Transform overlayLayer;
        [SerializeField] private PageRegistration[] pages =
            Array.Empty<PageRegistration>();

        private readonly Dictionary<UIPageId, UIPanel> instances =
            new Dictionary<UIPageId, UIPanel>();
        private LuaManager luaManager;
        private bool initialized;
        private Task initializationTask;
        private CancellationTokenSource lifetimeCancellation;
        private readonly List<IPresentationAssetLease<GameObject>> pageLeases =
            new List<IPresentationAssetLease<GameObject>>();
        private UIPageId pendingMainPage = UIPageId.None;
        private UIPageId pendingOverlay = UIPageId.None;
        private bool pendingCloseAll;
        private UIPageId _currentMainPage =
            UIPageId.None;
        private UIPageId _currentOverlay =
            UIPageId.None;

        public bool IsInitialized => initialized;
        public LuaManager Lua => luaManager;
        public static UIManager Instance { get; private set; }
        public Task Ready => initializationTask ?? Task.CompletedTask;
        public event Action Initialized;

        /// <summary>
        /// HUD owned-equipment focus event (UI design v9.1 2.5): slot +
        /// equipment id, forwarded to the Shop overlay.
        /// </summary>
        public event Action<int, int> ShopOwnedEquipmentFocused;

        private void Awake()
        {
            Instance = this;
            if (luaManager == null)
                luaManager =
                    LuaManager.CreateDefault();
            lifetimeCancellation ??= new CancellationTokenSource();
            ClientSpriteRegistry.SpriteLoaded += RefreshLoadedPages;
            Initialize();
        }

// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Bootstrap/UI/UIPanel.cs`：

```csharp
using System;
using FrameSyncMoba.LuaBridge;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIPage))]
    public sealed class UIPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private string luaModule;
        [SerializeField] private UIRef[] refs =
            Array.Empty<UIRef>();

        private LuaHost host;

        public UIPage Page { get; private set; }
        public bool IsOpen { get; private set; }
        public bool HasLuaHost => host != null;

        public event Action Opened;
        public event Action Closed;

        private void Awake()
        {
            Page = GetComponent<UIPage>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            SetCanvasState(true);
            if (IsOpen)
                return;
            IsOpen = true;
            Opened?.Invoke();
            host?.Show();
        }

        public void Close()
        {
            if (!gameObject.activeSelf && !IsOpen)
                return;
            SetCanvasState(false);
            IsOpen = false;
            Closed?.Invoke();
            host?.Hide();
            gameObject.SetActive(false);
        }

        public void Build(LuaManager luaManager)
        {
            var lists =
                GetComponentsInChildren<UIList>(true);
            for (int i = 0; i < lists.Length; i++)
                lists[i].SetManager(luaManager);
            if (string.IsNullOrEmpty(luaModule))
                return;
            host?.Dispose();
            host = null;
            host = luaManager.CreatePageHost(
                luaModule,
                refs);
        }

        public void Refresh()
        {
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

**HUD 数值技能与小地图**

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。

**大厅槽位与开局配置**

LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。

断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/LuaBridge/Tests/LuaHostAndManagerTests.cs`：EditMode，程序集 `FrameSyncMoba.LuaBridge.Tests`，函数 `CreateDefault_ExecutesLuaInit_AndBecomesReady`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CreateDefault_ExecutesLuaInit_AndBecomesReady()
        {
            Assert.That(_manager.IsReady, Is.True);
            Assert.That(
                _manager.ReadGlobalInt("_LuaUiInitialized"),
                Is.EqualTo(1));
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationAddressablesMigrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ProjectilesAreSplitIntoLogicAndAddressableViews`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ProjectilesAreSplitIntoLogicAndAddressableViews()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            List<PrefabEntry> entries = FindEntries(
                table,
                settings,
                PrefabKind.Projectile);
            Assert.That(entries.Count, Is.EqualTo(8));
            for (int i = 0; i < entries.Count; i++)
            {
                PrefabEntry entry = entries[i];
                AddressableAssetEntry logic =
                    FindEntryByAddress(settings, entry.LogicAssetAddress);
                Assert.That(logic, Is.Not.Null, entry.LogicAssetAddress);
                string logicPath = logic.AssetPath;
                GameObject logicPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(logicPath);
                Assert.That(logicPath,
                    Does.StartWith(
                        "Assets/Config/Formal/Prefabs/Logic/Projectile/"));
                Assert.That(
                    logicPrefab.GetComponent<PhysicsEntity2D>(),
                    Is.Not.Null,
                    logicPath);
                Assert.That(
                    logicPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Empty,
                    logicPath);
                AddressableAssetEntry view =
                    FindEntryByAddress(settings, entry.ClientViewAddress);
                Assert.That(view, Is.Not.Null, entry.ClientViewAddress);
                GameObject viewPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(view.AssetPath);
                Assert.That(viewPrefab.GetComponent<PhysicsEntity2D>(), Is.Null);
                Assert.That(
                    viewPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty,
                    view.AssetPath);
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
