using System;
using System.Collections.Generic;
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
        [SerializeField] private UnitRuntimeCatalogAsset unitRuntimeCatalog;
        [SerializeField] private AbilityRuntimeCatalogAsset abilityRuntimeCatalog;
        [SerializeField] private ProjectileRuntimeCatalogAsset projectileRuntimeCatalog;
        [SerializeField] private DeterministicMapConfig deterministicMapConfig;
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private bool driveSimulationFromUnityUpdate = true;

        [Header("Optional online application flow")]
        [SerializeField] private bool enableOnlineApplicationFlow;
        [Tooltip("Explicit local NGO path. It bypasses UOS only for local development and never reports provider success.")]
        [SerializeField] private bool localDevelopmentNetworkFlow;
        [SerializeField] private bool autoApplyLocalFixturePayload = true;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FrameSyncNetworkBridge frameSyncNetworkBridge;
        [SerializeField] private string uosMatchmakingConfigId;
        [SerializeField] private string uosRegionId;

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
        [SerializeField] private ShopPageController shopPage;

        [Header("HUD Elements (0087/0089)")]
        [SerializeField] private ScoreboardController scoreboardController;
        [SerializeField] private MinimapController minimapController;
        [SerializeField] private AbilityCooldownPresenter cooldownPresenter;
        [SerializeField] private ResultPageController resultPageController;
        [SerializeField] private ClientUiActionRouter clientUiActionRouter;
        [SerializeField] private LobbyPanelController lobbyPanelController;
        [SerializeField] private HeroSelectPageController heroSelectPageController;

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
        public bool IsMatchReady => matchBootstrapApplied;
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

        private double logicAccumulatorSeconds;
        private double logicDeltaSeconds;
        private double recoveryAccumulatorSeconds;
        private IEquipmentShopView localShopView;
        private GameStartConfig? activeGameStartConfig;
        private int recoveryControlTick;
        private BakedGlobalGameplayData bakedConfig;
        private BakedDeterministicMapData bakedMap;
        private bool matchBootstrapApplied;
        private List<InitialUnitSpawnAuthoring> frozenInitialSpawns =
            new List<InitialUnitSpawnAuthoring>();

        private void Awake()
        {
            ResolveMapPathfindingAuthoring();
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();
            bakedConfig = config;
            LocalVersions = new FrameSyncVersionHandshake(
                config.GameplayDataVersion,
                config.MapDataVersion,
                config.GlobalPrefabTableVersion,
                config.CommandSchemaVersion,
                (uint)GameplaySnapshot.CurrentSchemaVersion);
            if (unitRuntimeCatalog == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires UnitRuntimeCatalogAsset.");
            BakedUnitRuntimeCatalog unitCatalog =
                unitRuntimeCatalog.BakeOrThrow(config.PrefabTable);
            if (unitCatalog.DisposePolicies == null)
                throw new InvalidOperationException(
                    $"{nameof(UnitRuntimeCatalogAsset)} requires a UnitDisposePolicyTable.");
            if (abilityRuntimeCatalog == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires AbilityRuntimeCatalogAsset.");
            AbilityDefinitionRegistry abilityDefinitions =
                abilityRuntimeCatalog.BakeOrThrow();
            MaxLogicTicksPerUnityFrame = config.MaxLogicTicksPerUnityFrame;
            logicDeltaSeconds = 1d / config.TickRate;
            logicAccumulatorSeconds = 0d;

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
                EquipmentDatabase = new EquipmentDatabase(),
                AbilityDefinitions = abilityDefinitions,
                BuffDefinitions = new BuffDefinitionRegistry(),
                StatGrowthC = config.StatGrowthC,
                StatGrowthD = config.StatGrowthD,
                MoveSpeedToLogicVelocityScale =
                    config.MoveSpeedToLogicVelocityScale,
                StatDistanceToLogicDistanceScale =
                    config.MoveSpeedToLogicVelocityScale,
                TickRate = config.TickRate,
                AttackSequenceResetIntervalTicks =
                    config.AttackSequenceResetIntervalTicks,
            };
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
            if (projectileRuntimeCatalog != null)
            {
                Runtime.TickPipeline.ProjectileWorld.DefRegistry =
                    projectileRuntimeCatalog.BakeOrThrow(
                        config.PrefabTable);
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
                if (MatchFlow.HasFinished && resultPageController != null && !resultPageController.IsShown)
                {
                    uiManager?.OpenPage(
                        UIPageId.Result);
                    resultPageController.Show(MatchFlow.Result);
                }

                shopPage?.ProcessQueuedTransaction();
                presentationDispatcher?.DispatchCurrentFrame();
                PushUiSnapshot();
            };

            if (dedicatedServer && playerInputController != null)
                throw new InvalidOperationException(
                    "Dedicated Server bootstrap must not reference PlayerInputController.");

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

            BakedMinionWaveConfig schedule =
                minionWaveConfig != null
                    ? BakedMinionWaveConfig
                        .FromConfig(
                            minionWaveConfig)
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

        private void ResolveMapPathfindingAuthoring()
        {
            if (flowFieldAuthoring == null)
            {
                GameObject mapPrefab =
                    Resources.Load<GameObject>(
                        "Prefab/Map");
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
            if (UsesNetworkSimulation &&
                !dedicatedServer &&
                IsClientGameplayActive() &&
                frameSyncNetworkBridge != null &&
                frameSyncNetworkBridge
                    .IsConnectedClient)
            {
                frameSyncNetworkBridge.SendLocalCommands();
                recoveryAccumulatorSeconds +=
                    Time.unscaledDeltaTime;
                while (recoveryAccumulatorSeconds >=
                       logicDeltaSeconds)
                {
                    recoveryAccumulatorSeconds -=
                        logicDeltaSeconds;
                    recoveryControlTick++;
                }
                frameSyncNetworkBridge.TickRecovery(
                    recoveryControlTick);
            }
            if (driveSimulationFromUnityUpdate)
                AdvanceSimulationByElapsedSeconds(Time.unscaledDeltaTime);
            if (shopPage != null) shopPage.TickVisual();
        }

        private async void Start()
        {
            if (!UsesNetworkSimulation) return;
            try
            {
                BindFrameSyncNetworkRuntime();
                if (localDevelopmentNetworkFlow)
                    return;
                if (dedicatedServer)
                    await ApplicationFlow.DedicatedServer.BootAsync();
                else
                    await ApplicationFlow.Client
                        .InitializeAccountAsync(
                            Environment.GetCommandLineArgs());
            }
            catch (Exception exception)
            {
                driveSimulationFromUnityUpdate = false;
                Debug.LogException(exception, this);
            }
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
                throw new InvalidOperationException(
                    "Online application flow requires NetworkManager.");
            if (frameSyncNetworkBridge == null)
                frameSyncNetworkBridge =
                    GetComponent<FrameSyncNetworkBridge>();
            if (frameSyncNetworkBridge == null)
                throw new InvalidOperationException(
                    "Online application flow requires FrameSyncNetworkBridge.");
            frameSyncNetworkBridge.MatchResultReady +=
                OnMatchResultReady;

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
                if (string.IsNullOrWhiteSpace(
                        uosMatchmakingConfigId))
                    throw new InvalidOperationException(
                        "Client online flow requires a UOS Matchmaking config ID.");
                ApplicationFlow =
                    new GameApplicationFlowManager(
                        new ClientApplicationFlow(
                            new TestAccountBootstrapService(
                                new PlayerPrefsTestAccountPersistence()),
                            new UosClientSession(),
                            new UosMatchmakingApplicationClient(
                                uosMatchmakingConfigId,
                                uosRegionId),
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
            Runtime.ConfigureMatchStart(
                payload.StartTick,
                payload.InitialRandomSeed,
                payload.GameStartConfig.GameStartPlayerCount,
                bakedConfig.InitialEarnedGold);
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
            matchBootstrapApplied = true;
            if (!dedicatedServer)
                TryBindConfiguredLocalPlayer();
        }

        private void ConfigureClientPresentation()
        {
            if (uiManager == null)
                uiManager =
                    FindObjectOfType<UIManager>(true);
            if (uiManager != null)
            {
                uiManager.Initialize();
                shopPage ??=
                    uiManager.GetPageComponent<ShopPageController>(
                        UIPageId.Shop);
                scoreboardController ??=
                    uiManager.GetPageComponent<ScoreboardController>(
                        UIPageId.GameplayHud);
                minimapController ??=
                    uiManager.GetPageComponent<MinimapController>(
                        UIPageId.GameplayHud);
                cooldownPresenter ??=
                    uiManager.GetPageComponent<AbilityCooldownPresenter>(
                        UIPageId.GameplayHud);
                resultPageController ??=
                    uiManager.GetPageComponent<ResultPageController>(
                        UIPageId.Result);
                lobbyPanelController ??=
                    uiManager.GetPageComponent<LobbyPanelController>(
                        UIPageId.Lobby);
                heroSelectPageController ??=
                    uiManager.GetPageComponent<HeroSelectPageController>(
                        UIPageId.HeroSelect);
                uiManager.OpenPage(
                    UIPageId.Lobby);
                uiManager.OpenPage(
                    UIPageId.HeroSelect);
            }

            shopPage?.Inject(
                Runtime,
                Runtime.TickPipeline.EquipmentShop,
                UnitWorld.EquipmentDatabase,
                null);

            if (clientUiActionRouter != null)
            {
                lobbyPanelController?.Inject(
                    clientUiActionRouter);
                heroSelectPageController?.Inject(
                    lobbyPanelController,
                    clientUiActionRouter);
                resultPageController?.Inject(
                    clientUiActionRouter);
            }
            lobbyPanelController?.Show();
            heroSelectPageController?.Show();
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
                LocalPlayerSlot =
                    slot.PlayerSlot;
                LocalControlledUnitUid =
                    controlledUnit.UnitUid;
                IsLocalPlayerBound = true;
                return true;
            }
            return false;
        }

        private void TryBindConfiguredLocalPlayer()
        {
            ulong clientId;
            if (enableOnlineApplicationFlow)
            {
                if (networkManager == null ||
                    !networkManager.IsClient)
                    return;
                clientId =
                    networkManager.LocalClientId;
            }
            else
            {
                PlayerSlotConfig[] slots =
                    activeGameStartConfig.Value
                        .PlayerSlots;
                if (slots.Length == 0)
                    return;
                clientId =
                    slots[0].ControllerClientId;
            }
            TryBindLocalPlayer(clientId);
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

        private void ConfigureInitialAIControllers(
            UnitUid[] spawned)
        {
            for (int i = 0;
                 i < frozenInitialSpawns.Count;
                 i++)
            {
                if (!frozenInitialSpawns[i]
                        .EnableTowerAI)
                    continue;
                if (!UnitWorld.TryGetUnit(
                        spawned[i],
                        out UnitType unit))
                    throw new DeterministicSimulationException(
                        $"Initial tower spawn {i} was not materialized.");
                UnitWorld.RegisterAIController(
                    new TowerAIController(unit));
            }
        }

        private static void ValidateFinite(Vector2 value, string label)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y))
                throw new InvalidOperationException($"{label} must be finite.");
        }

        /// <summary>
        /// Application scheduling boundary from FrameSync v10.2 section 8.9.
        /// Elapsed wall time selects only how many deterministic Logic Ticks run;
        /// it is never passed into Gameplay calculations.
        /// </summary>
        public int AdvanceSimulationByElapsedSeconds(double elapsedSeconds)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "GameBootstrap must initialize before advancing simulation.");
            if (double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds) ||
                elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds), "Elapsed time must be finite and nonnegative.");
            if (!matchBootstrapApplied)
                return 0;

            logicAccumulatorSeconds += elapsedSeconds;
            int executed = 0;
            while (logicAccumulatorSeconds >= logicDeltaSeconds &&
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
                            !Runtime.ExecutePredictionTick())
                            break;
                    }
                }
                else
                {
                    Runtime.ExecuteOneTick();
                }
                logicAccumulatorSeconds -= logicDeltaSeconds;
                executed++;
            }
            return executed;
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
            var resolver = new MouseWorldResolver(gameplayCamera, fp.zero);
            var requester = new PlayerCommandRequester(
                controlledUnit,
                new GameplayInputGate(),
                Runtime.CommandCollector,
                playerSlot,
                clientId,
                Runtime.CreateCommandTargetTickResolver(),
                AbilityInputProfileProvider.CreateFromAbilityHandler(
                    controlledUnit.AbilityHandler),
                new UnitWorldAbilityRuntimeView(UnitWorld));
            playerInputController.Initialize(buffer, resolver, requester);
            if (indicatorDriver != null)
            {
                playerInputController.SetIndicatorDriver(indicatorDriver);
            }

            // Update shop page with controlled unit
            if (shopPage != null)
            {
                shopPage.Inject(Runtime, Runtime.TickPipeline.EquipmentShop,
                    UnitWorld.EquipmentDatabase,
                    controlledUnit,
                    playerInputController.CommandRequester);
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

            if (uiManager != null)
            {
                uiManager.ClosePage(UIPageId.Lobby);
                uiManager.ClosePage(
                    UIPageId.HeroSelect);
                uiManager.OpenPage(
                    UIPageId.GameplayHud);
            }
        }

        private void PushUiSnapshot()
        {
            var allUnits = UnitWorld?.GetAllUnits();
            if (allUnits == null || allUnits.Count == 0) return;

            UnitType unit = null;
            for (int i = 0; i < allUnits.Count; i++)
            {
                if (allUnits[i].ControlledByPlayerSlot >= 0)
                {
                    unit = allUnits[i];
                    break;
                }
            }
            if (unit == null) return;

            int currentTick = Runtime.CurrentTick;

            // Populate scoreboard data from MatchStatisticsRuntime
            var stats = Runtime.MatchRule.Statistics;
            PopulateScoreboardDto(stats, ref _scoreboardBuffer);

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
                AllPlayerKills = _scoreboardBuffer.AllPlayerKills,
                AllPlayerDeaths = _scoreboardBuffer.AllPlayerDeaths,
                AllPlayerAssists = _scoreboardBuffer.AllPlayerAssists,
                AllPlayerNames = _scoreboardBuffer.AllPlayerNames,
            };

            // Update static cache for controllers
            LuaDataCache.Latest = dto;

            // Push to Lua bridge if configured
            if (luaBridge != null)
            {
                luaBridge.PushTickData(currentTick, dto, unit);
            }
        }

        // Scoreboard aggregation buffer (reused per tick)
        private ScoreboardBuffer _scoreboardBuffer;

        private struct ScoreboardBuffer
        {
            public int PlayerCount;
            public int Kills;
            public int Deaths;
            public int Assists;
            public System.Collections.Generic.List<int> AllPlayerKills;
            public System.Collections.Generic.List<int> AllPlayerDeaths;
            public System.Collections.Generic.List<int> AllPlayerAssists;
            public System.Collections.Generic.List<string> AllPlayerNames;
        }

        private static void PopulateScoreboardDto(
            MatchStatisticsRuntime stats,
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

            buffer.AllPlayerKills = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerDeaths = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerAssists = new System.Collections.Generic.List<int>(count);
            buffer.AllPlayerNames = new System.Collections.Generic.List<string>(count);

            for (int i = 0; i < count; i++)
            {
                var e = entries[i];
                buffer.AllPlayerKills.Add(e.Kills);
                buffer.AllPlayerDeaths.Add(e.Deaths);
                buffer.AllPlayerAssists.Add(e.Assists);
                buffer.AllPlayerNames.Add($"Hero {e.HeroUnitUid.SpawnLogicTick}");
            }
        }

        private static int GetCooldownTicks(UnitType unit, byte slot, int currentTick)
        {
            return unit.AbilityHandler?.GetCooldownRemainingTicks(slot, currentTick) ?? 0;
        }

        private static int GetCooldownTotalTicks(UnitType unit, byte slot)
        {
            return unit.AbilityHandler?.GetCooldownTotalTicks(slot) ?? 1;
        }

        private void OnDestroy()
        {
            if (frameSyncNetworkBridge != null)
                frameSyncNetworkBridge.MatchResultReady -=
                    OnMatchResultReady;
        }
    }
}
