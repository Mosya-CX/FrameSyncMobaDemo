# 计分板与小地图

## 本次执行范围

本计划对应原编码 0087 的一次执行：计分板与小地图。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/MinimapController.cs`：`MinimapController`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/UiSnapshotDto.cs`：`UiSnapshotDtoAlias`。
- `Assets/Scripts/LuaBridge/UiSnapshotDto.cs`：`UiSnapshotDto`。
- `Assets/Scripts/Bootstrap/LuaDataCache.cs`：`LuaDataCache`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/LuaBridge/LuaBridge.cs`：`LuaBridge`。
- `Assets/Scripts/FrameSync/MatchRuleRuntime.cs`：`MatchPhase`、`MatchEndReason`、`MatchTopologyRole`、`MatchStatisticsEntry`、`MatchStatisticsRuntimeSnapshot`、`MatchStatisticsRuntime`、`StatisticKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/MinimapController.cs`：

```csharp
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.UI;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Renders a minimap overlay with unit dots, lane path lines,
    /// and tower position markers.
    /// Reads unit positions from UnitWorld each frame.
    /// Presentation-only.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 4-5
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Minimap Settings")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private int textureWidth = 256;
        [SerializeField] private int textureHeight = 256;
        [SerializeField] private fp worldWidth = (fp)200;
        [SerializeField] private fp worldHeight = (fp)200;

        [Header("Dot Colors")]
        [SerializeField] private Color32 allyColor = new Color32(0, 100, 255, 255);
        [SerializeField] private Color32 enemyColor = new Color32(255, 50, 50, 255);
        [SerializeField] private Color32 neutralColor = new Color32(200, 200, 0, 255);

        [Header("Structure Markers")]
        [SerializeField] private Color32 towerAllyColor = new Color32(0, 80, 200, 255);
        [SerializeField] private Color32 towerEnemyColor = new Color32(200, 50, 50, 255);
        [SerializeField] private Color32 laneLineColor = new Color32(40, 40, 60, 255);

        [Header("Visibility")]
        [SerializeField] private bool alwaysVisible = true;

        private Texture2D _texture;
        private Color32[] _pixels;
        private int _localPlayerSlot = -1;
        private UnitType _controlledUnit;
        private UnitWorld _unitWorld;

        // Cached lane endpoints for overlay drawing
        private static readonly fp2[] _laneTop = new[] { new fp2(-fp.one * 80, fp.one * 80), new fp2(fp.one * 80, fp.one * 80) };
        private static readonly fp2[] _laneMid = new[] { new fp2(-fp.one * 80, fp.zero), new fp2(fp.one * 80, fp.zero) };
        private static readonly fp2[] _laneBot = new[] { new fp2(-fp.one * 80, -fp.one * 80), new fp2(fp.one * 80, -fp.one * 80) };

        private void Awake()
        {
            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _pixels = new Color32[textureWidth * textureHeight];

            if (minimapImage == null)
                minimapImage = GetComponentInChildren<RawImage>();
            if (minimapImage != null)
                minimapImage.texture = _texture;
        }

        /// <summary>
        /// Bind to the current local player and UnitWorld for team-aware dot coloring.
        /// </summary>
        public void Bind(UnitType controlledUnit, UnitWorld unitWorld)
        {
            _controlledUnit = controlledUnit;
            _unitWorld = unitWorld;
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.Physics;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    [Serializable]
    public struct InitialUnitSpawnAuthoring
    {
        [Min(0)] public int StableSpawnOrder;
        [Min(1)] public int UnitPrototypeId;
        [Min(0)] public int TeamId;
        public Vector2 Position;
        public Vector2 Forward;
        public bool UseMapSpawnPoint;
        [Min(0)] public int SpawnPointId;
        public MatchTopologyRole MatchTopologyRole;
        public bool EnableTowerAI;
        public bool PlayerControlled;
        [Min(0)] public int PlayerSlot;
    }

    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Project-wide deterministic configuration")]
        [SerializeField] private GlobalGameplayData globalGameplayData;
        [SerializeField, HideInInspector] private UnitRuntimeCatalogAsset unitRuntimeCatalog;
        [SerializeField, HideInInspector] private AbilityRuntimeCatalogAsset abilityRuntimeCatalog;
        [SerializeField, HideInInspector] private ProjectileRuntimeCatalogAsset projectileRuntimeCatalog;
        [SerializeField, HideInInspector] private DeterministicMapConfig deterministicMapConfig;
        [SerializeField, HideInInspector] private EquipmentCatalogAsset equipmentCatalog;
        [SerializeField, HideInInspector] private BuffCatalogAsset buffCatalog;
        [SerializeField, HideInInspector] private CrowdControlCatalogAsset crowdControlCatalog;
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private bool driveSimulationFromUnityUpdate = true;

        [Header("Optional online application flow")]
        [SerializeField] private bool enableOnlineApplicationFlow;
        [Tooltip("Explicit local NGO path. It bypasses UOS only for local development and never reports provider success.")]
        [SerializeField] private bool localDevelopmentNetworkFlow;
        [SerializeField] private bool autoApplyLocalFixturePayload = true;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FrameSyncNetworkBridge frameSyncNetworkBridge;

        [Header("Frozen match-start composition")]
        [SerializeField] private List<InitialUnitSpawnAuthoring> initialUnitSpawns =
            new List<InitialUnitSpawnAuthoring>();

        [Header("Client-local input (unused on Dedicated Server)")]
        [SerializeField] private PlayerInputController playerInputController;
        [SerializeField] private Camera gameplayCamera;

        [Header("Presentation (client only)")]
        [SerializeField] private SkillIndicatorDriver indicatorDriver;
        [SerializeField] private PresentationEventDispatcher presentationDispatcher;
        [SerializeField] private VfxEventHandler vfxEventHandler;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**HUD 数值技能与小地图**

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

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
- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnMultipleKinds_GetByKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
