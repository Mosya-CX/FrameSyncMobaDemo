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
        [SerializeField] private AttackSfxHandler attackSfxHandler;
        [SerializeField] private HitReactionPresenter hitReactionPresenter;
        [SerializeField] private DeathPresenter deathPresenter;

        [Header("Lua UI Bridge")]
        [SerializeField] private LuaBridge.LuaBridge luaBridge;

        [Header("Prefab UI")]
        [SerializeField] private UIManager uiManager;

        [Header("HUD Elements (0087/0089)")]
        [SerializeField] private MinimapController minimapController;
        [SerializeField] private ClientUiActionRouter clientUiActionRouter;

        [Header("Explicit non-hero map topology")]
        [SerializeField] private MinionWaveConfig minionWaveConfig;
        [SerializeField] private LaneAuthoring[] laneAuthoring =
            Array.Empty<LaneAuthoring>();
        [SerializeField] private FlowFieldBakeAsset[]
            flowFieldAssets =
                Array.Empty<FlowFieldBakeAsset>();
        [SerializeField] private FlowFieldSceneAuthoring
            flowFieldAuthoring;
        [SerializeField] private JungleCamp[] jungleCamps =
            Array.Empty<JungleCamp>();

        public FrameSyncGameRuntime Runtime { get; private set; }
        public UnitWorld UnitWorld { get; private set; }
        public PhysicsWorld PhysicsWorld { get; private set; }
        public bool IsInitialized => Runtime != null;
        public Task InitializationTask =>
            initializationTask ?? Task.CompletedTask;
        public bool IsMatchReady => matchBootstrapApplied;
        public bool IsLaunchCommitted =>
            !UsesNetworkSimulation ||
            launchCommitApplied;
        public FrameSyncVersionHandshake LocalVersions { get; private set; }
        public MatchFlowStateMachine MatchFlow { get; private set; }
        public GameApplicationFlowManager ApplicationFlow { get; private set; }
        public int MaxLogicTicksPerUnityFrame { get; private set; }
        public bool UsesNetworkSimulation =>
            enableOnlineApplicationFlow ||
            localDevelopmentNetworkFlow;
        public bool IsLocalPlayerBound { get; private set; }
        public int LocalPlayerSlot { get; private set; } = -1;
        public UnitUid LocalControlledUnitUid { get; private set; }

        private long logicAccumulatorMillisecondRateUnits;
        private double logicDeltaSeconds;
        private long recoveryAccumulatorMillisecondRateUnits;
        private long lastUnityUpdateMonotonicMilliseconds = -1L;
        private IEquipmentShopView localShopView;
        private GameStartConfig? activeGameStartConfig;
        private int recoveryControlTick;
        private bool hudLaunchPending;
        private float gameLoadProgress = 0.7f;
        private string gameLoadStatus = "Waiting for match data";
        private long loadWaitStartMonotonicMilliseconds;
        private long loadWaitDurationMilliseconds = 1;
        private long launchServerTimeMilliseconds;
        private long launchPacingOriginMonotonicMilliseconds = -1;
        private IFrameSyncLaunchClock launchClock;
        private int matchStartTick;
        private bool launchScheduleLogged;
        private BakedGlobalGameplayData bakedConfig;
        private BakedDeterministicMapData bakedMap;
        private bool matchBootstrapApplied;
        private bool launchCommitApplied;
        private List<InitialUnitSpawnAuthoring> frozenInitialSpawns =
            new List<InitialUnitSpawnAuthoring>();
        private LaneRuntimeData[] nonHeroLanes =
            Array.Empty<LaneRuntimeData>();
        private CancellationTokenSource contentLoadCancellation;
        private Task initializationTask;
        private AddressableMatchContentScope matchContentScope;
        private bool isDestroying;

        private void Awake()
        {
            contentLoadCancellation = new CancellationTokenSource();
            initializationTask = InitializeWithCleanupAsync(
                contentLoadCancellation.Token);
        }

        private async Task InitializeWithCleanupAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await InitializeAsync(cancellationToken);
            }
            catch
            {
                matchContentScope?.Dispose();
                matchContentScope = null;
                throw;
            }
        }

        private async Task InitializeAsync(
            CancellationToken cancellationToken)
        {
            if (GameSessionContext.IsDedicatedServer)
                dedicatedServer = true;
            FrameSyncDiagnosticsUnityHost.EnsureInitialized(
                dedicatedServer);
            if (GameSessionContext.FlowManagedExternally)
            {
                // External flow ownership is exclusive. Serialized scene
                // defaults must never leak LocalDirect behavior into UOS.
                enableOnlineApplicationFlow =
                    GameSessionContext.FlowMode ==
                    FrameFlowMode.UosOnline;
                localDevelopmentNetworkFlow =
                    GameSessionContext.FlowMode ==
                    FrameFlowMode.LocalDirect;
                autoApplyLocalFixturePayload = false;
            }
            else
            {
                // Legacy unmanaged path (scene loads GameScene directly):
                // honor the -onlineFlow/-localFlow command-line override.
                enableOnlineApplicationFlow =
                    UosApplicationConfig.IsOnlineFlowRequested(
                        enableOnlineApplicationFlow);
            }
            PrimeExternalLoadingPresentation();
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();
            if (config.PrefabTable.Partitions.Count > 0)
            {
                MatchContentSelection selection =
                    ResolveMatchContentSelection(
                        config.PrefabTable);
                AddressableMatchContentScope loadedScope =
                    await AddressableMatchContentService.LoadAsync(
                        config.PrefabTable,
                        selection,
                        cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (isDestroying)
                        throw new OperationCanceledException(
                            cancellationToken);
                    config = config.WithPrefabTable(
                        loadedScope.PrefabTable);
                    deterministicMapConfig = loadedScope.MapConfig;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (isDestroying)
                        throw new OperationCanceledException(
                            cancellationToken);
                    matchContentScope = loadedScope;
                    loadedScope = null;
                }
                finally
                {
                    loadedScope?.Dispose();
                }
            }
            ResolveMapPathfindingAuthoring(config.PrefabTable);
            bakedConfig = config;
            Debug.Log(
                $"[FrameSyncConfig] tickRate={config.TickRate} " +
                $"maxPredictionLead={config.MaxPredictionLeadTicks} " +
                $"maxTicksPerFrame={config.MaxLogicTicksPerUnityFrame} " +
                $"launchDelay={config.LaunchDelayMilliseconds}ms " +
                $"flow={GameSessionContext.FlowMode} " +
                $"online={enableOnlineApplicationFlow} " +
                $"localDirect={localDevelopmentNetworkFlow}");
            EnsureAudioListener();
            LocalVersions = new FrameSyncVersionHandshake(
                config.GameplayDataVersion,
                config.MapDataVersion,
                config.GlobalPrefabTableVersion,
                config.CommandSchemaVersion,
                (uint)GameplaySnapshot.CurrentSchemaVersion);
            if (matchContentScope == null && unitRuntimeCatalog == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires UnitRuntimeCatalogAsset.");
            BakedUnitRuntimeCatalog unitCatalog =
                matchContentScope != null
                    ? UnitRuntimeCatalogAsset.BakeCombinedOrThrow(
                        matchContentScope.UnitCatalogs,
                        config.PrefabTable,
                        config.TickRate)
                    : unitRuntimeCatalog.BakeOrThrow(
                        config.PrefabTable,
                        config.TickRate);
            if (unitCatalog.DisposePolicies == null)
                throw new InvalidOperationException(
                    $"{nameof(UnitRuntimeCatalogAsset)} requires a UnitDisposePolicyTable.");
            if (matchContentScope == null && abilityRuntimeCatalog == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires AbilityRuntimeCatalogAsset.");
            AbilityDefinitionRegistry abilityDefinitions =
                matchContentScope != null
                    ? AbilityRuntimeCatalogAsset.BakeCombinedOrThrow(
                        matchContentScope.AbilityCatalogs,
                        config.TickRate)
                    : abilityRuntimeCatalog.BakeOrThrow(
                        config.TickRate);
            MaxLogicTicksPerUnityFrame = config.MaxLogicTicksPerUnityFrame;
            logicDeltaSeconds = 1d / config.TickRate;
            logicAccumulatorMillisecondRateUnits = 0L;
            recoveryAccumulatorMillisecondRateUnits = 0L;
            lastUnityUpdateMonotonicMilliseconds =
                GetUnityMonotonicMilliseconds();

            PhysicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = config.UnitGridCellSize,
                },
            };
            UnitWorld = new UnitWorld
            {
                PhysicsWorld = PhysicsWorld,
                GlobalPrefabTable = config.PrefabTable,
                UnitPrototypeTable = unitCatalog.UnitPrototypes,
                DisposePolicyTable = unitCatalog.DisposePolicies,
                StatDefinitionTable = unitCatalog.StatDefinitions,
                EquipmentDatabase =
                    matchContentScope != null
                        ? matchContentScope.EquipmentCatalog.BakeOrThrow(
                            config.TickRate)
                        : equipmentCatalog != null
                            ? equipmentCatalog.BakeOrThrow(
                            config.TickRate)
                            : new EquipmentDatabase(),
                AbilityDefinitions = abilityDefinitions,
                BuffDefinitions = new BuffDefinitionRegistry(),
                CrowdControlDefinitions =
                    new CrowdControlDefinitionRegistry(),
                StatGrowthC = config.StatGrowthC,
                StatGrowthD = config.StatGrowthD,
                MoveSpeedToLogicVelocityScale =
                    config.MoveSpeedToLogicVelocityScale,
                StatDistanceToLogicDistanceScale =
                    config.MoveSpeedToLogicVelocityScale,
                TickRate = config.TickRate,
                AttackSequenceResetIntervalTicks =
                    config.AttackSequenceResetIntervalTicks,
                RangedAttackRangeThreshold =
                    config.RangedAttackRangeThreshold,
            };
            if (matchContentScope != null)
            {
                for (int i = 0;
                     i < matchContentScope.BuffCatalogs.Count;
                     i++)
                    matchContentScope.BuffCatalogs[i].RegisterAll(
                        UnitWorld.BuffDefinitions,
                        config.TickRate);
            }
            else if (buffCatalog != null)
            {
                buffCatalog.RegisterAll(
                    UnitWorld.BuffDefinitions,
                    config.TickRate);
            }
            CrowdControlCatalogAsset loadedCrowdControlCatalog =
                matchContentScope != null
                    ? matchContentScope.CrowdControlCatalog
                    : crowdControlCatalog;
            if (loadedCrowdControlCatalog != null)
            {
                loadedCrowdControlCatalog.RegisterAll(
                    UnitWorld.CrowdControlDefinitions);
            }
            if (deterministicMapConfig != null)
            {
                bakedMap =
                    deterministicMapConfig.BakeOrThrow();
                if (bakedMap.MapDataVersion !=
                    config.MapDataVersion)
                    throw new InvalidOperationException(
                        "Deterministic map version does not match GlobalGameplayData.");
                UnitWorld.PathGrid =
                    bakedMap.CreatePathGrid();
                UnitWorld.FlowFieldRegistry =
                    BuildFlowFieldRegistry(
                        UnitWorld.PathGrid);
            }
            UnitWorld.MovementCollisionResolver =
                new PhysicsCollisionResolver(
                    PhysicsWorld,
                    UnitWorld.PathGrid);
            Runtime = new FrameSyncGameRuntime(UnitWorld, PhysicsWorld, config);
            ConfigureOptionalApplicationFlow();
            InitializeNonHeroTopology(config);
            if (matchContentScope != null)
            {
                Runtime.TickPipeline.ProjectileWorld.DefRegistry =
                    ProjectileRuntimeCatalogAsset.BakeCombinedOrThrow(
                        matchContentScope.ProjectileCatalogs,
                        config.PrefabTable,
                        config.TickRate);
            }
            else if (projectileRuntimeCatalog != null)
            {
                Runtime.TickPipeline.ProjectileWorld.DefRegistry =
                    projectileRuntimeCatalog.BakeOrThrow(
                        config.PrefabTable,
                        config.TickRate);
            }
            QueueInitialUnitSpawns();

            // Create MatchFlowStateMachine (0090)
            MatchFlow = new MatchFlowStateMachine(Runtime.MatchRule);

            if (!dedicatedServer)
            {
                ConfigureClientPresentation();
            }

            // Wire presentation dispatch and UI snapshot after each tick
            Runtime.TickPipeline.TickCompleted += (_, _, _) =>
            {
                MatchFlow.ObserveTick();

                // Show result screen when match finishes (0092)
                if (MatchFlow.HasFinished &&
                    uiManager != null &&
                    !uiManager.IsOpen(UIPageId.Result))
                {
                    uiManager.ShowPage(
                        UIPageId.Result);
                    GameFlowLuaBridge.LastMatchVictory =
                        () => MatchFlow.Result
                            .WinningTeamId.Value != 0;
                }

                presentationDispatcher?.DispatchCurrentFrame();
                PushUiSnapshot();
            };

            if (dedicatedServer && playerInputController != null)
            {
                if (!GameSessionContext
                        .FlowManagedExternally)
                    throw new InvalidOperationException(
                        "Dedicated Server bootstrap must not reference " +
                        "PlayerInputController.");
                Debug.LogWarning(
                    "Shared GameScene references PlayerInputController but " +
                    "this process is the Dedicated Server; input is ignored.");
            }

            // Wire presentation event handlers
            if (presentationDispatcher != null)
            {
                if (vfxEventHandler != null)
                    presentationDispatcher.RegisterVfxHandler(vfxEventHandler);
                if (attackSfxHandler != null) presentationDispatcher.RegisterSfxHandler(attackSfxHandler);
                if (hitReactionPresenter != null) presentationDispatcher.RegisterVfxHandler(hitReactionPresenter);
                if (deathPresenter != null)
                {
                    presentationDispatcher.RegisterVfxHandler(deathPresenter);
                    presentationDispatcher.RegisterSfxHandler(deathPresenter);
                }
            }

            // Blight stack marks (full-flow equivalent of the hero-test
            // scene wiring): persistent per-stack markers driven by the
            // deterministic BuffHandler. Client-presentation only.
            if (!dedicatedServer &&
                UnitWorld != null)
            {
                bool includesVarus =
                    matchContentScope == null ||
                    matchContentScope.Selection
                        .ContainsHeroConfigId(1001);
                if (includesVarus)
                {
                    var blightMarks =
                        GetComponent<
                            FrameSyncMoba.FrameSync
                                .BlightStackMarkPresenter>();
                    if (blightMarks == null)
                    {
                        blightMarks =
                            gameObject.AddComponent<
                                FrameSyncMoba.FrameSync
                                    .BlightStackMarkPresenter>();
                    }
                    blightMarks.InitializeAddressable(
                        "vfx/4102",
                        () => UnitWorld.GetAllUnits());
                }

                var verticalMotion =
                    GetComponent<
                        CrowdControlVerticalMotionPresenter>();
                if (verticalMotion == null)
                {
                    verticalMotion = gameObject.AddComponent<
                        CrowdControlVerticalMotionPresenter>();
                }
                verticalMotion.Initialize(
                    () => UnitWorld.GetAllUnits(),
                    () => Runtime.CurrentTick,
                    (float)(1d / logicDeltaSeconds));
            }

            if (autoApplyLocalFixturePayload &&
                !UsesNetworkSimulation &&
                frozenInitialSpawns.Count > 0)
            {
                GameStartConfig fixtureConfig =
                    CreateFixtureGameStartConfig();
                GameBootstrapPayload payload =
                    BuildAuthoritativeBootstrapPayload(
                        fixtureConfig);
                ApplyGameBootstrapPayload(payload);
            }

            RegisterExternalFlowSession();
        }

        private MatchContentSelection ResolveMatchContentSelection(
            GlobalPrefabTable rootTable)
        {
            var heroIds = new List<int>();
            if (GameSessionContext.SelectedHeroConfigIds != null)
                heroIds.AddRange(
                    GameSessionContext.SelectedHeroConfigIds);
            if (heroIds.Count == 0)
            {
                for (int i = 0; i < initialUnitSpawns.Count; i++)
                    if (initialUnitSpawns[i].PlayerControlled)
                        heroIds.Add(
                            initialUnitSpawns[i].UnitPrototypeId);
            }
            int mapConfigId =
                GameSessionContext.SelectedMapConfigId;
            if (mapConfigId <= 0)
            {
                for (int i = 0; i < rootTable.Partitions.Count; i++)
                {
                    GlobalPrefabPartitionReference partition =
                        rootTable.Partitions[i];
                    if (partition.PartitionKind !=
                        GlobalPrefabPartitionKind.Map)
                        continue;
                    if (mapConfigId != 0)
                        throw new InvalidOperationException(
                            "Standalone GameScene requires an explicit map when multiple map partitions exist.");
                    mapConfigId = partition.OwnerConfigId;
                }
            }
            if (mapConfigId <= 0)
                throw new InvalidOperationException(
                    "GameScene requires a selected MapConfigId before loading match content.");
            return new MatchContentSelection(
                mapConfigId,
                heroIds);
        }

        /// <summary>
        /// GameScene camera has no AudioListener; without one the pooled 3D
        /// AudioSources are silent. Add a listener on the gameplay camera so
        /// attack / ability SFX are heard on clients. Dedicated servers have
        /// no camera, so this is a no-op there.
        /// </summary>
        private void EnsureAudioListener()
        {
            if (FindObjectOfType<AudioListener>() != null)
            {
                return;
            }
            Camera camera =
                gameplayCamera != null
                    ? gameplayCamera
                    : Camera.main;
            if (camera != null)
            {
                camera.gameObject
                    .AddComponent<AudioListener>();
            }
        }

        /// <summary>
        /// Player-centric 3D audio: move the AudioListener onto the locally
        /// controlled unit once it is bound, so attack / ability SFX played
        /// at the hero socket are clearly audible. Removes the camera
        /// fallback listener added at Awake (scenes carry no listener).
        /// </summary>
        private void AttachListenerToLocalUnit(
            UnitType unit)
        {
            if (unit == null)
            {
                return;
            }
            AudioListener existing =
                FindObjectOfType<AudioListener>();
            if (existing != null &&
                existing.transform ==
                    unit.transform)
            {
                return;
            }
            if (existing != null)
            {
                Destroy(existing);
            }
            unit.gameObject
                .AddComponent<AudioListener>();
        }

        /// <summary>
        /// Registers this GameScene runtime into the cross-scene session and
        /// handles hand-off state that arrived before GameScene loaded.
        /// </summary>
        private void RegisterExternalFlowSession()
        {
            if (!UsesNetworkSimulation)
                return;
            GameSessionContext.Bootstrap = this;
            if (dedicatedServer &&
                GameSessionContext.LobbyBridge != null)
            {
                GameSessionContext.LobbyBridge.StartScheduled -=
                    OnLobbyStartScheduled;
                GameSessionContext.LobbyBridge.StartScheduled +=
                    OnLobbyStartScheduled;
                GameSessionContext.LobbyBridge
                    .AllClientsBootstrapApplied -=
                    OnAllClientsBootstrapApplied;
                GameSessionContext.LobbyBridge
                    .AllClientsBootstrapApplied +=
                    OnAllClientsBootstrapApplied;
            }
            if (!dedicatedServer &&
                GameSessionContext.ReceivedClientPayload.HasValue &&
                !matchBootstrapApplied)
            {
                ApplyGameBootstrapPayload(
                    GameSessionContext.ReceivedClientPayload.Value);
            }
            if (!dedicatedServer &&
                GameSessionContext.ReceivedClientLaunchCommit.HasValue &&
                matchBootstrapApplied &&
                !launchCommitApplied)
            {
                ApplyMatchLaunchCommit(
                    GameSessionContext
                        .ReceivedClientLaunchCommit.Value);
            }
        }

        private void InitializeNonHeroTopology(
            in BakedGlobalGameplayData config)
        {
            LaneAuthoring[] authoredLanes =
                flowFieldAuthoring != null &&
                flowFieldAuthoring.Lanes.Length > 0
                    ? flowFieldAuthoring.Lanes
                    : laneAuthoring ??
                      Array.Empty<LaneAuthoring>();
            var lanes =
                new LaneRuntimeData[
                    authoredLanes.Length];
            for (int i = 0;
                 i < authoredLanes.Length;
                 i++)
            {
                if (authoredLanes[i] == null)
                    throw new InvalidOperationException(
                        $"LaneAuthoring entry {i} is missing.");
                lanes[i] =
                    authoredLanes[i].BakeOrThrow();
            }
            Array.Sort(
                lanes,
                (left, right) =>
                    left.LaneId.CompareTo(
                        right.LaneId));
            for (int i = 1;
                 i < lanes.Length;
                 i++)
                if (lanes[i - 1].LaneId ==
                    lanes[i].LaneId)
                    throw new InvalidOperationException(
                        $"Duplicate LaneId {lanes[i].LaneId}.");
            ValidateLaneFlowFields(lanes);
            nonHeroLanes = lanes;

            BakedMinionWaveConfig schedule =
                minionWaveConfig != null
                    ? BakedMinionWaveConfig
                        .FromConfig(
                            minionWaveConfig,
                            config.TickRate)
                    : config.MinionWaveConfig;
            Runtime.ConfigureNonHeroTopology(
                schedule,
                lanes);

            JungleCamp[] authoredCamps =
                jungleCamps ??
                Array.Empty<JungleCamp>();
            for (int i = 0;
                 i < authoredCamps.Length;
                 i++)
            {
                if (authoredCamps[i] == null)
                    throw new InvalidOperationException(
                        $"JungleCamp entry {i} is missing.");
                authoredCamps[i]
                    .InitializeForMatch(UnitWorld);
            }
        }

        private FlowFieldRegistry
            BuildFlowFieldRegistry(
                PathGridMap2D pathGrid)
        {
            FlowFieldBakeAsset[] assets =
                flowFieldAuthoring != null
                    ? flowFieldAuthoring.BakedFields
                    : flowFieldAssets ??
                      Array.Empty<FlowFieldBakeAsset>();
            if (assets.Length == 0)
            {
                return new FlowFieldRegistry();
            }
            var sorted =
                (FlowFieldBakeAsset[])assets.Clone();
            Array.Sort(
                sorted,
                (left, right) =>
                {
                    if (left == null)
                        return right == null ? 0 : -1;
                    if (right == null)
                        return 1;
                    return left.Key.Packed.CompareTo(
                        right.Key.Packed);
                });
            var registry =
                new FlowFieldRegistry();
            int expectedCellCount =
                checked(
                    pathGrid.Width *
                    pathGrid.Height);
            for (int i = 0;
                 i < sorted.Length;
                 i++)
            {
                FlowFieldBakeAsset asset =
                    sorted[i];
                if (asset == null ||
                    !asset.IsValid ||
                    asset.Field.Width !=
                        pathGrid.Width ||
                    asset.Field.Height !=
                        pathGrid.Height ||
                    asset.Field.CellCount !=
                        expectedCellCount)
                {
                    throw new InvalidOperationException(
                        $"Flow-field asset {i} is missing or does not match the deterministic map grid.");
                }
                registry.Register(asset.Field);
            }
            return registry;
        }

        private void ResolveMapPathfindingAuthoring(
            GlobalPrefabTable prefabTable)
        {
            bool explicitlyWired =
                flowFieldAuthoring != null;
            if (flowFieldAuthoring == null)
            {
                GameObject mapPrefab = null;
                prefabTable?.TryGetPrefab(
                        PrefabKind.Misc,
                        5001,
                        out mapPrefab);
                if (mapPrefab != null)
                    flowFieldAuthoring =
                        mapPrefab.GetComponent<
                            FlowFieldSceneAuthoring>();
            }
            if (flowFieldAuthoring == null)
                return;
            DeterministicMapConfig ownedConfig =
                flowFieldAuthoring.MapConfig;
            if (ownedConfig == null)
                throw new InvalidOperationException(
                    "Map FlowFieldSceneAuthoring requires a deterministic map config.");
            if (!explicitlyWired)
            {
                // Auto-loaded Map prefab must not override an explicitly
                // configured (e.g. neutral smoke) map; flow fields are only
                // consumed when lane topology exists.
                if (deterministicMapConfig == null)
                    deterministicMapConfig =
                        ownedConfig;
                return;
            }
            if (deterministicMapConfig != null &&
                deterministicMapConfig != ownedConfig)
                throw new InvalidOperationException(
                    "GameBootstrap and the Map prefab reference different deterministic map configs.");
            deterministicMapConfig = ownedConfig;
        }

        private void ValidateLaneFlowFields(
            LaneRuntimeData[] lanes)
        {
            if (lanes.Length == 0)
                return;
            if (UnitWorld.PathGrid == null ||
                UnitWorld.FlowFieldRegistry == null)
            {
                throw new InvalidOperationException(
                    "Lane topology requires a deterministic path grid and flow-field registry.");
            }
            var teamIds = new List<byte>();
            for (int laneIndex = 0;
                 laneIndex < lanes.Length;
                 laneIndex++)
            {
                LaneTeamSpawnData[] spawns =
                    lanes[laneIndex].TeamSpawns;
                for (int spawnIndex = 0;
                     spawnIndex < spawns.Length;
                     spawnIndex++)
                {
                    byte teamId =
                        spawns[spawnIndex]
                            .TeamId.Value;
                    if (!teamIds.Contains(teamId))
                        teamIds.Add(teamId);
                }
            }
            teamIds.Sort();
            for (int teamIndex = 0;
                 teamIndex < teamIds.Count;
                 teamIndex++)
            {
                for (RadiusClass radiusClass =
                         RadiusClass.Small;
                     radiusClass <=
                         RadiusClass.Large;
                     radiusClass++)
                {
                    var key = new FlowFieldKey(
                        teamIds[teamIndex],
                        radiusClass);
                    if (!UnitWorld.FlowFieldRegistry
                        .TryGet(key, out _))
                    {
                        throw new InvalidOperationException(
                            $"Lane topology requires flow field Team {teamIds[teamIndex]}, Radius {radiusClass}.");
                    }
                }
            }
        }

        private void Update()
        {
            long nowMilliseconds =
                GetUnityMonotonicMilliseconds();
            long elapsedMilliseconds =
                lastUnityUpdateMonotonicMilliseconds < 0L
                    ? 0L
                    : Math.Max(
                        0L,
                        nowMilliseconds -
                        lastUnityUpdateMonotonicMilliseconds);
            lastUnityUpdateMonotonicMilliseconds =
                nowMilliseconds;
            if (Runtime == null)
                return;
            if (UsesNetworkSimulation &&
                !dedicatedServer &&
                IsClientGameplayActive() &&
                frameSyncNetworkBridge != null &&
                frameSyncNetworkBridge
                    .IsConnectedClient)
            {
                frameSyncNetworkBridge.SendLocalCommands();
                frameSyncNetworkBridge.TickPresentationPing(
                    nowMilliseconds);
                recoveryAccumulatorMillisecondRateUnits =
                    checked(
                        recoveryAccumulatorMillisecondRateUnits +
                        elapsedMilliseconds * bakedConfig.TickRate);
                while (recoveryAccumulatorMillisecondRateUnits >=
                       DeterministicTimeConversion
                           .MillisecondsPerSecond)
                {
                    recoveryAccumulatorMillisecondRateUnits -=
                        DeterministicTimeConversion
                            .MillisecondsPerSecond;
                    recoveryControlTick++;
                }
                frameSyncNetworkBridge.TickRecovery(
                    recoveryControlTick);
            }
            if (driveSimulationFromUnityUpdate)
                AdvanceSimulationByElapsedMilliseconds(
                    elapsedMilliseconds);
            if (hudLaunchPending &&
                Runtime != null &&
                !IsEndpointLaunchTimeReached())
            {
                if (!launchCommitApplied)
                {
                    gameLoadProgress = 0.9f;
                    gameLoadStatus =
                        "Waiting for all players";
                }
                else
                {
                    long loadElapsedMilliseconds = Math.Max(
                        0L,
                        RequireLaunchClock()
                            .MonotonicTimeMilliseconds -
                        loadWaitStartMonotonicMilliseconds);
                    float waitProgress = Mathf.Clamp01(
                        (float)((double)loadElapsedMilliseconds /
                        loadWaitDurationMilliseconds));
                    gameLoadProgress =
                        Mathf.Lerp(0.9f, 0.99f, waitProgress);
                    gameLoadStatus =
                        "Synchronizing players";
                }
            }
            if (hudLaunchPending &&
                Runtime != null &&
                IsEndpointLaunchTimeReached() &&
                uiManager != null)
            {
                hudLaunchPending = false;
                gameLoadProgress = 1f;
                gameLoadStatus = "Entering battle";
                uiManager.CloseAll();
                uiManager.ShowPage(UIPageId.HUD);
            }
            RefreshLoadingLua();
            RefreshHudLua();
        }

        private void RefreshLoadingLua()
        {
            if (dedicatedServer ||
                uiManager == null ||
                !uiManager.IsOpen(UIPageId.Load))
                return;
            uiManager.RefreshLuaHost(UIPageId.Load);
        }

        /// <summary>
        /// Targeted Lua HUD refresh while the battle HUD page is open.
        /// Interim per-frame poll until WatchableValue/WatchHook lands
        /// (design v9.1 10.2); UIManager itself never ticks all pages.
        /// </summary>
        private void RefreshHudLua()
        {
            if (dedicatedServer ||
                uiManager == null ||
                !uiManager.IsOpen(UIPageId.HUD))
                return;
            uiManager.RefreshLuaHost(UIPageId.HUD);
            if (uiManager.IsOpen(UIPageId.Shop))
                uiManager.RefreshLuaHost(UIPageId.Shop);
        }

        private async void Start()
        {
            try
            {
                await initializationTask;
                if (!UsesNetworkSimulation) return;
                Debug.Log(
                    $"[GB] Start role={dedicatedServer} managed=" +
                    $"{GameSessionContext.FlowManagedExternally} " +
                    $"mode={GameSessionContext.FlowMode}");
                BindFrameSyncNetworkRuntime();
                if (GameSessionContext.FlowManagedExternally)
                {
                    Debug.Log(
                        "[GB] External flow start: " +
                        (dedicatedServer
                            ? "server"
                            : "client"));
                    HandleExternalFlowStart();
                    return;
                }
                if (localDevelopmentNetworkFlow)
                    return;
                if (dedicatedServer)
                    await ApplicationFlow.DedicatedServer.BootAsync();
                else
                {
                    await ApplicationFlow.Client
                        .InitializeAccountAsync(
                            Environment.GetCommandLineArgs());
                    ClientAccountSession session =
                        ApplicationFlow.Client
                            .AccountSession;
                    GameFlowLuaBridge.AccountDisplayName =
                        session.TestAccountId;
                    uiManager?.RefreshLuaHost(
                        UIPageId.Main);
                }
            }
            catch (Exception exception)
            {
                driveSimulationFromUnityUpdate = false;
                Debug.LogException(exception, this);
            }
        }

        private void HandleExternalFlowStart()
        {
            if (dedicatedServer)
            {
                Debug.Log(
                    "[GB] Server external start: pending=" +
                    GameSessionContext
                        .PendingServerStart.HasValue);
                GameSessionContext.ServerFlow?
                    .EnterLobby();
                if (GameSessionContext
                        .PendingServerStart.HasValue)
                    BuildAndBroadcastServerPayload();
                return;
            }

            Debug.Log(
                "[GB] Client external start: payload=" +
                GameSessionContext
                    .ReceivedClientPayload.HasValue +
                " bridge=" +
                (GameSessionContext.LobbyBridge != null));
            if (GameSessionContext
                    .ReceivedClientPayload.HasValue &&
                !matchBootstrapApplied)
            {
                ApplyGameBootstrapPayload(
                    GameSessionContext
                        .ReceivedClientPayload.Value);
            }
            GameSessionContext.LobbyBridge?
                .SubmitLoadedAndReady();
            Debug.Log(
                "[GB] Client submitted loaded+ready.");
        }

        private void OnLobbyStartScheduled(
            GameStartConfig config)
        {
            if (GameSessionContext
                    .PendingServerStart.HasValue &&
                !matchBootstrapApplied)
                BuildAndBroadcastServerPayload();
        }

        /// <summary>
        /// Server-side GameScene hand-off: build the authoritative snapshot,
        /// apply it locally and broadcast it. Simulation remains blocked until
        /// every frozen client reports BootstrapApplied.
        /// </summary>
        private void BuildAndBroadcastServerPayload()
        {
            if (!GameSessionContext
                    .PendingServerStart.HasValue)
                return;
            GameStartConfig config =
                GameSessionContext
                    .PendingServerStart.Value;
            GameBootstrapPayload payload =
                BuildAuthoritativeBootstrapPayload(
                    config);
            ApplyGameBootstrapPayload(payload);
            LobbyNetworkBridge bridge =
                GameSessionContext.LobbyBridge ??
                throw new InvalidOperationException(
                    "Server bootstrap requires the persistent LobbyNetworkBridge.");
            GameSessionContext.ServerFlow?
                .BeginLoadingBarrier();
            bridge.BroadcastBootstrap(payload);
            Debug.Log(
                $"[GameBootstrap] Server applied and broadcast payload for " +
                $"match '{config.MatchId}' at StartTick {config.StartTick}; " +
                "simulation is waiting for BootstrapApplied.");
        }

        private void OnAllClientsBootstrapApplied()
        {
            if (!dedicatedServer ||
                !matchBootstrapApplied ||
                !activeGameStartConfig.HasValue)
                throw new InvalidOperationException(
                    "BootstrapApplied barrier completed without an active server bootstrap.");
            GameStartConfig config =
                activeGameStartConfig.Value;
            var commit = new MatchLaunchCommit(
                config.MatchId,
                config.StartTick,
                checked(
                    RequireLaunchClock()
                        .SynchronizedServerTimeMilliseconds +
                    bakedConfig.LaunchDelayMilliseconds));
            ApplyMatchLaunchCommit(commit);
            GameSessionContext.LobbyBridge?
                .BroadcastLaunchCommit(commit);
        }

        public void BindFrameSyncNetworkRuntime()
        {
            if (!UsesNetworkSimulation)
                throw new InvalidOperationException(
                    "GameBootstrap is not configured for network simulation.");
            if (frameSyncNetworkBridge == null)
                throw new InvalidOperationException(
                    "Network simulation requires FrameSyncNetworkBridge.");
            if (frameSyncNetworkBridge.IsBound)
                return;
            if (dedicatedServer)
                frameSyncNetworkBridge.Bind(
                    Runtime,
                    AuthorizeNetworkCommand);
            else
                frameSyncNetworkBridge.Bind(
                    Runtime);
        }

        private void ConfigureOptionalApplicationFlow()
        {
            if (!UsesNetworkSimulation) return;
            if (networkManager == null)
                networkManager =
                    FindObjectOfType<NetworkManager>(true);
            if (frameSyncNetworkBridge == null)
                frameSyncNetworkBridge =
                    GetComponent<FrameSyncNetworkBridge>();
            if (frameSyncNetworkBridge == null)
                frameSyncNetworkBridge =
                    FindObjectOfType<FrameSyncNetworkBridge>(true);
            if (frameSyncNetworkBridge == null)
                throw new InvalidOperationException(
                    "Network simulation requires FrameSyncNetworkBridge.");
            if (networkManager == null)
                throw new InvalidOperationException(
                    "Network simulation requires NetworkManager for synchronized launch time.");
            launchClock ??=
                new NgoFrameSyncLaunchClock(networkManager);
            frameSyncNetworkBridge.MatchResultReady +=
                OnMatchResultReady;

            if (GameSessionContext.FlowManagedExternally)
            {
                if (GameSessionContext.FlowMode ==
                    FrameFlowMode.UosOnline)
                {
                    if (dedicatedServer)
                        ApplicationFlow =
                            new GameApplicationFlowManager(
                                GameSessionContext.ServerFlow);
                    else
                        ApplicationFlow =
                            new GameApplicationFlowManager(
                                GameSessionContext.ClientFlow);
                }
                return;
            }
            if (networkManager == null)
                throw new InvalidOperationException(
                    "Online application flow requires NetworkManager.");
            if (localDevelopmentNetworkFlow)
                return;

            var ngo = new NgoConnectionService(networkManager);
            if (dedicatedServer)
            {
                ApplicationFlow =
                    new GameApplicationFlowManager(
                        new DedicatedServerApplicationFlow(
                            new UosDedicatedServerPlatform(),
                            ngo));
            }
            else
            {
                string configId =
                    UosApplicationConfig
                        .ResolveMatchmakingConfigId();
                if (string.IsNullOrWhiteSpace(
                        configId))
                    throw new InvalidOperationException(
                        "Client online flow requires a UOS Matchmaking " +
                        "config ID. Configure it in the UOS Launcher " +
                        "environment settings or pass " +
                        $"{UosApplicationConfig.MatchmakingConfigIdArg}" +
                        "=<id> on the command line.");
                ApplicationFlow =
                    new GameApplicationFlowManager(
                        new ClientApplicationFlow(
                            new TestAccountBootstrapService(
                                new PlayerPrefsTestAccountPersistence()),
                            new UosClientSession(),
                            new UosMatchmakingApplicationClient(
                                configId,
                                UosApplicationConfig
                                    .ResolveRegionId()),
                            ngo));
            }
        }

        public void ApplyGameStartConfig(
            in GameStartConfig config)
        {
            config.ValidateOrThrow();
            throw new InvalidOperationException(
                "GameStartConfig alone is insufficient. Apply the complete GameBootstrapPayload.");
        }

        public GameBootstrapPayload
            BuildAuthoritativeBootstrapPayload(
                in GameStartConfig config)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "GameBootstrap must initialize before building a payload.");
            if (matchBootstrapApplied ||
                Runtime.CurrentTick != 0)
                throw new InvalidOperationException(
                    "A bootstrap payload can only be built before the match starts.");
            config.ValidateOrThrow();
            ValidateLoadedContentSelection(config);
            if (config.GameplayDataVersion !=
                LocalVersions.GameplayDataVersion)
                throw new DeterministicSimulationException(
                    "GameStartConfig GameplayDataVersion does not match local data.");
            if (bakedMap != null &&
                config.MapConfigId !=
                    bakedMap.MapConfigId)
                throw new DeterministicSimulationException(
                    "GameStartConfig MapConfigId does not match the selected map.");

            Runtime.ConfigureMatchStart(
                config.StartTick,
                config.InitialRandomSeed,
                config.GameStartPlayerCount,
                bakedConfig.InitialEarnedGold);
            BindSelectedHeroesToPlayerSpawns(
                in config);
            UnitUid[] spawned =
                Runtime.MaterializeInitialSpawnsForBootstrap(
                    config.StartTick);
            ConfigureInitialAIControllers(spawned);
            PlayerSlotUnitMapping[] mappings =
                BuildPlayerSlotMappings(
                    config,
                    spawned);
            Runtime.ConfigurePlayerSlotMappings(
                mappings);
            Runtime.MatchRule.BeginCountdown(
                config.StartTick,
                bakedConfig.CountdownTicks);
            GameplaySnapshot initialSnapshot =
                Runtime.TickPipeline
                    .CaptureAggregateSnapshot();
            return new GameBootstrapPayload(
                config,
                LocalVersions,
                initialSnapshot,
                config.StartTick,
                config.StartTick,
                config.InitialRandomSeed,
                mappings);
        }

        public void ApplyGameBootstrapPayload(
            in GameBootstrapPayload payload)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "GameBootstrap must initialize before applying a payload.");
            if (matchBootstrapApplied)
                throw new InvalidOperationException(
                    "The match bootstrap payload was already applied.");
            LocalVersions.RequireExactMatch(
                payload.Versions);
            payload.GameStartConfig.ValidateOrThrow();
            ValidateLoadedContentSelection(
                payload.GameStartConfig);
            Runtime.ConfigureMatchStart(
                payload.StartTick,
                payload.InitialRandomSeed,
                payload.GameStartConfig.GameStartPlayerCount,
                bakedConfig.InitialEarnedGold);
            matchStartTick = payload.StartTick;
            launchScheduleLogged = false;
            launchServerTimeMilliseconds = 0;
            launchPacingOriginMonotonicMilliseconds = -1;
            launchCommitApplied =
                !UsesNetworkSimulation;
            gameLoadProgress = 0.9f;
            gameLoadStatus =
                UsesNetworkSimulation
                    ? "Waiting for all players"
                    : "Ready";
            Runtime.ConfigurePlayerSlotMappings(
                payload.PlayerSlotMappings);
            Runtime.RestoreInitialSnapshot(
                payload.InitialGameplaySnapshot,
                payload.InitialSnapshotTick,
                dedicatedServer
                    ? ExecutionMode.ServerAuthority
                    : ExecutionMode.ClientPrediction);
            activeGameStartConfig =
                payload.GameStartConfig;
            frameSyncNetworkBridge?.SetMatchId(
                payload.GameStartConfig.MatchId);
            FrameSyncDiagnosticsUnityHost.SetContext(
                payload.GameStartConfig.MatchId,
                LocalPlayerSlot);
            matchBootstrapApplied = true;
            if (!dedicatedServer)
            {
                TryBindConfiguredLocalPlayer();
                if (UsesNetworkSimulation &&
                    !IsLocalPlayerBound)
                    throw new DeterministicSimulationException(
                        "Client must bind its controlled unit before BootstrapApplied.");
                if (uiManager != null &&
                    GameSessionContext.FlowManagedExternally)
                {
                    // Keep Loading visible through the BootstrapApplied barrier
                    // and the later wall-clock LaunchCommit.
                    hudLaunchPending = true;
                }
                if (UsesNetworkSimulation &&
                    GameSessionContext.FlowManagedExternally)
                    GameSessionContext.LobbyBridge?
                        .SubmitBootstrapApplied(payload);
            }
            Debug.Log(
                $"[BootstrapApplied] role=" +
                $"{(dedicatedServer ? "server" : "client")} " +
                $"match='{payload.GameStartConfig.MatchId}' " +
                $"startTick={payload.StartTick} " +
                $"localBound={IsLocalPlayerBound}; waiting for LaunchCommit.");
        }

        private void ValidateLoadedContentSelection(
            in GameStartConfig config)
        {
            if (matchContentScope == null)
                return;
            MatchContentSelection authoritative =
                MatchContentSelection.FromGameStartConfig(config);
            if (!matchContentScope.Selection.HasSameContent(
                    authoritative))
                throw new InvalidOperationException(
                    $"Loaded match content ({matchContentScope.Selection}) does not match " +
                    $"the authoritative bootstrap ({authoritative}).");
        }

        public void ApplyMatchLaunchCommit(
            in MatchLaunchCommit commit)
        {
            if (!UsesNetworkSimulation)
                throw new InvalidOperationException(
                    "LaunchCommit is only valid for network simulation.");
            if (!matchBootstrapApplied ||
                !activeGameStartConfig.HasValue)
                throw new InvalidOperationException(
                    "LaunchCommit requires an applied bootstrap.");

            commit.ValidateOrThrow();
            GameStartConfig config =
                activeGameStartConfig.Value;
            if (!string.Equals(
                    commit.MatchId,
                    config.MatchId,
                    StringComparison.Ordinal) ||
                commit.StartTick != config.StartTick)
                throw new DeterministicSimulationException(
                    "LaunchCommit does not match the applied bootstrap.");
            if (launchCommitApplied)
            {
                if (launchServerTimeMilliseconds ==
                    commit.LaunchServerTimeMilliseconds)
                    return;
                throw new DeterministicSimulationException(
                    "A conflicting LaunchCommit was received.");
            }

            launchServerTimeMilliseconds =
                commit.LaunchServerTimeMilliseconds;
            launchCommitApplied = true;
            launchScheduleLogged = false;
            launchPacingOriginMonotonicMilliseconds = -1;
            int clientLeadTicks =
                GetClientLaunchLeadTicks();
            long clientLaunchServerTimeMilliseconds =
                FrameSyncLaunchSchedule
                    .GetClientPredictionLaunchServerTimeMilliseconds(
                        commit.LaunchServerTimeMilliseconds,
                        bakedConfig.TickRate,
                        clientLeadTicks);
            long endpointLaunchServerTimeMilliseconds =
                dedicatedServer
                    ? commit.LaunchServerTimeMilliseconds
                    : clientLaunchServerTimeMilliseconds;
            IFrameSyncLaunchClock clock = RequireLaunchClock();
            long receivedServerTimeMilliseconds =
                clock.SynchronizedServerTimeMilliseconds;
            loadWaitStartMonotonicMilliseconds =
                clock.MonotonicTimeMilliseconds;
            loadWaitDurationMilliseconds = Math.Max(
                1L,
                endpointLaunchServerTimeMilliseconds -
                receivedServerTimeMilliseconds);
            gameLoadStatus = "Synchronizing players";

            if (dedicatedServer)
            {
                DedicatedServerApplicationFlow server =
                    GameSessionContext.ServerFlow;
                if (server != null &&
                    server.State ==
                    DedicatedServerApplicationState.LoadingBarrier)
                    server.StartGameplay();
            }
            else
            {
                ClientApplicationFlow client =
                    GameSessionContext.ClientFlow ??
                    ApplicationFlow?.Client;
                if (client != null)
                {
                    if (client.State ==
                        ClientApplicationState.Lobby)
                        client.BeginLoadingGame();
                    if (client.State ==
                        ClientApplicationState.LoadingGame)
                        client.EnterGame();
                    if (client.State !=
                        ClientApplicationState.InGame)
                        throw new InvalidOperationException(
                            $"LaunchCommit arrived while client flow is {client.State}.");
                }
            }

            GameSessionContext.ReceivedClientLaunchCommit =
                null;
            Debug.Log(
                $"[LaunchCommit] role=" +
                $"{(dedicatedServer ? "server" : "client")} " +
                $"receivedServerMs={receivedServerTimeMilliseconds} " +
                $"serverLaunchMs={commit.LaunchServerTimeMilliseconds} " +
                $"clientLaunchMs={clientLaunchServerTimeMilliseconds} " +
                $"remainingMs=" +
                $"{Math.Max(0L, endpointLaunchServerTimeMilliseconds - receivedServerTimeMilliseconds)} " +
                $"leadTicks={clientLeadTicks} " +
                $"startTick={commit.StartTick}");
        }

        private void ConfigureClientPresentation()
        {
            if (uiManager == null)
                uiManager =
                    FindObjectOfType<UIManager>(true);
            if (uiManager != null)
            {
                uiManager.Initialized -= OnUiManagerInitialized;
                uiManager.Initialized += OnUiManagerInitialized;
                uiManager.Initialize();
                uiManager.CloseAll();
                minimapController ??=
                    uiManager.GetPageComponent<MinimapController>(
                        UIPageId.HUD);
                uiManager.ShowPage(
                    GameSessionContext.FlowManagedExternally
                        ? UIPageId.Load
                        : UIPageId.Main);
            }

            BindGameFlowLuaBridge();
            uiManager?.TryGetPage(
                UIPageId.Select,
                out _);
            uiManager?.RefreshLuaHost(
                UIPageId.Select);
        }

        private void PrimeExternalLoadingPresentation()
        {
            if (dedicatedServer ||
                !GameSessionContext.FlowManagedExternally)
                return;

            gameLoadStatus = "Loading match content";
            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>(true);
            if (uiManager == null)
                return;

            // GameScene's UIManager begins loading its Addressable pages from
            // Awake and has Main as its standalone OpenOnStart fallback. Queue
            // the externally-owned page before the first match-content await
            // so that fallback can never become the rendered handoff page.
            GameFlowLuaBridge.UiManager = uiManager;
            GameFlowLuaBridge.LocalLoadProgress =
                () => gameLoadProgress;
            GameFlowLuaBridge.GetLoadingStatus =
                () => gameLoadStatus;
            uiManager.Initialize();
            uiManager.CloseAll();
            uiManager.ShowPage(UIPageId.Load);
        }

        private void OnUiManagerInitialized()
        {
            if (uiManager == null)
                return;
            minimapController ??=
                uiManager.GetPageComponent<MinimapController>(UIPageId.HUD);
            uiManager.TryGetPage(UIPageId.Select, out _);
            uiManager.RefreshLuaHost(UIPageId.Select);
        }

        private void BindGameFlowLuaBridge()
        {
            GameFlowLuaBridge.UiManager = uiManager;
            FrameSyncMoba.FrameSync.FrameSyncGameRuntime
                .RegisterActiveInstance(Runtime);
            GameFlowLuaBridge.AccountDisplayName =
                GameSessionContext.ClientFlow != null
                    ? GameSessionContext.ClientFlow
                        .AccountSession.TestAccountId
                    : "Player";

            if (!GameSessionContext.FlowManagedExternally)
            {
                GameFlowLuaBridge.CanStartMatchmaking =
                    () => true;
                GameFlowLuaBridge.StartMatchmaking =
                    () =>
                    {
                        uiManager?.ShowPage(
                            UIPageId.Match);
                    };
                GameFlowLuaBridge.CancelMatchmaking =
                    () => uiManager?.ShowPage(
                        UIPageId.Main);
                GameFlowLuaBridge.QuitApplication =
                    () => { };
                GameFlowLuaBridge.IsSearching =
                    () => true;
                GameFlowLuaBridge.MatchElapsedSeconds =
                    () => 0f;
                GameFlowLuaBridge.CanCancelMatchmaking =
                    () => true;

                GameFlowLuaBridge.ChooseHero =
                    heroId =>
                    {
                        if (clientUiActionRouter != null &&
                            clientUiActionRouter.IsBound)
                            clientUiActionRouter
                                .SelectHero(heroId);
                    };
                GameFlowLuaBridge.ConfirmHero =
                    () =>
                    {
                        if (clientUiActionRouter != null &&
                            clientUiActionRouter.IsBound)
                            clientUiActionRouter.SetReady(
                                true);
                    };
                GameFlowLuaBridge.ConfirmedCount =
                    () => 0;
                GameFlowLuaBridge.PlayerCount =
                    () => 2;
                GameFlowLuaBridge.CanConfirmHero =
                    () => true;
                GameFlowLuaBridge.BindHeroSelect(
                    globalGameplayData != null
                        ? globalGameplayData
                            .HeroDisplayTable
                        : null);
                GameFlowLuaBridge.LocalLoadProgress =
                    () => 1f;
                GameFlowLuaBridge.GetLoadingStatus =
                    () => "Ready";
            }
            else
            {
                GameFlowLuaBridge.LocalLoadProgress =
                    () => gameLoadProgress;
                GameFlowLuaBridge.GetLoadingStatus =
                    () => gameLoadStatus;
            }
            GameFlowLuaBridge.IsLocalTeamVictory =
                () =>
                {
                    if (Runtime == null ||
                        Runtime.LocalPlayerSlot < 0)
                        return false;
                    Unit.Unit unit =
                        Runtime.GetLocalControlledUnit();
                    if (unit == null ||
                        MatchFlow == null)
                        return false;
                    int winner =
                        MatchFlow.Result
                            .WinningTeamId.Value;
                    return winner != 0 &&
                        unit.TeamId.Value == winner;
                };
            GameFlowLuaBridge.LastMatchDraw =
                () =>
                    MatchFlow != null &&
                    MatchFlow.Result
                        .WinningTeamId.Value == 0;
            GameFlowLuaBridge.ReturnMainMenu =
                () =>
                {
                    if (GameSessionContext.FlowManagedExternally)
                    {
                        GameSessionContext.LobbyBridge?
                            .Shutdown();
                        GameSessionContext.Bootstrap =
                            null;
                        GameSessionContext
                            .PendingServerStart =
                            null;
                        SceneManager.LoadScene(
                            GameSessionContext
                                .LobbySceneName);
                        return;
                    }
                    if (clientUiActionRouter != null &&
                        clientUiActionRouter.IsBound)
                        clientUiActionRouter
                            .ReturnToMainMenu();
                    uiManager?.ShowPage(
                        UIPageId.Main);
                };

            var equipmentDatabase =
                UnitWorld?.EquipmentDatabase;
            GameFlowLuaBridge.GetShopItemCount =
                () =>
                    equipmentDatabase?.Count ?? 0;
            GameFlowLuaBridge.GetShopItemId =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    return defs != null &&
                        index >= 0 &&
                        index < defs.Count
                            ? defs[index].Id
                            : 0;
                };
            GameFlowLuaBridge.GetShopItemName =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    return defs != null &&
                        index >= 0 &&
                        index < defs.Count
                            ? defs[index].Name ?? ""
                            : "";
                };
            GameFlowLuaBridge.GetShopItemDescription =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    return defs != null &&
                        index >= 0 &&
                        index < defs.Count
                            ? defs[index].Description ?? ""
                            : "";
                };
            GameFlowLuaBridge.GetShopItemIcon =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    return defs != null &&
                        index >= 0 &&
                        index < defs.Count
                            ? ClientSpriteRegistry.Resolve(
                                defs[index].IconAddress)
                            : null;
                };
            GameFlowLuaBridge.GetShopItemPrice =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    if (defs == null ||
                        index < 0 ||
                        index >= defs.Count)
                        return 0;
                    var view =
                        Runtime.LocalEquipmentShopView;
                    return view != null &&
                        Runtime.LocalPlayerSlot >= 0
                            ? view.CalculatePurchasePrice(
                                defs[index].Id)
                            : defs[index].Value;
                };
            GameFlowLuaBridge.GetShopItemNameById =
                equipmentId =>
                {
                    var def =
                        FindEquipmentDefinition(
                            equipmentId);
                    return def?.Name ?? "";
                };
            GameFlowLuaBridge.GetShopItemPriceById =
                equipmentId =>
                {
                    var view =
                        Runtime.LocalEquipmentShopView;
                    if (view != null &&
                        Runtime.LocalPlayerSlot >= 0)
                        return view.CalculatePurchasePrice(
                            equipmentId);
                    return FindEquipmentDefinition(
                            equipmentId)?.Value ??
                        0;
                };
            GameFlowLuaBridge.GetShopItemEffectById =
                equipmentId =>
                {
                    var def =
                        FindEquipmentDefinition(
                            equipmentId);
                    if (def?.Effects == null)
                        return "";
                    var parts =
                        new System.Collections.Generic
                            .List<string>();
                    for (int i = 0;
                         i < def.Effects.Length;
                         i++)
                    {
                        var effect = def.Effects[i];
                        if (effect == null ||
                            string.IsNullOrEmpty(
                                effect.Name))
                            continue;
                        parts.Add(
                            string.IsNullOrEmpty(
                                effect.Description)
                                ? effect.Name
                                : effect.Name +
                                    ": " +
                                    effect.Description);
                    }
                    return string.Join(
                        "; ",
                        parts);
                };
            GameFlowLuaBridge.GetShopItemStatById =
                equipmentId =>
                {
                    var def =
                        FindEquipmentDefinition(
                            equipmentId);
                    if (def?.BakedFixedStats == null)
                        return "";
                    var parts =
                        new System.Collections.Generic
                            .List<string>();
                    for (int i = 0;
                         i < def.BakedFixedStats.Length;
                         i++)
                    {
                        var stat =
                            def.BakedFixedStats[i];
                        parts.Add(
                            $"{stat.Stat} +{(float)stat.Value}");
                    }
                    return string.Join(
                        ", ",
                        parts);
                };
            GameFlowLuaBridge.GetCurrentGold =
                () =>
                    Runtime.LocalEquipmentShopView
                        ?.GetCurrentAvailableGold()
                        ?? 0;
            GameFlowLuaBridge.CanUndo =
                () =>
                    Runtime.LocalPlayerSlot >= 0 &&
                    Runtime.EquipmentShop.CanUndo(
                        Runtime.LocalPlayerSlot,
                        Runtime
                            .LocalEquipmentShopView
                            ?.GetCurrentAvailableGold()
                            ?? 0,
                        out _);
            GameFlowLuaBridge.RequestPurchase =
                equipmentId =>
                {
                    if (Runtime.LocalPlayerSlot < 0)
                    {
                        Debug.LogWarning(
                            $"[ShopRequest] purchase item={equipmentId} " +
                            "rejected reason=LocalPlayerNotBound");
                        GameFlowLuaBridge.GetShopStatus =
                            () => "LocalPlayerNotBound";
                        return;
                    }
                    int playerSlot =
                        Runtime.LocalPlayerSlot;
                    int gold = Runtime.EquipmentShop
                        .GetCurrentAvailableGold(
                            playerSlot);
                    int price = Runtime.EquipmentShop
                        .CalculatePurchasePrice(
                            playerSlot,
                            equipmentId);
                    var check =
                        Runtime.EquipmentShop
                            .RequestPurchase(
                                playerSlot,
                                equipmentId);
                    Debug.Log(
                        $"[ShopRequest] purchase item={equipmentId} " +
                        $"slot={playerSlot} gold={gold} price={price} " +
                        $"allowed={check.Allowed} " +
                        $"reason={check.FailureReason}");
                    GameFlowLuaBridge.GetShopStatus =
                        () =>
                            check.Allowed
                                ? ""
                                : check.FailureReason
                                    .ToString();
                };
            GameFlowLuaBridge.RequestSell =
                slot =>
                {
                    if (Runtime.LocalPlayerSlot < 0)
                    {
                        Debug.LogWarning(
                            $"[ShopRequest] sell inventorySlot={slot} " +
                            "rejected reason=LocalPlayerNotBound");
                        GameFlowLuaBridge.GetShopStatus =
                            () => "LocalPlayerNotBound";
                        return;
                    }
                    int playerSlot =
                        Runtime.LocalPlayerSlot;
                    var check = Runtime.EquipmentShop
                        .RequestSell(
                            playerSlot,
                            slot);
                    Debug.Log(
                        $"[ShopRequest] sell inventorySlot={slot} " +
                        $"slot={playerSlot} allowed={check.Allowed} " +
                        $"reason={check.FailureReason}");
                    GameFlowLuaBridge.GetShopStatus =
                        () =>
                            check.Allowed
                                ? ""
                                : check.FailureReason
                                    .ToString();
                };
            GameFlowLuaBridge.RequestUndo =
                () =>
                {
                    if (Runtime.LocalPlayerSlot < 0)
                    {
                        Debug.LogWarning(
                            "[ShopRequest] undo rejected " +
                            "reason=LocalPlayerNotBound");
                        GameFlowLuaBridge.GetShopStatus =
                            () => "LocalPlayerNotBound";
                        return;
                    }
                    int playerSlot =
                        Runtime.LocalPlayerSlot;
                    var check = Runtime.EquipmentShop
                        .RequestUndo(
                            playerSlot);
                    Debug.Log(
                        $"[ShopRequest] undo slot={playerSlot} " +
                        $"allowed={check.Allowed} " +
                        $"reason={check.FailureReason}");
                    GameFlowLuaBridge.GetShopStatus =
                        () =>
                            check.Allowed
                                ? ""
                                : check.FailureReason
                                    .ToString();
                };

            // ---- HUD read-only data ----

            GameFlowLuaBridge.GetLocalHp =
                () => UIDisplayConvert.ResourceInt(
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler?.CurrentHealth
                        ?? Unity.Mathematics.FixedPoint.fp.zero);
            GameFlowLuaBridge.GetLocalMaxHp =
                () => UIDisplayConvert.ResourceInt(
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler
                        ?.GetStat(StatId.MaxHealth)
                        ?? Unity.Mathematics.FixedPoint.fp.zero);
            GameFlowLuaBridge.GetLocalResource =
                () => UIDisplayConvert.ResourceInt(
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler
                        ?.CurrentCastResource
                        ?? Unity.Mathematics.FixedPoint.fp.zero);
            GameFlowLuaBridge.GetLocalMaxResource =
                () => UIDisplayConvert.ResourceInt(
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler
                        ?.GetStat(StatId.MaxCastResource)
                        ?? Unity.Mathematics.FixedPoint.fp.zero);
            GameFlowLuaBridge.GetLocalLevel =
                () =>
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler?.Level ?? 1;
            GameFlowLuaBridge.GetLocalExp =
                () =>
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler
                        ?.CurrentExperience ?? 0;
            GameFlowLuaBridge.GetLocalNextLevelExp =
                () =>
                    Runtime.GetLocalControlledUnit()
                        ?.StatHandler
                        ?.ExperienceRequiredForNextLevel
                        ?? 100;
            GameFlowLuaBridge.IsExpandStatsHeld =
                () => FrameSyncMoba.PlayerInput
                    .PresentationInputState
                    .ExpandStatsHeld;
            GameFlowLuaBridge.GetCooldownRemaining =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.AbilityHandler != null
                        ? unit.AbilityHandler
                            .GetDisplayCooldownRemainingTicks(
                                (byte)slot,
                                Runtime.CurrentTick)
                        : 0;
                };
            GameFlowLuaBridge.GetCooldownTotal =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.AbilityHandler != null
                        ? unit.AbilityHandler
                            .GetDisplayCooldownTotalTicks(
                                (byte)slot)
                        : 0;
                };
            GameFlowLuaBridge.GetCooldownRemainingSeconds =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    if (unit?.AbilityHandler == null)
                        return 0f;
                    int remaining =
                        unit.AbilityHandler
                            .GetDisplayCooldownRemainingTicks(
                                (byte)slot,
                                Runtime.CurrentTick);
                    return remaining *
                        (float)logicDeltaSeconds;
                };
            GameFlowLuaBridge.GetActiveAbilityId =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.AbilityHandler
                        ?.GetAbilityDef((byte)slot)
                        ?.AbilityId ?? 0;
                };
            GameFlowLuaBridge.GetActiveAbilityIcon =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.AbilityHandler
                        ?.GetActiveRuntime((byte)slot)
                        ?.GetCurrentIconAddress() is string address
                            ? ClientSpriteRegistry.Resolve(address)
                            : null;
                };
            GameFlowLuaBridge.GetLocalPendingSkillPoints =
                () =>
                    Runtime.GetLocalControlledUnit()
                        ?.AbilityHandler
                        ?.PendingSkillPoints ?? 0;
            GameFlowLuaBridge.GetLocalAbilityLevel =
                slot =>
                    Runtime.GetLocalControlledUnit()
                        ?.AbilityHandler
                        ?.GetAbilityLevel((byte)slot)
                        ?? 0;
            GameFlowLuaBridge.GetLocalAbilityIsUltimate =
                slot =>
                    Runtime.GetLocalControlledUnit()
                        ?.AbilityHandler
                        ?.IsUltimateSlot((byte)slot)
                        ?? false;
            GameFlowLuaBridge.CanAllocateLocalSkillPoint =
                slot =>
                    Runtime.GetLocalControlledUnit()
                        ?.AbilityHandler
                        ?.CanAllocateSkillPoint(
                            (byte)slot)
                        ?? false;
            GameFlowLuaBridge.AllocateLocalSkillPoint =
                slot =>
                {
                    var requester =
                        playerInputController
                            ?.CommandRequester;
                    if (requester == null)
                        return;
                    requester
                        .RequestAllocateAbilitySkillPoint(
                            (byte)slot);
                };
            GameFlowLuaBridge.DebugHealLocal =
                () => SubmitDebugCommand(
                    DebugCommandOp.Heal);
            GameFlowLuaBridge.DebugRestoreManaLocal =
                () => SubmitDebugCommand(
                    DebugCommandOp.RestoreMana);
            GameFlowLuaBridge.DebugReviveLocal =
                () => SubmitDebugCommand(
                    DebugCommandOp.Revive);
            GameFlowLuaBridge.DebugLevelUpLocal =
                () => SubmitDebugCommand(
                    DebugCommandOp.LevelUp);
            GameFlowLuaBridge.DebugAddGoldLocal =
                amount => SubmitDebugCommand(
                    DebugCommandOp.AddGold,
                    amount);
            GameFlowLuaBridge.GetPassiveAbilityIcon =
                () =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.AbilityHandler
                        ?.FixedPassive
                        ?.GetCurrentIconAddress() is string address
                            ? ClientSpriteRegistry.Resolve(address)
                            : null;
                };
            GameFlowLuaBridge.GetLocalHeroAvatar =
                () =>
                {
                    if (GameSessionContext
                            .HeroDisplayTable == null ||
                        Runtime == null ||
                        Runtime.LocalPlayerSlot < 0 ||
                        !activeGameStartConfig.HasValue)
                        return null;
                    int heroConfigId =
                        activeGameStartConfig.Value
                            .PlayerSlots[
                                Runtime.LocalPlayerSlot]
                            .HeroConfigId;
                    RuntimeConfig.HeroDisplayTable table =
                        GameSessionContext
                            .HeroDisplayTable;
                    for (int i = 0;
                         i < table.Count;
                         i++)
                    {
                        RuntimeConfig.HeroDisplayEntry
                            entry = table.GetEntry(i);
                        if (entry.UnitPrototypeId ==
                            heroConfigId)
                            return ClientSpriteRegistry.Resolve(
                                entry.AvatarAddress);
                    }
                    return null;
                };
            GameFlowLuaBridge.GetHudGold =
                () =>
                    Runtime.LocalEquipmentShopView
                        ?.GetCurrentAvailableGold()
                        ?? 0;
            // Ping label is enabled only while a network sync session is
            // active; offline/local scenes report -1 so the HUD hides it.
            GameFlowLuaBridge.GetLocalPing =
                () => frameSyncNetworkBridge != null &&
                      frameSyncNetworkBridge.IsConnectedClient
                    ? frameSyncNetworkBridge
                        .LatestPingMilliseconds
                    : -1;
            GameFlowLuaBridge.CloseShop =
                () => uiManager?.HideOverlay(
                    UIPageId.Shop);

            // ---- MatchBar (MatchPart scoreboard, kept per user) ----

            GameFlowLuaBridge.GetGameElapsedSeconds =
                () =>
                {
                    var rule = Runtime.MatchRule;
                    return rule != null &&
                        rule.RunningStartTick >= 0
                            ? (Runtime.CurrentTick -
                                rule.RunningStartTick) *
                                (float)logicDeltaSeconds
                            : 0f;
                };
            GameFlowLuaBridge.GetBlueTeamScore =
                () => TeamScore(0);
            GameFlowLuaBridge.GetRedTeamScore =
                () => TeamScore(1);
            GameFlowLuaBridge.GetLocalCreepScore =
                () => LuaDataCache.Latest.CreepScore;
            GameFlowLuaBridge.GetLocalKills =
                () => LuaDataCache.Latest.Kills;
            GameFlowLuaBridge.GetLocalDeaths =
                () => LuaDataCache.Latest.Deaths;
            GameFlowLuaBridge.GetLocalAssists =
                () => LuaDataCache.Latest.Assists;

            // ---- Expanded stats ----

            GameFlowLuaBridge.GetLocalStatValue =
                statId =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    if (unit?.StatHandler == null)
                        return 0;
                    var value =
                        unit.StatHandler.GetStat(
                            (StatId)statId);
                    switch ((StatId)statId)
                    {
                        case StatId
                            .CriticalStrikeChance:
                        case StatId
                            .ArmorPenetrationRatio:
                        case StatId
                            .MagicPenetrationRatio:
                            return UIDisplayConvert
                                .PercentInt(value);
                        default:
                            return UIDisplayConvert
                                .StatInt(value);
                    }
                };
            // Presentation-only formatted stat text keyed by HUD property
            // name. Formats follow the property bar conventions: percentages
            // use "%", dual-value stats (physical/magic penetration) use
            // "flat|percent%".
            GameFlowLuaBridge.GetLocalStatText =
                statName =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    if (unit?.StatHandler == null)
                        return "0";
                    fp Get(StatId id) =>
                        unit.StatHandler.GetStat(id);
                    switch (statName)
                    {
                        case "AttackDamage":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.AttackDamage))
                                .ToString();
                        case "AbilityPower":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.AbilityPower))
                                .ToString();
                        case "Armor":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.Armor))
                                .ToString();
                        case "MagicResist":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.MagicResistance))
                                .ToString();
                        case "AttackSpeed":
                            return UIDisplayConvert.Decimal2(
                                Get(StatId.AttackSpeed))
                                .ToString("F2");
                        case "SkillHaste":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.CooldownReduction))
                                .ToString();
                        case "CritChance":
                            return UIDisplayConvert.PercentInt(
                                Get(StatId
                                    .CriticalStrikeChance)) +
                                "%";
                        case "MoveSpeed":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.MoveSpeed))
                                .ToString();
                        case "Regeneration":
                            return UIDisplayConvert.Decimal2(
                                Get(StatId
                                    .HealthRegeneration))
                                .ToString();
                        case "HealAndShieldPower":
                            return UIDisplayConvert.PercentInt(
                                Get(StatId.HealPower)) +
                                "%";
                        case "ArmorPenetration":
                            return UIDisplayConvert.StatInt(
                                Get(StatId
                                    .FlatArmorPenetration)) +
                                "|" +
                                UIDisplayConvert.PercentInt(
                                    Get(StatId
                                        .ArmorPenetrationRatio)) +
                                "%";
                        case "MagicPenetration":
                            return UIDisplayConvert.StatInt(
                                Get(StatId
                                    .FlatMagicPenetration)) +
                                "|" +
                                UIDisplayConvert.PercentInt(
                                    Get(StatId
                                        .MagicPenetrationRatio)) +
                                "%";
                        case "LifeSteal":
                            return UIDisplayConvert.PercentInt(
                                Get(StatId.LifeSteal)) +
                                "%";
                        case "Omnivamp":
                            return UIDisplayConvert.PercentInt(
                                Get(StatId.Omnivamp)) +
                                "%";
                        case "AttackRange":
                            return UIDisplayConvert.StatInt(
                                Get(StatId.AttackRange))
                                .ToString();
                        case "Tenacity":
                            return UIDisplayConvert.PercentInt(
                                Get(StatId.Tenacity)) +
                                "%";
                        default:
                            return "0";
                    }
                };

            // ---- Equipment bar ----

            GameFlowLuaBridge.GetLocalEquipmentSlotCount =
                () => Unit.EquipmentHandler.SlotCount;
            GameFlowLuaBridge.GetLocalEquipmentSlotId =
                slot =>
                    FindEquipmentSlotDef(slot)?.Id ?? 0;
            GameFlowLuaBridge.GetLocalEquipmentSlotName =
                slot =>
                    FindEquipmentSlotDef(slot)?.Name ?? "";
            GameFlowLuaBridge.GetLocalEquipmentSlotStack =
                slot =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    return unit?.EquipmentHandler
                        ?.GetSlot(slot)?.StackCount ?? 0;
                };
            GameFlowLuaBridge.GetLocalEquipmentSlotIcon =
                slot =>
                    ClientSpriteRegistry.Resolve(
                        FindEquipmentSlotDef(slot)?.IconAddress);
            GameFlowLuaBridge.FocusShopEquipment =
                (slot, equipmentId) =>
                    uiManager?.FocusShopOwnedEquipment(
                        slot,
                        equipmentId);
            GameFlowLuaBridge.IsEquipmentOwned =
                equipmentId =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    var handler =
                        unit?.EquipmentHandler;
                    if (handler == null)
                        return false;
                    for (int slot = 0;
                         slot <
                         EquipmentHandler.SlotCount;
                         slot++)
                    {
                        if (handler.GetSlotDef(slot)?.Id ==
                            equipmentId)
                            return true;
                    }
                    return false;
                };

            // ---- Passive ability slot ----

            GameFlowLuaBridge
                .GetPassiveCooldownRemainingSeconds =
                () =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    var passive =
                        unit?.AbilityHandler
                            ?.FixedPassive;
                    if (passive == null)
                        return 0f;
                    int remaining =
                        passive.EffectRuntime.State
                            .NextReadyLogicTick -
                        Runtime.CurrentTick;
                    return Mathf.Max(0, remaining) *
                        (float)logicDeltaSeconds;
                };
            GameFlowLuaBridge
                .GetPassiveCooldownTotalSeconds =
                () =>
                {
                    var unit =
                        Runtime.GetLocalControlledUnit();
                    var passive =
                        unit?.AbilityHandler
                            ?.FixedPassive;
                    if (passive == null ||
                        unit?.StatHandler == null)
                        return 0f;
                    int ticks =
                        passive.Definition
                            .GetCooldownTicks(
                                unit.StatHandler.Level);
                    return ticks *
                        (float)logicDeltaSeconds;
                };

            // ---- Buff bar (user-added BuffBar; design v14.2 UI rules) ----

            GameFlowLuaBridge.GetLocalBuffCount =
                () =>
                    Runtime.GetLocalControlledUnit()
                        ?.BuffHandler?.Count ?? 0;
            GameFlowLuaBridge.GetLocalBuffIcon =
                index =>
                    ClientSpriteRegistry.Resolve(
                        BuffAt(index)
                            ?.Definition?.Display?.IconAddress);
            GameFlowLuaBridge.GetLocalBuffName =
                index =>
                    BuffAt(index)
                        ?.Definition?.Display?.Name ?? "";
            GameFlowLuaBridge.GetLocalBuffStacks =
                index =>
                    BuffAt(index)?.CurrentStacks ?? 0;
            GameFlowLuaBridge.GetLocalBuffTimeProgress =
                index =>
                {
                    var buff = BuffAt(index);
                    if (buff == null ||
                        buff.IsPermanent)
                        return 0f;
                    int duration =
                        buff.Definition
                            ?.DurationTicks ?? 0;
                    return duration > 0
                        ? Mathf.Clamp01(
                            (float)buff.RemainingTicks /
                            duration)
                        : 0f;
                };
            GameFlowLuaBridge.GetLocalBuffIsPermanent =
                index =>
                    BuffAt(index)?.IsPermanent ?? false;
            GameFlowLuaBridge.GetLocalBuffShowStack =
                index =>
                    (BuffAt(index)
                        ?.Definition
                        ?.MaxStacks ?? 1) > 1;
        }

        private Unit.EquipmentDefinition
            FindEquipmentSlotDef(int slot)
        {
            var unit =
                Runtime.GetLocalControlledUnit();
            return unit?.EquipmentHandler
                ?.GetSlotDef(slot);
        }

        private FrameSyncMoba.Unit.BuffRuntime
            BuffAt(int index)
        {
            var buffs =
                Runtime.GetLocalControlledUnit()
                    ?.BuffHandler
                    ?.GetAllOrdered();
            return buffs != null &&
                index >= 0 &&
                index < buffs.Count
                    ? buffs[index]
                    : null;
        }

        private readonly TeamScoreLogThrottle
            teamScoreLogThrottle =
                new TeamScoreLogThrottle();

        private int TeamScore(int rank)
        {
            var entries =
                Runtime.MatchRule?.Statistics
                    ?.Entries;
            if (entries == null ||
                entries.Count == 0)
                return 0;
            var teamIds =
                new System.Collections.Generic
                    .List<byte>();
            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                if (!UnitWorld.TryGetUnit(
                        entries[i].HeroUnitUid,
                        out UnitType unit))
                    continue;
                byte teamId =
                    unit.TeamId.Value;
                if (!teamIds.Contains(teamId))
                    teamIds.Add(teamId);
            }
            teamIds.Sort();
            if (rank >= teamIds.Count)
                return 0;
            byte target = teamIds[rank];
            int score = 0;
            var breakdown =
                new System.Text.StringBuilder();
            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                if (!UnitWorld.TryGetUnit(
                        entries[i].HeroUnitUid,
                        out UnitType unit))
                    continue;
                if (unit.TeamId.Value == target)
                {
                    score += entries[i].Kills;
                    breakdown.Append(
                        $"[{entries[i].HeroUnitUid}:" +
                        $"k{entries[i].Kills}/d{entries[i].Deaths}/" +
                        $"a{entries[i].Assists}/c{entries[i].CreepKills}]");
                }
            }
            string scoreboardLine =
                $"[Scoreboard] rank={rank} targetTeam={target} " +
                $"score={score} teamIds={string.Join(",", teamIds)} " +
                $"breakdown={breakdown}";
            // Log only when the scoreboard content actually changed; the
            // identical line is rebuilt every HUD refresh frame otherwise.
            if (teamScoreLogThrottle.ShouldLog(
                    rank,
                    scoreboardLine))
            {
                UnityEngine.Debug.Log(
                    scoreboardLine);
            }
            return score;
        }

        private Unit.EquipmentDefinition
            FindEquipmentDefinition(int equipmentId)
        {
            var defs =
                UnitWorld?.EquipmentDatabase
                    ?.AllDefinitions;
            if (defs == null)
                return null;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null &&
                    defs[i].Id == equipmentId)
                    return defs[i];
            }
            return null;
        }

        public bool TryBindLocalPlayer(
            ulong clientId)
        {
            if (dedicatedServer ||
                !activeGameStartConfig.HasValue)
                return false;
            if (IsLocalPlayerBound)
            {
                PlayerSlotConfig boundSlot =
                    activeGameStartConfig.Value
                        .PlayerSlots[LocalPlayerSlot];
                UnityEngine.Debug.Log(
                    $"[BindLocal] already bound clientId={clientId} " +
                    $"slot={LocalPlayerSlot} " +
                    $"slotClient={boundSlot.ControllerClientId}");
                return boundSlot.ControllerClientId ==
                    clientId;
            }

            PlayerSlotConfig[] slots =
                activeGameStartConfig.Value.PlayerSlots;
            for (int i = 0;
                 i < slots.Length;
                 i++)
            {
                PlayerSlotConfig slot = slots[i];
                if (slot.ControllerClientId !=
                    clientId)
                    continue;
                if (!Runtime.TryGetControlledUnit(
                        slot.PlayerSlot,
                        out UnitType controlledUnit))
                    throw new DeterministicSimulationException(
                        $"PlayerSlot {slot.PlayerSlot} has no restored controlled Unit.");
                if (controlledUnit.TeamId !=
                    slot.TeamId)
                    throw new DeterministicSimulationException(
                        "Local controlled Unit team disagrees with GameStartConfig.");
                BindLocalPlayer(
                    controlledUnit,
                    slot.PlayerSlot,
                    clientId);
                AttachListenerToLocalUnit(
                    controlledUnit);
                LocalPlayerSlot =
                    slot.PlayerSlot;
                FrameSyncDiagnosticsUnityHost.SetContext(
                    activeGameStartConfig.Value.MatchId,
                    LocalPlayerSlot);
                Runtime.BindLocalPlayerSlot(
                    slot.PlayerSlot);
                FrameSyncGameRuntime.RegisterActiveInstance(
                    Runtime);
                LocalControlledUnitUid =
                    controlledUnit.UnitUid;
                IsLocalPlayerBound = true;
                UnityEngine.Debug.Log(
                    $"[BindLocal] bound clientId={clientId} " +
                    $"slot={slot.PlayerSlot} " +
                    $"slotClient={slot.ControllerClientId} " +
                    $"team={slot.TeamId.Value} " +
                    $"uid={controlledUnit.UnitUid}");
                return true;
            }
            UnityEngine.Debug.Log(
                $"[BindLocal] NO MATCH clientId={clientId} " +
                $"slots={string.Join(",", System.Array.ConvertAll(activeGameStartConfig.Value.PlayerSlots, s => $"{s.PlayerSlot}:{s.ControllerClientId}"))}");
            return false;
        }

        private void TryBindConfiguredLocalPlayer()
        {
            if (networkManager == null ||
                !networkManager.IsClient)
            {
                return;
            }
            // Both online and local-direct flows bind by the NGO local
            // client id. The lobby server assigns slots by sender client id
            // (server is 0, clients are 1..N), so using the first slot's
            // ControllerClientId would bind every client to slot 0 and stamp
            // Commands with the wrong ClientId.
            TryBindLocalPlayer(
                networkManager.LocalClientId);
        }

        private void OnMatchResultReady(
            MatchResultState result)
        {
            ClientApplicationFlow client =
                ApplicationFlow?.Client;
            if (client == null)
                return;
            if (client.State ==
                ClientApplicationState.InGame)
                client.ConfirmAuthorityMatchEnd();
            if (client.State !=
                ClientApplicationState.Ending)
                throw new InvalidOperationException(
                    $"MatchResultState arrived while client flow is {client.State}.");
            client.ApplyMatchResult(
                result,
                Runtime.MatchRule,
                Runtime.Prediction
                    .LatestAuthorityFrameTick);
        }

        private bool AuthorizeNetworkCommand(
            ulong senderClientId,
            GameplayCommand command)
        {
            if (!activeGameStartConfig.HasValue)
                return false;
            PlayerSlotConfig[] slots =
                activeGameStartConfig.Value.PlayerSlots;
            if ((uint)command.PlayerSlot >=
                    (uint)slots.Length)
                return false;
            PlayerSlotConfig slot =
                slots[command.PlayerSlot];
            if (slot.ControllerClientId !=
                    senderClientId ||
                slot.PlayerSlot != command.PlayerSlot ||
                !UnitWorld.TryGetUnit(
                    command.ControlledUnitUid,
                    out UnitType unit))
                return false;
            return unit.ControlledByPlayerSlot ==
                command.PlayerSlot;
        }

        private void QueueInitialUnitSpawns()
        {
            frozenInitialSpawns.Clear();
            if (initialUnitSpawns == null || initialUnitSpawns.Count == 0)
                return;

            var ordered = new List<InitialUnitSpawnAuthoring>(initialUnitSpawns);
            ordered.Sort((left, right) =>
                left.StableSpawnOrder.CompareTo(right.StableSpawnOrder));
            for (int i = 0; i < ordered.Count; i++)
            {
                InitialUnitSpawnAuthoring entry = ordered[i];
                if (entry.UnitPrototypeId <= 0)
                    throw new InvalidOperationException(
                        $"Initial spawn {i} has an invalid UnitPrototypeId.");
                if (entry.TeamId < 0 || entry.TeamId > byte.MaxValue)
                    throw new InvalidOperationException(
                        $"Initial spawn {i} TeamId must fit in a byte.");
                if (i > 0 &&
                    ordered[i - 1].StableSpawnOrder == entry.StableSpawnOrder)
                    throw new InvalidOperationException(
                        $"Duplicate initial StableSpawnOrder {entry.StableSpawnOrder}.");
                ValidateFinite(entry.Position, $"Initial spawn {i} Position");
                ValidateFinite(entry.Forward, $"Initial spawn {i} Forward");
                if (entry.PlayerControlled &&
                    (entry.PlayerSlot < 0 ||
                     entry.PlayerSlot >= 10))
                    throw new InvalidOperationException(
                        $"Initial spawn {i} has an invalid PlayerSlot.");

                fp2 position =
                    new fp2(
                        (fp)entry.Position.x,
                        (fp)entry.Position.y);
                fp2 forward =
                    new fp2(
                        (fp)entry.Forward.x,
                        (fp)entry.Forward.y);
                if (entry.UseMapSpawnPoint)
                {
                    if (bakedMap == null)
                        throw new InvalidOperationException(
                            $"Initial spawn {i} requires a DeterministicMapConfig.");
                    BakedSpawnPoint point =
                        bakedMap.GetRequiredSpawnPoint(
                            entry.SpawnPointId);
                    if (point.TeamId !=
                        new TeamId((byte)entry.TeamId))
                        throw new InvalidOperationException(
                            $"Initial spawn {i} team disagrees with SpawnPoint {entry.SpawnPointId}.");
                    position = point.Position;
                    forward = point.Forward;
                }

                Runtime.TickPipeline.QueueInitialSpawn(
                    new UnitSpawnRequest(
                        entry.UnitPrototypeId,
                        GameplayParticipantId.InitialSpawn(
                            entry.StableSpawnOrder),
                        new TeamId((byte)entry.TeamId),
                        position,
                        forward),
                    entry.MatchTopologyRole);
                frozenInitialSpawns.Add(entry);
            }
        }

        private GameStartConfig
            CreateFixtureGameStartConfig()
        {
            var players =
                new List<InitialUnitSpawnAuthoring>();
            for (int i = 0;
                 i < frozenInitialSpawns.Count;
                 i++)
            {
                InitialUnitSpawnAuthoring spawn =
                    frozenInitialSpawns[i];
                if (spawn.PlayerControlled)
                    players.Add(spawn);
            }
            if (players.Count == 0)
            {
                for (int i = 0;
                     i < frozenInitialSpawns.Count;
                     i++)
                {
                    InitialUnitSpawnAuthoring spawn =
                        frozenInitialSpawns[i];
                    if (spawn.MatchTopologyRole ==
                            MatchTopologyRole.None &&
                        spawn.TeamId > 0)
                    {
                        spawn.PlayerControlled = true;
                        spawn.PlayerSlot = 0;
                        players.Add(spawn);
                        // The frozen composition must agree with the fixture
                        // slot so BindSelectedHeroesToPlayerSpawns and
                        // BuildPlayerSlotMappings can resolve this spawn for
                        // the fabricated player slot.
                        InitialUnitSpawnAuthoring authored =
                            frozenInitialSpawns[i];
                        authored.PlayerControlled = true;
                        authored.PlayerSlot = 0;
                        frozenInitialSpawns[i] = authored;
                        break;
                    }
                }
            }
            if (players.Count == 0)
                throw new InvalidOperationException(
                    "A local framework fixture requires at least one non-neutral player spawn.");
            players.Sort((left, right) =>
                left.PlayerSlot.CompareTo(
                    right.PlayerSlot));

            var slots =
                new PlayerSlotConfig[players.Count];
            var teamIds = new List<int>();
            for (int i = 0;
                 i < players.Count;
                 i++)
            {
                InitialUnitSpawnAuthoring player =
                    players[i];
                if (player.PlayerSlot != i)
                    throw new InvalidOperationException(
                        "Fixture PlayerSlots must be contiguous and start at zero.");
                slots[i] = new PlayerSlotConfig(
                    i,
                    $"FixturePlayer{i}",
                    (ulong)(i + 1),
                    new TeamId((byte)player.TeamId),
                    player.UnitPrototypeId,
                    player.UseMapSpawnPoint
                        ? player.SpawnPointId
                        : player.StableSpawnOrder);
                if (!teamIds.Contains(player.TeamId))
                    teamIds.Add(player.TeamId);
            }
            return new GameStartConfig(
                "framework-fixture",
                1,
                1,
                slots.Length,
                teamIds.Count,
                slots,
                0,
                bakedConfig.RandomSeed,
                bakedConfig.GameplayDataVersion);
        }

        private PlayerSlotUnitMapping[]
            BuildPlayerSlotMappings(
                in GameStartConfig config,
                UnitUid[] spawned)
        {
            if (spawned == null ||
                spawned.Length !=
                    frozenInitialSpawns.Count)
                throw new DeterministicSimulationException(
                    "Initial spawn results do not match the frozen composition.");
            PlayerSlotConfig[] slots =
                config.PlayerSlots;
            var mappings =
                new PlayerSlotUnitMapping[slots.Length];
            for (int slotIndex = 0;
                 slotIndex < slots.Length;
                 slotIndex++)
            {
                PlayerSlotConfig slot =
                    slots[slotIndex];
                int spawnIndex = -1;
                for (int i = 0;
                     i < frozenInitialSpawns.Count;
                     i++)
                {
                    InitialUnitSpawnAuthoring spawn =
                        frozenInitialSpawns[i];
                    int resolvedSpawnPointId =
                        spawn.UseMapSpawnPoint
                            ? spawn.SpawnPointId
                            : spawn.StableSpawnOrder;
                    if (resolvedSpawnPointId ==
                            slot.SpawnPointId &&
                        spawn.UnitPrototypeId ==
                            slot.HeroConfigId &&
                        spawn.TeamId ==
                            slot.TeamId.Value)
                    {
                        spawnIndex = i;
                        break;
                    }
                }
                if (spawnIndex < 0)
                    throw new DeterministicSimulationException(
                        $"PlayerSlot {slot.PlayerSlot} has no matching initial spawn.");
                mappings[slotIndex] =
                    new PlayerSlotUnitMapping(
                        slot.PlayerSlot,
                        spawned[spawnIndex]);
            }
            return mappings;
        }

        /// <summary>
        /// Player-controlled initial spawns are bound to the hero selected in
        /// the lobby: the spawn slot (spawn point + team) is the deterministic
        /// topology, the hero prototype comes from
        /// PlayerSlotConfig.HeroConfigId instead of the authored scene value.
        /// This keeps the scene composition hero-agnostic (adding a hero only
        /// needs the prefab table, unit catalog and hero display table).
        /// </summary>
        private void BindSelectedHeroesToPlayerSpawns(
            in GameStartConfig config)
        {
            for (int slotIndex = 0;
                 slotIndex < config.PlayerSlots.Length;
                 slotIndex++)
            {
                PlayerSlotConfig slot =
                    config.PlayerSlots[slotIndex];
                int spawnIndex =
                    FindPlayerSpawnIndex(slot);
                if (spawnIndex < 0)
                    throw new DeterministicSimulationException(
                        $"PlayerSlot {slot.PlayerSlot} has no matching " +
                        "player spawn slot.");
                InitialUnitSpawnAuthoring spawn =
                    frozenInitialSpawns[spawnIndex];
                if (spawn.UnitPrototypeId ==
                    slot.HeroConfigId)
                {
                    continue;
                }
                spawn.UnitPrototypeId =
                    slot.HeroConfigId;
                frozenInitialSpawns[spawnIndex] =
                    spawn;
                Runtime.TickPipeline
                    .OverrideInitialSpawnPrototype(
                        spawnIndex,
                        slot.HeroConfigId);
            }
        }

        private int FindPlayerSpawnIndex(
            in PlayerSlotConfig slot)
        {
            for (int i = 0;
                 i < frozenInitialSpawns.Count;
                 i++)
            {
                InitialUnitSpawnAuthoring spawn =
                    frozenInitialSpawns[i];
                if (!spawn.PlayerControlled)
                    continue;
                int resolvedSpawnPointId =
                    spawn.UseMapSpawnPoint
                        ? spawn.SpawnPointId
                        : spawn.StableSpawnOrder;
                if (resolvedSpawnPointId ==
                        slot.SpawnPointId &&
                    spawn.TeamId ==
                        slot.TeamId.Value)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ConfigureInitialAIControllers(
            UnitUid[] spawned)
        {
            for (int i = 0;
                 i < frozenInitialSpawns.Count;
                 i++)
            {
                // The authoring flag means "register an AI for this initial
                // spawn"; the concrete controller is chosen by UnitKind so a
                // minion authored with EnableTowerAI (as in the long-run test
                // scene) walks its lane instead of standing like a tower.
                if (!frozenInitialSpawns[i]
                        .EnableTowerAI)
                    continue;
                if (!UnitWorld.TryGetUnit(
                        spawned[i],
                        out UnitType unit))
                    throw new DeterministicSimulationException(
                        $"Initial AI spawn {i} was not materialized.");
                if (unit.UnitKind == UnitKind.Structure)
                {
                    UnitWorld.RegisterAIController(
                        new TowerAIController(unit));
                }
                else if (unit.UnitKind ==
                         UnitKind.Minion)
                {
                    ushort laneId =
                        ResolveMinionLaneId(unit);
                    if (laneId == 0)
                    {
                        throw new DeterministicSimulationException(
                            $"Initial minion spawn {i} " +
                            $"{unit.UnitUid} could not resolve a lane.");
                    }
                    UnitWorld.RegisterAIController(
                        new MinionAIController(
                            unit,
                            laneId));
                }
            }
        }

        /// <summary>
        /// Map an initial minion to its lane by matching its spawn position
        /// against the authored lane team spawn points (Unit v27.3 minion AI
        /// needs a LaneId to issue LaneAdvance flow-field orders).
        /// </summary>
        private ushort ResolveMinionLaneId(
            UnitType minion)
        {
            if (nonHeroLanes == null ||
                nonHeroLanes.Length == 0)
            {
                return 0;
            }
            fp2 position =
                minion.PhysicsEntity
                    .Transform2D.Position;
            ushort bestLane = 0;
            fp bestDistanceSq =
                new fp(int.MaxValue);
            for (int laneIndex = 0;
                 laneIndex < nonHeroLanes.Length;
                 laneIndex++)
            {
                LaneRuntimeData lane =
                    nonHeroLanes[laneIndex];
                LaneTeamSpawnData[] spawns =
                    lane.TeamSpawns;
                for (int spawnIndex = 0;
                     spawnIndex < spawns.Length;
                     spawnIndex++)
                {
                    if (spawns[spawnIndex].TeamId !=
                        minion.TeamId)
                    {
                        continue;
                    }
                    fp distanceSq =
                        fpmath.lengthsq(
                            position -
                            spawns[spawnIndex].Position);
                    if (distanceSq >= bestDistanceSq)
                    {
                        continue;
                    }
                    bestDistanceSq = distanceSq;
                    bestLane = lane.LaneId;
                }
            }
            return bestLane;
        }

        private static void ValidateFinite(Vector2 value, string label)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y))
                throw new InvalidOperationException($"{label} must be finite.");
        }

        /// <summary>
        /// Application scheduling boundary from FrameSync v10.2 section 8.9.
        /// Elapsed monotonic time selects only how many deterministic Logic Ticks run;
        /// it is never passed into Gameplay calculations.
        /// </summary>
        public int AdvanceSimulationByElapsedMilliseconds(
            long elapsedMilliseconds)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "GameBootstrap must initialize before advancing simulation.");
            if (elapsedMilliseconds < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedMilliseconds),
                    "Elapsed milliseconds must be nonnegative.");
            if (!matchBootstrapApplied)
                return 0;

            logicAccumulatorMillisecondRateUnits = checked(
                logicAccumulatorMillisecondRateUnits +
                elapsedMilliseconds * bakedConfig.TickRate);
            if (UsesNetworkSimulation &&
                Runtime != null &&
                !IsEndpointLaunchTimeReached())
            {
                logicAccumulatorMillisecondRateUnits = 0L;
                return 0;
            }
            long monotonicNowMilliseconds =
                UsesNetworkSimulation
                    ? RequireLaunchClock()
                        .MonotonicTimeMilliseconds
                    : 0L;
            int executed = 0;
            if (UsesNetworkSimulation &&
                !dedicatedServer &&
                IsClientGameplayActive())
            {
                // Client prediction lead control (FrameSync v10.2 8.7/8.8):
                // keep the client a few Ticks ahead of the confirmed server
                // Tick so Commands target future Ticks and tolerate network
                // latency. Both the authority lead and the absolute launch
                // timeline bound prediction independently.
                int targetLead = Math.Max(
                    0,
                    bakedConfig.MaxPredictionLeadTicks - 1);
                while (executed <
                           MaxLogicTicksPerUnityFrame &&
                       Runtime.Prediction.PredictedTickCount <
                       targetLead &&
                       CanExecuteClientPredictionTick(
                           monotonicNowMilliseconds,
                           targetLead))
                {
                    if (!Runtime.ExecutePredictionTick())
                    {
                        break;
                    }
                    executed++;
                }
            }
            while (logicAccumulatorMillisecondRateUnits >=
                       DeterministicTimeConversion
                           .MillisecondsPerSecond &&
                   executed < MaxLogicTicksPerUnityFrame)
            {
                if (UsesNetworkSimulation)
                {
                    if (dedicatedServer)
                    {
                        if (!IsServerGameplayActive())
                            break;
                        Runtime.ExecuteAuthorityTick();
                    }
                    else
                    {
                        if (!IsClientGameplayActive() ||
                            !CanExecuteClientPredictionTick(
                                monotonicNowMilliseconds,
                                GetClientLaunchLeadTicks()) ||
                            !Runtime.ExecutePredictionTick())
                            break;
                    }
                }
                else
                {
                    Runtime.ExecuteOneTick();
                }
                logicAccumulatorMillisecondRateUnits -=
                    DeterministicTimeConversion
                        .MillisecondsPerSecond;
                executed++;
            }
            return executed;
        }

        private static long GetUnityMonotonicMilliseconds()
        {
            return FrameSyncLaunchSchedule.SecondsToMilliseconds(
                Time.realtimeSinceStartupAsDouble);
        }

        private int GetClientLaunchLeadTicks()
        {
            return Math.Max(
                0,
                bakedConfig.MaxPredictionLeadTicks - 1);
        }

        private bool IsEndpointLaunchTimeReached()
        {
            if (!UsesNetworkSimulation)
                return true;
            if (!launchCommitApplied)
                return false;
            if (launchPacingOriginMonotonicMilliseconds >= 0)
                return true;
            IFrameSyncLaunchClock clock = RequireLaunchClock();
            if (!FrameSyncLaunchSchedule.IsEndpointLaunchReached(
                    clock.SynchronizedServerTimeMilliseconds,
                    launchServerTimeMilliseconds,
                    bakedConfig.TickRate,
                    GetClientLaunchLeadTicks(),
                    dedicatedServer))
                return false;
            launchPacingOriginMonotonicMilliseconds =
                clock.MonotonicTimeMilliseconds;
            return true;
        }

        private bool CanExecuteClientPredictionTick(
            long monotonicNowMilliseconds,
            int predictionLeadTicks)
        {
            int maximumTickExclusive =
                FrameSyncLaunchSchedule
                    .GetMaximumClientSimulationTickExclusive(
                        matchStartTick,
                        launchPacingOriginMonotonicMilliseconds,
                        monotonicNowMilliseconds,
                        bakedConfig.TickRate,
                        predictionLeadTicks,
                        Runtime.Prediction
                            .LatestContiguousReceivedAuthorityFrameTick);
            bool allowed = Runtime.CurrentTick < maximumTickExclusive;
            if (!launchScheduleLogged &&
                (!allowed || Runtime.CurrentTick > matchStartTick))
            {
                launchScheduleLogged = true;
                Debug.Log(
                    $"[LaunchSchedule] gate local={Runtime.CurrentTick} " +
                    $"authority=" +
                    $"{Runtime.Prediction.LatestAuthorityFrameTick} " +
                    $"predicted={Runtime.Prediction.PredictedTickCount} " +
                    $"coordinatorLimit=" +
                    $"{Runtime.Prediction.MaxPredictionLeadTicks} " +
                    $"scheduleLimitExclusive={maximumTickExclusive} " +
                    $"allowed={allowed}");
            }
            return allowed;
        }

        private IFrameSyncLaunchClock RequireLaunchClock()
        {
            return launchClock ??
                throw new InvalidOperationException(
                    "Network launch scheduling requires an initialized launch clock.");
        }

        private bool IsServerGameplayActive()
        {
            if (!matchBootstrapApplied)
                return false;
            return localDevelopmentNetworkFlow ||
                   ApplicationFlow?.DedicatedServer
                       ?.State ==
                   DedicatedServerApplicationState
                       .Gameplay;
        }

        private bool IsClientGameplayActive()
        {
            if (!matchBootstrapApplied)
                return false;
            return localDevelopmentNetworkFlow ||
                   ApplicationFlow?.Client?.State ==
                   ClientApplicationState.InGame;
        }

        public void BindLocalPlayer(
            UnitType controlledUnit,
            int playerSlot,
            ulong clientId)
        {
            if (dedicatedServer)
                throw new InvalidOperationException(
                    "Dedicated Server cannot bind local player input.");
            if (!IsInitialized || controlledUnit == null)
                throw new InvalidOperationException(
                    "Bootstrap and controlled Unit must be initialized first.");
            if (playerInputController == null || gameplayCamera == null)
                throw new InvalidOperationException(
                    "Client bootstrap requires PlayerInputController and Gameplay Camera.");

            controlledUnit.ControlledByPlayerSlot = playerSlot;
            var buffer = new LocalInputEventBuffer();
            MobaCameraPresentationConfig cameraConfig =
                gameplayCamera
                    .GetComponent<CameraController>()
                    ?.PresentationConfig;
            fp pointerGroundY = cameraConfig != null
                ? (fp)cameraConfig.PointerGroundY
                : fp.zero;
            fp pointerPickRadius = cameraConfig != null
                ? (fp)cameraConfig.PointerPickRadius
                : (fp)4;
            var resolver = new MouseWorldResolver(
                gameplayCamera,
                pointerGroundY,
                UnitWorld,
                pointerPickRadius);
            var outlineDriver =
                GetComponent<UnitOutlineHoverDriver>();
            if (outlineDriver == null)
            {
                outlineDriver =
                    gameObject.AddComponent<
                        UnitOutlineHoverDriver>();
            }
            outlineDriver.Initialize(
                resolver,
                UnitWorld,
                controlledUnit.TeamId,
                cameraConfig != null
                    ? cameraConfig.FriendlyOutlineColor
                    : Color.green,
                cameraConfig != null
                    ? cameraConfig.EnemyOutlineColor
                    : Color.red,
                cameraConfig != null
                    ? cameraConfig.OutlineWidth
                    : 0.05f);
            var requester = new PlayerCommandRequester(
                controlledUnit,
                new GameplayInputGate(),
                Runtime.CommandCollector,
                playerSlot,
                clientId,
                Runtime.CreateCommandTargetTickResolver(),
                AbilityInputMappingProvider.CreateFromAbilityHandler(
                    controlledUnit.AbilityHandler),
                new UnitWorldAbilityRuntimeView(UnitWorld));
            playerInputController.Initialize(buffer, resolver, requester);
            Runtime.ConfigureShopCommandSubmitter(requester);
            if (indicatorDriver != null)
            {
                playerInputController.SetIndicatorDriver(indicatorDriver);
            }

            localShopView =
                Runtime.CreateEquipmentShopView(
                    controlledUnit
                        .ControlledByPlayerSlot);

            // Bind minimap to controlled unit
            if (minimapController != null)
            {
                minimapController.Bind(controlledUnit, UnitWorld);
            }

        }

        private void PushUiSnapshot()
        {
            // The UI snapshot targets the local client's own controlled unit
            // only. ReapplyPlayerSlotMappings stamps every mapped hero with
            // its slot on every endpoint, so scanning for the first unit
            // with ControlledByPlayerSlot >= 0 would pick the same hero on
            // every client. Resolve through LocalPlayerSlot -> mapping.
            UnitType unit =
                Runtime.GetLocalControlledUnit();
            if (unit == null) return;

            int currentTick = Runtime.CurrentTick;

            // Populate scoreboard data from MatchStatisticsRuntime
            var stats = Runtime.MatchRule.Statistics;
            PopulateScoreboardDto(
                stats,
                unit.UnitUid,
                ref _scoreboardBuffer);

            int confirmedGold =
                Runtime.GoldIncome
                    ?.GetConfirmedEarnedGoldTotal(
                        unit.ControlledByPlayerSlot) ??
                0;
            var dto = new UiSnapshotDto
            {
                CurrentHealth = unit.StatHandler?.CurrentHealth ?? fp.zero,
                MaxHealth = unit.StatHandler?.GetStat(StatId.MaxHealth) ?? fp.one,
                CurrentResource =
                    unit.StatHandler?.CurrentCastResource ??
                    fp.zero,
                MaxResource =
                    unit.StatHandler?.GetStat(
                        StatId.MaxCastResource) ??
                    fp.zero,
                CurrentGold = localShopView
                    ?.GetCurrentAvailableGold() ??
                    confirmedGold,
                ConfirmedGold = confirmedGold,
                CooldownRemaining0 = GetCooldownTicks(unit, 0, currentTick),
                CooldownRemaining1 = GetCooldownTicks(unit, 1, currentTick),
                CooldownRemaining2 = GetCooldownTicks(unit, 2, currentTick),
                CooldownRemaining3 = GetCooldownTicks(unit, 3, currentTick),
                CooldownTotal0 = GetCooldownTotalTicks(unit, 0),
                CooldownTotal1 = GetCooldownTotalTicks(unit, 1),
                CooldownTotal2 = GetCooldownTotalTicks(unit, 2),
                CooldownTotal3 = GetCooldownTotalTicks(unit, 3),
                UnitLevel = unit.StatHandler?.Level ?? 1,
                CurrentExperience = unit.StatHandler?.CurrentExperience ?? 0,
                ExperienceForNextLevel = unit.StatHandler?.ExperienceRequiredForNextLevel ?? 100,

                // Scoreboard data
                PlayerCount = _scoreboardBuffer.PlayerCount,
                Kills = _scoreboardBuffer.Kills,
                Deaths = _scoreboardBuffer.Deaths,
                Assists = _scoreboardBuffer.Assists,
                CreepScore = _scoreboardBuffer.CreepScore,
                AllPlayerKills = _scoreboardBuffer.AllPlayerKills,
                AllPlayerDeaths = _scoreboardBuffer.AllPlayerDeaths,
                AllPlayerAssists = _scoreboardBuffer.AllPlayerAssists,
                AllPlayerCreepScore =
                    _scoreboardBuffer.AllPlayerCreepScore,
                AllPlayerNames = _scoreboardBuffer.AllPlayerNames,
            };

            // Update static cache for controllers
            LuaDataCache.Latest = dto;
            LogHudDiagnostics(unit);

            // Push to Lua bridge if configured
            if (luaBridge != null)
            {
                luaBridge.PushTickData(currentTick, dto, unit);
            }
        }

        private void SubmitDebugCommand(
            DebugCommandOp op,
            int value = 0)
        {
            var requester =
                playerInputController
                    ?.CommandRequester;
            if (requester == null)
                return;
            requester.RequestDebugCommand(
                (byte)op,
                value);
        }

        // Scoreboard aggregation buffer (reused per tick)
        private ScoreboardBuffer _scoreboardBuffer;

        private struct ScoreboardBuffer
        {
            public int PlayerCount;
            public int Kills;
            public int Deaths;
            public int Assists;
            public int CreepScore;
            public System.Collections.Generic.List<int> AllPlayerKills;
            public System.Collections.Generic.List<int> AllPlayerDeaths;
            public System.Collections.Generic.List<int> AllPlayerAssists;
            public System.Collections.Generic.List<int> AllPlayerCreepScore;
            public System.Collections.Generic.List<string> AllPlayerNames;
        }

        private static void PopulateScoreboardDto(
            MatchStatisticsRuntime stats,
            in UnitUid localHeroUid,
            ref ScoreboardBuffer buffer)
        {
            if (stats == null)
            {
                buffer = default;
                return;
            }

            var entries = stats.Entries;
            int count = entries?.Count ?? 0;
            buffer.PlayerCount = count;

            // Per-controlled-unit aggregates (controlled unit's stats)
            buffer.Kills = 0;
            buffer.Deaths = 0;
            buffer.Assists = 0;
            buffer.CreepScore = 0;

            buffer.AllPlayerKills = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerDeaths = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerAssists = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerCreepScore = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerNames = new System.Collections.Generic.List<string>(count);

            for (int i = 0; i < count; i++)
            {
                var e = entries[i];
                buffer.AllPlayerKills.Add(e.Kills);
                buffer.AllPlayerDeaths.Add(e.Deaths);
                buffer.AllPlayerAssists.Add(e.Assists);
                buffer.AllPlayerCreepScore.Add(
                    e.CreepKills);
                buffer.AllPlayerNames.Add($"Hero {e.HeroUnitUid.SpawnLogicTick}");
                if (e.HeroUnitUid == localHeroUid)
                {
                    buffer.Kills = e.Kills;
                    buffer.Deaths = e.Deaths;
                    buffer.Assists = e.Assists;
                    buffer.CreepScore = e.CreepKills;
                }
            }
        }

        private static int GetCooldownTicks(UnitType unit, byte slot, int currentTick)
        {
            return unit.AbilityHandler?.GetDisplayCooldownRemainingTicks(slot, currentTick) ?? 0;
        }

        private static int GetCooldownTotalTicks(UnitType unit, byte slot)
        {
            return unit.AbilityHandler?.GetDisplayCooldownTotalTicks(slot) ?? 1;
        }

        private string _lastHudBuffSignature;
        private int _lastHudCreep = -1;

        private void LogHudDiagnostics(UnitType unit)
        {
            if (unit == null)
                return;
            var buffs =
                unit.BuffHandler?.GetAllOrdered();
            if (buffs != null)
            {
                var sb =
                    new System.Text.StringBuilder();
                sb.Append("count=")
                    .Append(buffs.Count);
                for (int i = 0;
                     i < buffs.Count;
                     i++)
                {
                    var buff = buffs[i];
                    sb.Append(" [")
                        .Append(
                            buff.Definition
                                ?.ConfigId.Value)
                        .Append(":st")
                        .Append(buff.CurrentStacks)
                        .Append("/max")
                        .Append(
                            buff.Definition
                                ?.MaxStacks)
                        .Append("]");
                }
                string signature =
                    sb.ToString();
                if (signature !=
                    _lastHudBuffSignature)
                {
                    _lastHudBuffSignature =
                        signature;
                    UnityEngine.Debug.Log(
                        $"[HudBuffs] unit={unit.UnitUid} " +
                        signature);
                }
            }

            int creep =
                _scoreboardBuffer.CreepScore;
            if (creep != _lastHudCreep)
            {
                _lastHudCreep = creep;
                UnityEngine.Debug.Log(
                    $"[HudCreep] unit={unit.UnitUid} " +
                    $"creep={creep}");
            }
        }

        private void OnDestroy()
        {
            isDestroying = true;
            contentLoadCancellation?.Cancel();
            if (uiManager != null)
                uiManager.Initialized -= OnUiManagerInitialized;
            FrameSyncGameRuntime.UnregisterActiveInstance(
                Runtime);
            if (frameSyncNetworkBridge != null)
                frameSyncNetworkBridge.MatchResultReady -=
                    OnMatchResultReady;
            if (GameSessionContext.LobbyBridge != null)
            {
                GameSessionContext.LobbyBridge.StartScheduled -=
                    OnLobbyStartScheduled;
                GameSessionContext.LobbyBridge
                    .AllClientsBootstrapApplied -=
                    OnAllClientsBootstrapApplied;
            }
            matchContentScope?.Dispose();
            matchContentScope = null;
            contentLoadCancellation?.Dispose();
            contentLoadCancellation = null;
        }
    }
}
