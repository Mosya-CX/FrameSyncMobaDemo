# UOS NGO 应用与大厅流程

## 本次执行范围

本计划对应原编码 0120 的一次执行：UOS NGO 应用与大厅流程。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [UOS 配置与连接模式](../../requirements/match-flow/REQ-FEAT-004_uos-configuration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [大厅槽位与开局配置](../../requirements/match-flow/REQ-FEAT-002_lobby-slots.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：`FrameSyncNetworkBridge`、`PresentationPingTracker`、`FrameSyncWireCodec`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/PlayerSlot.cs`：`PlayerSlot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class FrameSyncNetworkBridge : MonoBehaviour,
        ICommandNetworkTimingProvider
    {
        private const string BundleMessage =
            "FrameSyncMoba.GameplayCommandBundle.v1";
        private const string RelayMessage =
            "FrameSyncMoba.AcceptedCommandRelay.v1";
        private const string AuthorityMessage =
            "FrameSyncMoba.AuthorityFrame.v1";
        private const string RecoveryRequestMessage =
            "FrameSyncMoba.AuthorityRecoveryRequest.v1";
        private const string RecoveryResponseMessage =
            "FrameSyncMoba.AuthorityRecoveryResponse.v1";
        private const string MatchResultMessage =
            "FrameSyncMoba.MatchResultState.v1";
        private const string PingRequestMessage =
            "FrameSyncMoba.CommandTimingPingRequest.v2";
        private const string PingResponseMessage =
            "FrameSyncMoba.CommandTimingPingResponse.v2";

        [SerializeField] private NetworkManager networkManager;
        [SerializeField, Min(1)]
        private int pingRefreshIntervalMilliseconds;
        [FormerlySerializedAs("pingRefreshIntervalSeconds")]
        [SerializeField, HideInInspector]
        private float legacyPingRefreshIntervalSeconds;
        [Header("Adaptive Command Timing")]
        [SerializeField, Min(1)]
        private int minimumCommandTimingSamples = 4;
        [SerializeField, Min(1)]
        private int commandTimingSampleMaxAgeMilliseconds = 3000;
        [SerializeField, Min(0)]
        private int minimumJitterBudgetMilliseconds = 10;
        [SerializeField, Min(0)]
        private int commandProcessingBudgetMilliseconds = 10;
        [SerializeField, Min(0)]
        private int jitterVariationMultiplier = 2;
        [SerializeField, Min(0)]
        private int desiredServerSlackTicks = 1;

        private FrameSyncGameRuntime runtime;
        private Func<ulong, GameplayCommand, bool>
            authorizeCommand;
        private uint nextBundleSequence = 1;
        private uint nextResultRevision = 1;
        private bool registered;
        private string matchId;
        private MatchResultState? pendingMatchResult;
        private PresentationPingTracker pingTracker;
        private long scheduledServerGameplayActivationRealtimeMilliseconds =
            -1;
        private readonly GameplayCommandSendLedger commandSendLedger =
            new GameplayCommandSendLedger();

        public bool IsBound => runtime != null;
        internal bool IsConnectedClient =>
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

**UOS 配置与连接模式**

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。

**大厅槽位与开局配置**

LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。

断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `CommandBundle_ProducesStablePerTickReplacementRelays`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CommandBundle_ProducesStablePerTickReplacementRelays()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand first = GameplayCommand.CreateMove(
                Header(unitUid, 3, 1),
                new fp2(fp.one, fp.zero));
            GameplayCommand replacement = GameplayCommand.CreateMove(
                Header(unitUid, 3, 2),
                new fp2((fp)2, fp.zero));
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] relays = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                command => command.ControlledUnitUid == unitUid);

            Assert.AreEqual(1, relays.Length);
            Assert.AreEqual(3, relays[0].TargetTick);
            Assert.AreEqual(1u, relays[0].RelayRevision);
            GameplayCommand[] canonical = relays[0].DecodeCommands();
            Assert.AreEqual(1, canonical.Length);
            Assert.AreEqual(2u, canonical[0].CommandSeq);
            Assert.AreEqual(new fp2((fp)2, fp.zero),
                canonical[0].MoveTargetPoint);

            AcceptedCommandRelay[] duplicate = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                null);
            Assert.AreEqual(0, duplicate.Length);
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `TestAccount_CommandLineOverridesPersistedIdentity`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void TestAccount_CommandLineOverridesPersistedIdentity()
        {
            var persistence = new MemoryPersistence("persisted");
            var service = new TestAccountBootstrapService(
                persistence,
                () => "generated");

            ClientAccountSession session = service.Resolve(
                new[] { "--TestAccountId=command-line" });

            Assert.AreEqual(
                "command-line",
                session.TestAccountId);
            Assert.AreEqual(
                "command-line",
                persistence.Value);
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks()
        {
            GlobalGameplayData global = AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                "Assets/Config/Formal/GlobalGameplayData.asset");
            UnitRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");
            AbilityRuntimeCatalogAsset abilityCatalog =
                AssetDatabase.LoadAssetAtPath<AbilityRuntimeCatalogAsset>(
                    "Assets/Config/Formal/Abilities/VarusAbilityRuntimeCatalog.asset");
            DeterministicMapConfig mapConfig =
                AssetDatabase.LoadAssetAtPath<DeterministicMapConfig>(
                    "Assets/Config/Formal/FullMatchDeterministicMapConfig.asset");
            Assert.That(global, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(abilityCatalog, Is.Not.Null);
            Assert.That(mapConfig, Is.Not.Null);

            GlobalGameplayData testGlobal =
                CreateLegacyTestGlobal(global, out GlobalPrefabTable runtimeTable);
            var root = new GameObject("FrameworkSmokeBootstrapTest");
            try
            {
                GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
                SetField(bootstrap, "globalGameplayData", testGlobal);
                SetField(bootstrap, "unitRuntimeCatalog", catalog);
                SetField(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    abilityCatalog);
                SetField(
                    bootstrap,
                    "deterministicMapConfig",
                    mapConfig);
                SetField(bootstrap, "dedicatedServer", true);
                SetField(bootstrap, "driveSimulationFromUnityUpdate", false);
                SetField(bootstrap, "initialUnitSpawns", new System.Collections.Generic.List<
                    InitialUnitSpawnAuthoring>
                {
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 0,
                        UnitPrototypeId = 1001,
                        TeamId = 1,
                        UseMapSpawnPoint = true,
                        SpawnPointId = 0,
                        PlayerControlled = true,
                        PlayerSlot = 0,
                    },
                    new InitialUnitSpawnAuthoring
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
