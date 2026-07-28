using System;
using System.Collections.Generic;
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
        public MatchTopologyRole MatchTopologyRole;
    }

    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Project-wide deterministic configuration")]
        [SerializeField] private GlobalGameplayData globalGameplayData;
        [SerializeField] private UnitRuntimeCatalogAsset unitRuntimeCatalog;
        [SerializeField] private AbilityRuntimeCatalogAsset abilityRuntimeCatalog;
        [SerializeField] private ProjectileRuntimeCatalogAsset projectileRuntimeCatalog;
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private bool driveSimulationFromUnityUpdate = true;

        [Header("Optional online application flow")]
        [SerializeField] private bool enableOnlineApplicationFlow;
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
        [SerializeField] private AttackSfxHandler attackSfxHandler;
        [SerializeField] private HitReactionPresenter hitReactionPresenter;
        [SerializeField] private DeathPresenter deathPresenter;

        [Header("Lua UI Bridge")]
        [SerializeField] private LuaBridge.LuaBridge luaBridge;

        [Header("Shop UI")]
        [SerializeField] private ShopPageController shopPage;

        [Header("HUD Elements (0087/0089)")]
        [SerializeField] private ScoreboardController scoreboardController;
        [SerializeField] private MinimapController minimapController;
        [SerializeField] private AbilityCooldownPresenter cooldownPresenter;
        [SerializeField] private ResultPageController resultPageController;

        [Header("Explicit non-hero map topology")]
        [SerializeField] private MinionWaveConfig minionWaveConfig;
        [SerializeField] private LaneAuthoring[] laneAuthoring =
            Array.Empty<LaneAuthoring>();
        [SerializeField] private JungleCamp[] jungleCamps =
            Array.Empty<JungleCamp>();

        public FrameSyncGameRuntime Runtime { get; private set; }
        public UnitWorld UnitWorld { get; private set; }
        public PhysicsWorld PhysicsWorld { get; private set; }
        public bool IsInitialized => Runtime != null;
        public MatchFlowStateMachine MatchFlow { get; private set; }
        public GameApplicationFlowManager ApplicationFlow { get; private set; }
        public int MaxLogicTicksPerUnityFrame { get; private set; }

        private double logicAccumulatorSeconds;
        private double logicDeltaSeconds;
        private double recoveryAccumulatorSeconds;
        private IEquipmentShopView localShopView;
        private GameStartConfig? activeGameStartConfig;
        private int recoveryControlTick;

        private void Awake()
        {
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();
            if (unitRuntimeCatalog == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires UnitRuntimeCatalogAsset.");
            BakedUnitRuntimeCatalog unitCatalog =
                unitRuntimeCatalog.BakeOrThrow(config.PrefabTable);
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
                StatDefinitionTable = unitCatalog.StatDefinitions,
                EquipmentDatabase = new EquipmentDatabase(),
                AbilityDefinitions = abilityDefinitions,
                BuffDefinitions = new BuffDefinitionRegistry(),
                StatGrowthC = config.StatGrowthC,
                StatGrowthD = config.StatGrowthD,
                TickRate = config.TickRate,
                AttackSequenceResetIntervalTicks =
                    config.AttackSequenceResetIntervalTicks,
            };
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
            Runtime.MatchRule.BeginCountdown(0, config.CountdownTicks);

            // Create MatchFlowStateMachine (0090)
            MatchFlow = new MatchFlowStateMachine(Runtime.MatchRule);

            // Create and wire shop page
            if (shopPage == null)
            {
                var shopGo = new GameObject("ShopPage", typeof(ShopPageController));
                shopGo.transform.SetParent(transform, false);
                shopPage = shopGo.GetComponent<ShopPageController>();
            }
            shopPage.Inject(Runtime, Runtime.TickPipeline.EquipmentShop,
                UnitWorld.EquipmentDatabase, null);

            // Create HUD elements if not assigned (0087/0089)
            if (scoreboardController == null)
            {
                var sbGo = new GameObject("ScoreboardController", typeof(ScoreboardController));
                sbGo.transform.SetParent(transform, false);
                scoreboardController = sbGo.GetComponent<ScoreboardController>();
            }
            if (minimapController == null)
            {
                var mmGo = new GameObject("MinimapController", typeof(MinimapController));
                mmGo.transform.SetParent(transform, false);
                minimapController = mmGo.GetComponent<MinimapController>();
            }
            if (cooldownPresenter == null)
            {
                var cdGo = new GameObject("AbilityCooldownPresenter", typeof(AbilityCooldownPresenter));
                cdGo.transform.SetParent(transform, false);
                cooldownPresenter = cdGo.GetComponent<AbilityCooldownPresenter>();
            }

            // Wire presentation dispatch and UI snapshot after each tick
            Runtime.TickPipeline.TickCompleted += (_, _, _) =>
            {
                MatchFlow.ObserveTick();

                // Show result screen when match finishes (0092)
                if (MatchFlow.HasFinished && resultPageController != null && !resultPageController.IsShown)
                    resultPageController.Show(MatchFlow.Result);

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
                if (attackSfxHandler != null) presentationDispatcher.RegisterSfxHandler(attackSfxHandler);
                if (hitReactionPresenter != null) presentationDispatcher.RegisterVfxHandler(hitReactionPresenter);
                if (deathPresenter != null)
                {
                    presentationDispatcher.RegisterVfxHandler(deathPresenter);
                    presentationDispatcher.RegisterSfxHandler(deathPresenter);
                }
            }
        }

        private void InitializeNonHeroTopology(
            in BakedGlobalGameplayData config)
        {
            LaneAuthoring[] authoredLanes =
                laneAuthoring ??
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

        private void Update()
        {
            if (enableOnlineApplicationFlow &&
                !dedicatedServer &&
                ApplicationFlow?.Client?.State ==
                    ClientApplicationState.InGame &&
                frameSyncNetworkBridge != null)
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
            if (!enableOnlineApplicationFlow) return;
            try
            {
                if (dedicatedServer)
                    frameSyncNetworkBridge.Bind(
                        Runtime,
                        AuthorizeNetworkCommand);
                else
                    frameSyncNetworkBridge.Bind(Runtime);
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

        private void ConfigureOptionalApplicationFlow()
        {
            if (!enableOnlineApplicationFlow) return;
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
            activeGameStartConfig = config;
            frameSyncNetworkBridge?.SetMatchId(
                config.MatchId);
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

                Runtime.TickPipeline.QueueInitialSpawn(
                    new UnitSpawnRequest(
                        entry.UnitPrototypeId,
                        new TeamId((byte)entry.TeamId),
                        new fp2((fp)entry.Position.x, (fp)entry.Position.y),
                        new fp2((fp)entry.Forward.x, (fp)entry.Forward.y)),
                    entry.MatchTopologyRole);
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

            logicAccumulatorSeconds += elapsedSeconds;
            int executed = 0;
            while (logicAccumulatorSeconds >= logicDeltaSeconds &&
                   executed < MaxLogicTicksPerUnityFrame)
            {
                if (enableOnlineApplicationFlow)
                {
                    if (dedicatedServer)
                    {
                        if (ApplicationFlow?.DedicatedServer?.State !=
                            DedicatedServerApplicationState.Gameplay)
                            break;
                        Runtime.ExecuteAuthorityTick();
                    }
                    else
                    {
                        if (ApplicationFlow?.Client?.State !=
                            ClientApplicationState.InGame ||
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
