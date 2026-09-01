using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Bootstrap.Tests;
using FrameSyncMoba.ClientContent;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.Physics;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;
using UnitUid = FrameSyncMoba.Unit.UnitUid;
using TeamId = FrameSyncMoba.Unit.TeamId;
using UnitKind = FrameSyncMoba.Unit.UnitKind;
using LifeState = FrameSyncMoba.Unit.LifeState;
using UnitPrototype = FrameSyncMoba.Unit.UnitPrototype;
using UnitSpawnRequest = FrameSyncMoba.Unit.UnitSpawnRequest;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Standalone hero test scene driver: builds a local deterministic world
    /// (no frame-sync authority/rollback), a grid map for A*, a hero and a
    /// dummy target, advances logic ticks, and exposes debug input + an
    /// IMGUI panel + grid/A* gizmos. Resolves the editor-only match content
    /// partitions selected by the configured hero and dummy (not packaged).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTestDriver : MonoBehaviour
    {
        private const int InitialShopGold = 10000;

        [Header("Map (logic units)")]
        [SerializeField] private float mapWidth = 40f;
        [SerializeField] private float mapHeight = 40f;
        [SerializeField] private Vector2 mapCenter = Vector2.zero;
        [SerializeField] private float cellSize = 0.5f;

        [Header("Hero")]
        [SerializeField] private int heroPrototypeId = 1002;
        [SerializeField] private Vector2 heroSpawn = new Vector2(-15f, -15f);
        [SerializeField] private int dummyPrototypeId = 1001;
        [SerializeField] private Vector2 dummySpawn = new Vector2(-10f, -10f);
        [Tooltip("Milliseconds after the dummy dies before it respawns at its spawn point.")]
        [SerializeField] private int dummyRespawnMilliseconds = 3000;

        [Header("Simulation")]
        [SerializeField] private float ticksPerSecond = 30f;
        [SerializeField] private bool paused;
        [Tooltip("Optional camera that should follow the hero.")]
        [SerializeField] private CameraController followCamera;

        [Header("Player input composition")]
        [Tooltip("Scene-authored generic input component. Ability mappings are derived from the spawned hero's CastModelDef and AimKind.")]
        [SerializeField] private PlayerInputController playerInputController;

        private UnitWorld world;
        private GlobalPrefabTable resolvedPrefabTable;
        private PhysicsWorld physicsWorld;
        private SimulationTickPipeline pipeline;
        private CombatSystem combat;
        private SimulationTickContextController tickController =
            new SimulationTickContextController();
        private UnitType hero;
        private readonly List<UnitType> dummies =
            new List<UnitType>();
        private long simulationAccumulatorMillisecondRateUnits;
        private long lastSimulationMonotonicMilliseconds = -1L;
        private PlayerCommandRequester playerCommandRequester;
        private SkillIndicatorDriver indicatorDriver;
        private PresentationEventDispatcher vfxDispatcher;
        private ClientProjectileViewBinder projectileViewBinder;
        private readonly List<IPresentationAssetLease<GameObject>>
            presentationLeases =
                new List<IPresentationAssetLease<GameObject>>();
        private readonly List<GameObject>
            presentationViewInstances =
                new List<GameObject>();

        private readonly Dictionary<UnitUid, ClientUnitOutline>
            outlines =
                new Dictionary<UnitUid, ClientUnitOutline>();
        private Task<List<UnitUid>> hoverTask;
        private UnitUid? hoveredUnit;
        private HeroDisplayTable heroDisplayTable;
        private EquipmentDatabase equipmentDatabase;
        private GoldIncomeRuntime goldIncome;
        private EquipmentShopRuntime equipmentShop;
        private string shopStatus = "";
        private LineRenderer attackRangeRing;
        private long dummyRespawnDeadlineMilliseconds = -1L;
        private Material outlineRimMaterial;
        private readonly Dictionary<UnitUid, LineRenderer>
            radiusCircles =
                new Dictionary<UnitUid, LineRenderer>();

        private struct HoverUnitSnapshot
        {
            public UnitUid Uid;
            public float X;
            public float Y;
            public bool Alive;
            public bool IsFriendly;
        }

        private sealed class EditorMatchContent
        {
            public GlobalPrefabTable PrefabTable;
            public readonly List<UnitRuntimeCatalogAsset> UnitCatalogs =
                new List<UnitRuntimeCatalogAsset>();
            public readonly List<AbilityRuntimeCatalogAsset> AbilityCatalogs =
                new List<AbilityRuntimeCatalogAsset>();
            public readonly List<ProjectileRuntimeCatalogAsset> ProjectileCatalogs =
                new List<ProjectileRuntimeCatalogAsset>();
            public readonly List<BuffCatalogAsset> BuffCatalogs =
                new List<BuffCatalogAsset>();
            public CrowdControlCatalogAsset CrowdControlCatalog;
            public EquipmentCatalogAsset EquipmentCatalog;
        }

        public UnitType Hero => hero;
        public IReadOnlyList<UnitType> Dummies => dummies;
        public UnitWorld World => world;
        public PlayerInputController PlayerInputController =>
            playerInputController;
        public int CurrentTick => pipeline != null
            ? pipeline.LocalSimulationTick
            : 0;

        /// <summary>
        /// Executes exactly one logic tick and returns any exception message
        /// (empty string on success). Useful for diagnosing a frozen tick
        /// from the debugger or automation.
        /// </summary>
        public string DebugExecuteOneTick()
        {
            if (pipeline == null)
            {
                return "pipeline is null";
            }
            try
            {
                pipeline.ExecuteTick(
                    tickController,
                    ExecutionMode.ServerAuthority);
                return "";
            }
            catch (System.Exception exception)
            {
                return exception.GetBaseException()?.ToString() ??
                    exception.ToString();
            }
        }

        private async void Start()
        {
            BuildWorld();
            BuildMap();
            SpawnHero();
            SpawnDummiesAtScenePoints();
            await EnsureIndicatorDriverAsync();
            await BindPresentationViewsAsync();
            ConfigurePlayerInput();
            ConfigureTestShop();
            var blightMarks =
                GetComponent<
                    BlightStackMarkPresenter>();
            if (blightMarks == null)
            {
                blightMarks =
                    gameObject.AddComponent<
                        BlightStackMarkPresenter>();
            }
            blightMarks.InitializeAddressable(
                "vfx/4102",
                () => world.GetAllUnits());
            var verticalMotion = gameObject.AddComponent<
                CrowdControlVerticalMotionPresenter>();
            verticalMotion.Initialize(
                () => world.GetAllUnits(),
                () => CurrentTick,
                ticksPerSecond);
            if (followCamera != null && hero != null)
            {
                followCamera.SetDebugTarget(hero.transform);
            }
            EnsureAudioListener();
            BindTestHudBridge();
            UIManager uiManager =
                FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ClientContent/UI/UIManager.prefab");
                uiManager = Instantiate(prefab)
                    .GetComponent<UIManager>();
            }
            if (uiManager != null)
            {
                uiManager.ShowPage(UIPageId.HUD);
            }
            BuildTestVfxPipeline();
        }

        /// <summary>
        /// The test scene camera has no AudioListener in the scene asset;
        /// without one the pooled 3D AudioSources are silent. Attach the
        /// listener to the player-controlled hero so attack SFX (played at
        /// the hero socket) are clearly audible; fall back to the camera
        /// when the hero is not yet available.
        /// </summary>
        private void EnsureAudioListener()
        {
            if (FindObjectOfType<AudioListener>() != null)
            {
                return;
            }
            if (hero != null)
            {
                hero.gameObject
                    .AddComponent<AudioListener>();
                return;
            }
            Camera camera =
                followCamera != null
                    ? followCamera
                        .GetComponent<Camera>()
                    : Camera.main;
            if (camera != null)
            {
                camera.gameObject
                    .AddComponent<AudioListener>();
            }
        }

        private void BuildTestVfxPipeline()
        {
            var dispatcherGO =
                new GameObject(
                    "HeroTestVfxDispatcher");
            vfxDispatcher =
                dispatcherGO.AddComponent<
                    PresentationEventDispatcher>();

            var vfxManagerGO =
                new GameObject(
                    "HeroTestVfxManager");
            var vfxManager =
                vfxManagerGO.AddComponent<VfxManager>();
            vfxManager.SetAssetLoader(
                ClientPresentationServices.Loader);
            vfxManager.SetLibrary(
                AssetDatabase
                    .LoadAssetAtPath<VfxLibrary>(
                        "Assets/Config/Formal/FullMatchVfxLibrary.asset"));
            var vfxHandler =
                vfxManagerGO.AddComponent<
                    VfxEventHandler>();
            vfxHandler.SetManager(vfxManager);
            vfxDispatcher.RegisterVfxHandler(
                vfxHandler);

            // Global SFX manager + Bootstrap bridge (Presentation Design
            // v13.2 section 5): SfxEvents (e.g. attack commit) are forwarded
            // to the pooled AudioManager, resolved against the unit
            // presentation host position.
            var audioManagerGO =
                new GameObject(
                    "HeroTestAudioManager");
            var audioManager =
                audioManagerGO.AddComponent<
                    AudioManager>();
            audioManager.SetAssetLoader(
                ClientPresentationServices.Loader);
            audioManager.SetLibrary(
                AssetDatabase
                    .LoadAssetAtPath<AudioLibrary>(
                        "Assets/Config/Formal/AudioLibrary.asset"));
            Debug.Log(
                $"[HeroTestAudio] listener=" +
                $"{FindObjectOfType<AudioListener>() != null}");
            var sfxBridgeGO =
                new GameObject(
                    "HeroTestSfxBridge");
            var sfxBridge =
                sfxBridgeGO.AddComponent<
                    AttackSfxHandler>();
            sfxBridge.SetAudioManager(
                audioManager);
            vfxDispatcher.RegisterSfxHandler(
                sfxBridge);

        }

        private void BuildWorld()
        {
            GlobalGameplayData globalData =
                AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                    "Assets/Config/Formal/GlobalGameplayData.asset");
            if (globalData == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene requires GlobalGameplayData.");
            }
            BakedGlobalGameplayData config =
                globalData.BakeOrThrow();
            outlineRimMaterial =
                AssetDatabase
                    .LoadAssetAtPath<Material>(
                        "Assets/ClientContent/Materials/UnitOutlineRim.mat");
            heroDisplayTable = globalData.HeroDisplayTable;
            EditorMatchContent matchContent =
                LoadEditorMatchContent(config.PrefabTable);
            resolvedPrefabTable = matchContent.PrefabTable;
            var unitCatalog =
                UnitRuntimeCatalogAsset.BakeCombinedOrThrow(
                    matchContent.UnitCatalogs,
                    resolvedPrefabTable,
                    config.TickRate);
            AbilityDefinitionRegistry abilityCatalog =
                AbilityRuntimeCatalogAsset.BakeCombinedOrThrow(
                    matchContent.AbilityCatalogs,
                    config.TickRate);

            physicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = config.UnitGridCellSize,
                },
            };
            EquipmentCatalogAsset equipmentCatalog =
                AssetDatabase
                    .LoadAssetAtPath<EquipmentCatalogAsset>(
                        "Assets/Config/Formal/Equipment/FormalEquipmentCatalog.asset");
            if (equipmentCatalog == null)
            {
                throw new System.InvalidOperationException(
                    "HeroTestScene requires the formal Equipment catalog.");
            }
            equipmentDatabase =
                equipmentCatalog.BakeOrThrow();
            world = new UnitWorld
            {
                PhysicsWorld = physicsWorld,
                GlobalPrefabTable = resolvedPrefabTable,
                UnitPrototypeTable = unitCatalog.UnitPrototypes,
                DisposePolicyTable = unitCatalog.DisposePolicies,
                StatDefinitionTable = unitCatalog.StatDefinitions,
                EquipmentDatabase =
                    equipmentDatabase,
                AbilityDefinitions = abilityCatalog,
                BuffDefinitions =
                    new FrameSyncMoba.Unit.BuffDefinitionRegistry(),
                CrowdControlDefinitions =
                    new FrameSyncMoba.Unit.CrowdControlDefinitionRegistry(),
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
            world.RangeQuery =
                new RangeQueryService(physicsWorld);
            for (int i = 0;
                 i < matchContent.BuffCatalogs.Count;
                 i++)
            {
                matchContent.BuffCatalogs[i].RegisterAll(
                    world.BuffDefinitions,
                    config.TickRate);
            }
            CrowdControlCatalogAsset ccCatalog =
                matchContent.CrowdControlCatalog;
            if (ccCatalog != null &&
                ccCatalog.Definitions != null)
            {
                // The catalog may not have been persisted through the
                // editor Bake step yet; bake in-memory so the runtime
                // registry accepts the definitions (matches CC v6.2 2.6).
                for (int i = 0;
                     i < ccCatalog.Definitions.Length;
                     i++)
                {
                    ccCatalog.Definitions[i]?.Bake();
                }
                ccCatalog.RegisterAll(
                    world.CrowdControlDefinitions);
            }

            combat = new CombatSystem(
                world,
                0,
                0);
            var randomService =
                new DeterministicRandomService(
                    config.RandomSeed);
            var projectileWorld =
                new ProjectileWorld
                {
                    DefRegistry =
                        ProjectileRuntimeCatalogAsset.BakeCombinedOrThrow(
                            matchContent.ProjectileCatalogs,
                            resolvedPrefabTable,
                            config.TickRate),
                    UnitWorld = world,
                    PhysicsWorld = physicsWorld,
                    PrefabTable =
                        resolvedPrefabTable,
                    LogicSecondsPerTick =
                        fp.one / (fp)config.TickRate,
                };
            goldIncome = new GoldIncomeRuntime();
            goldIncome.Initialize(
                1,
                InitialShopGold);
            equipmentShop = new EquipmentShopRuntime();
            equipmentShop.Initialize(
                1,
                equipmentDatabase,
                config.EquipmentSellRate,
                world);
            equipmentShop.ConfigureIncomeView(
                goldIncome);
            pipeline = new SimulationTickPipeline(
                world,
                physicsWorld)
            {
                CombatSystem = combat,
                GoldIncome = goldIncome,
                EquipmentShop = equipmentShop,
                ProjectileWorld = projectileWorld,
                ProjectileHitResolver =
                    new ProjectileHitResolver(
                        physicsWorld,
                        world),
                RandomService =
                    randomService,
                MaxFutureCommandTicks = 12,
            };
            world.CombatSystem = combat;
            world.ProjectileWorld =
                projectileWorld;
            world.RandomService =
                randomService;
        }

        private EditorMatchContent LoadEditorMatchContent(
            GlobalPrefabTable rootTable)
        {
            if (rootTable == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene requires a GlobalPrefabTable.");
            }
            rootTable.ValidateOrThrow();

            var selectedHeroIds = new List<int>();
            AddEditorHeroPartitionIfPresent(
                rootTable,
                heroPrototypeId,
                selectedHeroIds,
                true);
            AddEditorHeroPartitionIfPresent(
                rootTable,
                dummyPrototypeId,
                selectedHeroIds,
                false);
            selectedHeroIds.Sort();
            int mapConfigId =
                ResolveSingleEditorMapConfigId(rootTable);
            IReadOnlyList<GlobalPrefabPartitionReference> references =
                rootTable.SelectPartitions(
                    mapConfigId,
                    selectedHeroIds);
            var tables =
                new List<GlobalPrefabSubTableAsset>(references.Count);
            var resolvedPrefabs =
                new Dictionary<string, GameObject>(
                    StringComparer.Ordinal);
            var content = new EditorMatchContent();

            for (int referenceIndex = 0;
                 referenceIndex < references.Count;
                 referenceIndex++)
            {
                GlobalPrefabPartitionReference reference =
                    references[referenceIndex];
                GlobalPrefabSubTableAsset table =
                    LoadEditorSubTable(reference);
                tables.Add(table);

                IReadOnlyList<PrefabGroup> groups =
                    table.PrefabGroups;
                for (int groupIndex = 0;
                     groupIndex < groups.Count;
                     groupIndex++)
                {
                    IReadOnlyList<PrefabEntry> entries =
                        groups[groupIndex].Entries;
                    for (int entryIndex = 0;
                         entryIndex < entries.Count;
                         entryIndex++)
                    {
                        string address =
                            entries[entryIndex].LogicAssetAddress;
                        if (string.IsNullOrEmpty(address) ||
                            resolvedPrefabs.ContainsKey(address))
                        {
                            continue;
                        }
                        resolvedPrefabs.Add(
                            address,
                            LoadEditorAsset<GameObject>(
                                address,
                                $"{groups[groupIndex].Kind}/" +
                                $"{entries[entryIndex].PrefabId} logic prefab"));
                    }
                }

                IReadOnlyList<MatchContentAssetAddress> assets =
                    table.ContentAssets;
                for (int assetIndex = 0;
                     assetIndex < assets.Count;
                     assetIndex++)
                {
                    MatchContentAssetAddress asset = assets[assetIndex];
                    switch (asset.AssetKind)
                    {
                        case MatchContentAssetKind.UnitRuntimeCatalog:
                            content.UnitCatalogs.Add(
                                LoadEditorAsset<UnitRuntimeCatalogAsset>(
                                    asset.Address,
                                    "UnitRuntimeCatalog"));
                            break;
                        case MatchContentAssetKind.AbilityRuntimeCatalog:
                            content.AbilityCatalogs.Add(
                                LoadEditorAsset<AbilityRuntimeCatalogAsset>(
                                    asset.Address,
                                    "AbilityRuntimeCatalog"));
                            break;
                        case MatchContentAssetKind.ProjectileRuntimeCatalog:
                            content.ProjectileCatalogs.Add(
                                LoadEditorAsset<ProjectileRuntimeCatalogAsset>(
                                    asset.Address,
                                    "ProjectileRuntimeCatalog"));
                            break;
                        case MatchContentAssetKind.BuffCatalog:
                            content.BuffCatalogs.Add(
                                LoadEditorAsset<BuffCatalogAsset>(
                                    asset.Address,
                                    "BuffCatalog"));
                            break;
                        case MatchContentAssetKind.CrowdControlCatalog:
                            content.CrowdControlCatalog =
                                RequireSingleEditorContent(
                                    content.CrowdControlCatalog,
                                    LoadEditorAsset<CrowdControlCatalogAsset>(
                                        asset.Address,
                                        "CrowdControlCatalog"),
                                    asset.AssetKind);
                            break;
                        case MatchContentAssetKind.EquipmentCatalog:
                            content.EquipmentCatalog =
                                RequireSingleEditorContent(
                                    content.EquipmentCatalog,
                                    LoadEditorAsset<EquipmentCatalogAsset>(
                                        asset.Address,
                                        "EquipmentCatalog"),
                                    asset.AssetKind);
                            break;
                        case MatchContentAssetKind.DeterministicMapConfig:
                            LoadEditorAsset<DeterministicMapConfig>(
                                asset.Address,
                                "DeterministicMapConfig");
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"HeroTestScene does not support content asset kind {asset.AssetKind}.");
                    }
                }
            }

            if (content.UnitCatalogs.Count == 0 ||
                content.AbilityCatalogs.Count == 0 ||
                content.ProjectileCatalogs.Count == 0 ||
                content.BuffCatalogs.Count == 0 ||
                content.CrowdControlCatalog == null ||
                content.EquipmentCatalog == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene selected content is missing a required deterministic catalog.");
            }
            content.PrefabTable = rootTable.CreateResolvedRuntimeTable(
                tables,
                resolvedPrefabs);
            return content;
        }

        private static void AddEditorHeroPartitionIfPresent(
            GlobalPrefabTable rootTable,
            int prototypeId,
            List<int> selectedHeroIds,
            bool required)
        {
            bool found = false;
            IReadOnlyList<GlobalPrefabPartitionReference> partitions =
                rootTable.Partitions;
            for (int i = 0; i < partitions.Count; i++)
            {
                GlobalPrefabPartitionReference partition = partitions[i];
                if (partition.PartitionKind ==
                        GlobalPrefabPartitionKind.Hero &&
                    partition.OwnerConfigId == prototypeId)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        $"HeroTestScene has no Hero content partition for hero prototype {prototypeId}.");
                }
                return;
            }
            if (!selectedHeroIds.Contains(prototypeId))
            {
                selectedHeroIds.Add(prototypeId);
            }
        }

        private static int ResolveSingleEditorMapConfigId(
            GlobalPrefabTable rootTable)
        {
            int mapConfigId = 0;
            IReadOnlyList<GlobalPrefabPartitionReference> partitions =
                rootTable.Partitions;
            for (int i = 0; i < partitions.Count; i++)
            {
                GlobalPrefabPartitionReference partition = partitions[i];
                if (partition.PartitionKind !=
                    GlobalPrefabPartitionKind.Map)
                {
                    continue;
                }
                if (mapConfigId != 0 &&
                    mapConfigId != partition.OwnerConfigId)
                {
                    throw new InvalidOperationException(
                        "HeroTestScene requires exactly one map content partition.");
                }
                mapConfigId = partition.OwnerConfigId;
            }
            if (mapConfigId <= 0)
            {
                throw new InvalidOperationException(
                    "HeroTestScene requires a map content partition.");
            }
            return mapConfigId;
        }

        private static GlobalPrefabSubTableAsset LoadEditorSubTable(
            GlobalPrefabPartitionReference reference)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:GlobalPrefabSubTableAsset");
            Array.Sort(guids, StringComparer.Ordinal);
            GlobalPrefabSubTableAsset found = null;
            string foundPath = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GlobalPrefabSubTableAsset candidate =
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        path);
                if (candidate == null ||
                    candidate.PartitionKind != reference.PartitionKind ||
                    candidate.OwnerConfigId != reference.OwnerConfigId)
                {
                    continue;
                }
                if (found != null)
                {
                    throw new InvalidOperationException(
                        $"HeroTestScene found multiple editor child tables for {reference.PartitionKind}/{reference.OwnerConfigId}: " +
                        $"'{foundPath}' and '{path}'.");
                }
                found = candidate;
                foundPath = path;
            }
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"HeroTestScene cannot resolve child table address '{reference.SubTableAddress}'.");
            }
            found.ValidateAgainst(reference);
            return found;
        }

        private static T LoadEditorAsset<T>(
            string path,
            string label)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"HeroTestScene cannot load {label} at '{path}'.");
            }
            return asset;
        }

        private static T RequireSingleEditorContent<T>(
            T current,
            T loaded,
            MatchContentAssetKind kind)
            where T : UnityEngine.Object
        {
            if (current != null)
            {
                throw new InvalidOperationException(
                    $"HeroTestScene selected content defines {kind} more than once.");
            }
            return loaded;
        }

        private void ConfigureTestShop()
        {
            if (hero == null || equipmentShop == null)
            {
                throw new System.InvalidOperationException(
                    "HeroTestScene requires a spawned hero and EquipmentShopRuntime.");
            }
            equipmentShop.GetOrCreateTrader(
                0,
                hero.UnitUid);
            equipmentShop.SetCommandSubmitter(
                GetOrCreatePlayerCommandRequester());
        }

        private void ConfigurePlayerInput()
        {
            if (playerInputController == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene must configure a PlayerInputController " +
                    "reference in the scene.");
            }
            Camera gameplayCamera = followCamera != null
                ? followCamera.GetComponent<Camera>()
                : null;
            if (gameplayCamera == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene must configure a CameraController on " +
                    "the gameplay Camera.");
            }

            MobaCameraPresentationConfig cameraConfig =
                followCamera.PresentationConfig;
            fp pointerGroundY = cameraConfig != null
                ? (fp)cameraConfig.PointerGroundY
                : fp.zero;
            fp pointerPickRadius = cameraConfig != null
                ? (fp)cameraConfig.PointerPickRadius
                : (fp)4;
            var resolver = new MouseWorldResolver(
                gameplayCamera,
                pointerGroundY,
                world,
                pointerPickRadius);
            playerInputController.Initialize(
                new LocalInputEventBuffer(),
                resolver,
                GetOrCreatePlayerCommandRequester());
            playerInputController.SetIndicatorDriver(indicatorDriver);
        }

        private PlayerCommandRequester
            GetOrCreatePlayerCommandRequester()
        {
            if (playerCommandRequester != null)
            {
                return playerCommandRequester;
            }
            if (hero == null || pipeline == null)
            {
                throw new InvalidOperationException(
                    "HeroTestScene must build its world and spawn its hero " +
                    "before composing player commands.");
            }

            hero.ControlledByPlayerSlot = 0;
            playerCommandRequester = new PlayerCommandRequester(
                hero,
                new GameplayInputGate(),
                pipeline.CommandCollector,
                0,
                0,
                new CommandTargetTickResolver(
                    () => pipeline.LocalSimulationTick,
                    () => pipeline.LocalSimulationTick,
                    minCommandLeadTicks: 1,
                    maxFutureCommandTicks:
                        pipeline.MaxFutureCommandTicks),
                AbilityInputMappingProvider.CreateFromAbilityHandler(
                    hero.AbilityHandler),
                new UnitWorldAbilityRuntimeView(world));
            return playerCommandRequester;
        }

        private void BuildMap()
        {
            RebuildGridFromSceneObstacles();
        }

        /// <summary>
        /// Bakes the path grid from the draggable HeroTestObstacle walls in
        /// the scene. The walls themselves are the visible obstacle meshes,
        /// so no separate obstacle visualization is required. Call this after
        /// moving an obstacle to regenerate the grid.
        /// </summary>
        public void RebuildGridFromSceneObstacles()
        {
            if (world == null || physicsWorld == null)
            {
                return;
            }
            var grid = new PathGridMap2D();
            grid.Initialise(
                new fp2(
                    -(fp)mapWidth * (fp)0.5m,
                    -(fp)mapHeight * (fp)0.5m),
                new fp2(
                    (fp)mapWidth * (fp)0.5m,
                    (fp)mapHeight * (fp)0.5m),
                (fp)cellSize);
            var obstacles =
                FindObjectsOfType<HeroTestObstacle>();
            for (int i = 0;
                 i < obstacles.Length;
                 i++)
            {
                HeroTestObstacle obstacle =
                    obstacles[i];
                if (obstacle == null)
                {
                    continue;
                }
                Vector3 position =
                    obstacle.transform.position;
                float radians =
                    obstacle.transform.rotation
                        .eulerAngles.y *
                    Mathf.Deg2Rad;
                Vector2 axisX =
                    new Vector2(
                        Mathf.Cos(radians),
                        -Mathf.Sin(radians));
                Vector2 axisY =
                    new Vector2(
                        -axisX.y,
                        axisX.x);
                Vector2 half =
                    obstacle.Size * 0.5f;
                grid.SetOrientedRectObstruction(
                    new fp2(
                        (fp)position.x,
                        (fp)position.z),
                    new fp2(
                        (fp)axisX.x,
                        (fp)axisX.y),
                    new fp2(
                        (fp)axisY.x,
                        (fp)axisY.y),
                    new fp2(
                        (fp)half.x,
                        (fp)half.y),
                    true,
                    RadiusClass.Medium);
            }
            world.PathGrid = grid;
            world.MovementCollisionResolver =
                new PhysicsCollisionResolver(
                    physicsWorld,
                    grid);
        }

        private void SpawnHero()
        {
            PlayerSpawnPoint spawnPoint =
                FindObjectOfType<
                    PlayerSpawnPoint>();
            Vector2 spawn =
                spawnPoint != null
                    ? new Vector2(
                        spawnPoint.transform
                            .position.x,
                        spawnPoint.transform
                            .position.z)
                    : heroSpawn;
            hero = Spawn(
                heroPrototypeId,
                new TeamId(1),
                new fp2(
                    (fp)spawn.x,
                    (fp)spawn.y));
        }

        private void SpawnDummy()
        {
            SpawnDummyAt(
                new TeamId(2),
                new fp2(
                    (fp)dummySpawn.x,
                    (fp)dummySpawn.y));
        }

        /// <summary>
        /// Auto-detects every DummySpawnPoint marker in the scene and spawns
        /// a punching-bag dummy at each position. Teams alternate by marker
        /// index so team filters and multi-target ability behavior can be
        /// inspected against both friendly and enemy hero units.
        /// </summary>
        private void SpawnDummiesAtScenePoints()
        {
            DummySpawnPoint[] points =
                FindObjectsOfType<
                    DummySpawnPoint>();
            if (points == null ||
                points.Length == 0)
            {
                SpawnDummy();
                return;
            }
            for (int i = 0;
                 i < points.Length;
                 i++)
            {
                TeamId team =
                    i % 2 == 0
                        ? new TeamId(2)
                        : new TeamId(1);
                SpawnDummyAt(
                    team,
                    new fp2(
                        (fp)points[i]
                            .transform.position.x,
                        (fp)points[i]
                            .transform.position.z));
            }
        }

        private void SpawnDummyAt(
            TeamId teamId,
            fp2 position)
        {
            UnitType dummy = Spawn(
                dummyPrototypeId,
                teamId,
                position);
            // Punching-bag: shrink the collision shape so the hero can walk
            // into attack range instead of being pushed out by unit collision.
            if (dummy?.PhysicsEntity != null)
            {
                dummy.PhysicsEntity.SetLogicShape(
                    FrameSyncMoba.Physics.PhysicsShape2D
                        .CreateCircle(
                        default,
                        (fp)0.5m));
            }
            // Punching-bag: disable regen so sustained damage is visible
            // instead of being healed back between attacks.
            if (dummy?.StatHandler != null)
            {
                dummy.StatHandler.SetStat(
                    StatId.HealthRegeneration,
                    fp.zero);
                dummy.StatHandler.SetStat(
                    StatId.CastResourceRegeneration,
                    fp.zero);
            }
            dummies.Add(dummy);
        }

        private UnitType Spawn(
            int prototypeId,
            TeamId teamId,
            fp2 position)
        {
            UnitUid uid = world.SpawnUnit(
                new UnitSpawnRequest(
                    prototypeId,
                    GameplayParticipantId.Explicit(
                        world.GetAllUnits().Count + 1),
                    teamId,
                    position,
                    new fp2(fp.one, fp.zero)));
            world.TryGetUnit(uid, out UnitType unit);
            return unit;
        }

        private void Update()
        {
            if (pipeline == null)
            {
                return;
            }
            HandleDebugInput();
            UpdateUnitRadiusCircles();
            UpdateAttackRangeRing();
            UpdateDummyRespawn();
            UpdateHoverDetection();
            DrawDebugLines();
            RefreshTestHud();

            if (paused)
            {
                lastSimulationMonotonicMilliseconds =
                    FrameSyncLaunchSchedule.SecondsToMilliseconds(
                        Time.realtimeSinceStartupAsDouble);
                return;
            }
            long nowMilliseconds =
                FrameSyncLaunchSchedule.SecondsToMilliseconds(
                    Time.realtimeSinceStartupAsDouble);
            if (lastSimulationMonotonicMilliseconds < 0L)
                lastSimulationMonotonicMilliseconds =
                    nowMilliseconds;
            long elapsedMilliseconds = Math.Max(
                0L,
                nowMilliseconds -
                lastSimulationMonotonicMilliseconds);
            lastSimulationMonotonicMilliseconds =
                nowMilliseconds;
            int effectiveTickRate = Mathf.Max(
                1,
                Mathf.RoundToInt(ticksPerSecond));
            simulationAccumulatorMillisecondRateUnits =
                checked(
                    simulationAccumulatorMillisecondRateUnits +
                    elapsedMilliseconds * effectiveTickRate);
            int guard = 0;
            while (simulationAccumulatorMillisecondRateUnits >= 1000L &&
                   guard++ < 8)
            {
                simulationAccumulatorMillisecondRateUnits -= 1000L;
                pipeline.ExecuteTick(
                    tickController,
                    ExecutionMode.ServerAuthority);
                // Consume deterministic presentation events per Tick so
                // events from earlier Ticks in the same frame survive
                // (VisualEventOutput is cleared at the next Tick start).
                vfxDispatcher?.DispatchCurrentFrame();
            }
            projectileViewBinder?.Reconcile();
        }

        /// <summary>
        /// Draws each unit's logic collision radius as a ground circle so it
        /// is easy to see how much space a unit really occupies.
        /// </summary>
        private void UpdateUnitRadiusCircles()
        {
            var units =
                world.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType unit = units[i];
                if (unit == null ||
                    unit.PhysicsEntity == null)
                {
                    continue;
                }
                fp2 position =
                    unit.PhysicsEntity
                        .Transform2D.Position;
                fp radius =
                    unit.PhysicsEntity
                        .Shape.Radius;
                if (radius <= fp.zero)
                {
                    radius =
                        (fp)0.5m;
                }
                if (!radiusCircles.TryGetValue(
                        unit.UnitUid,
                        out LineRenderer circle) ||
                    circle == null)
                {
                    circle =
                        CreateRadiusCircle(
                            Color.white);
                    radiusCircles[
                        unit.UnitUid] =
                        circle;
                }
                bool hovered =
                    hoveredUnit.HasValue &&
                    hoveredUnit.Value ==
                        unit.UnitUid;
                circle.startColor =
                    hovered
                        ? Color.red
                        : Color.white;
                circle.endColor =
                    hovered
                        ? Color.red
                        : Color.white;
                circle.transform.position =
                    new Vector3(
                        (float)position.x,
                        0.05f,
                        (float)position.y);
                for (int j = 0;
                     j < circle.positionCount;
                     j++)
                {
                    float angle =
                        Mathf.PI * 2f *
                        j /
                        (circle.positionCount - 1);
                    circle.SetPosition(
                        j,
                        new Vector3(
                            Mathf.Cos(angle) *
                                (float)radius,
                            0f,
                            Mathf.Sin(angle) *
                                (float)radius));
                }
            }
        }

        private void UpdateAttackRangeRing()
        {
            if (hero == null ||
                hero.PhysicsEntity == null)
            {
                return;
            }
            if (attackRangeRing == null)
            {
                attackRangeRing =
                    CreateRadiusCircle(
                        new Color(
                            1f,
                            0.85f,
                            0.2f,
                            0.8f));
                attackRangeRing.gameObject.name =
                    "AttackRangeRing";
            }
            fp2 position =
                hero.PhysicsEntity
                    .Transform2D.Position;
            attackRangeRing.transform.position =
                new Vector3(
                    (float)position.x,
                    0.05f,
                    (float)position.y);
            float range =
                (float)hero.AttackHandler
                    .CurrentAttackRange;
            for (int j = 0;
                 j < attackRangeRing.positionCount;
                 j++)
            {
                float angle =
                    Mathf.PI * 2f *
                    j /
                    (attackRangeRing.positionCount - 1);
                attackRangeRing.SetPosition(
                    j,
                    new Vector3(
                        Mathf.Cos(angle) *
                            range,
                        0f,
                        Mathf.Sin(angle) *
                            range));
            }
        }

        private static LineRenderer
            CreateRadiusCircle(
                Color color)
        {
            var go =
                new GameObject(
                    "UnitRadiusCircle");
            LineRenderer line =
                go.AddComponent<
                    LineRenderer>();
            line.useWorldSpace =
                false;
            line.positionCount =
                33;
            line.startWidth =
                0.06f;
            line.endWidth =
                0.06f;
            line.startColor =
                color;
            line.endColor =
                color;
            var material =
                new Material(
                    Shader.Find(
                        "MOBA/TestObstacle"));
            material.SetColor(
                "_Color",
                Color.white);
            line.material =
                material;
            return line;
        }

        /// <summary>
        /// Auto-respawns the dummy at its spawn point after
        /// configured millisecond delay. Local test convenience, not
        /// frame-synced.
        /// </summary>
        private void UpdateDummyRespawn()
        {
            UnitType deadDummy = null;
            for (int i = 0;
                 i < dummies.Count;
                 i++)
            {
                UnitType dummy = dummies[i];
                if (dummy == null)
                {
                    continue;
                }
                if (dummy.LifeState ==
                        LifeState.Dead ||
                    (dummy.LifeState ==
                         LifeState.Dying &&
                     dummy.StatHandler != null &&
                     dummy.StatHandler
                         .CurrentHealth <=
                     fp.zero))
                {
                    deadDummy = dummy;
                    break;
                }
            }
            if (deadDummy == null)
            {
                dummyRespawnDeadlineMilliseconds = -1L;
                return;
            }
            long nowMilliseconds =
                FrameSyncLaunchSchedule.SecondsToMilliseconds(
                    Time.realtimeSinceStartupAsDouble);
            if (dummyRespawnDeadlineMilliseconds < 0L)
            {
                dummyRespawnDeadlineMilliseconds = checked(
                    nowMilliseconds +
                    Math.Max(0, dummyRespawnMilliseconds));
            }
            if (nowMilliseconds >=
                dummyRespawnDeadlineMilliseconds)
            {
                RespawnDummy(deadDummy);
                dummyRespawnDeadlineMilliseconds = -1L;
            }
        }

        private void RespawnDummy(UnitType dummy)
        {
            if (dummy == null || world == null)
            {
                return;
            }
            if (dummy.LifeState ==
                LifeState.Dying)
            {
                // The death settlement may already have run (leaving an
                // orphan Dying state); force the formal transition so the
                // respawn lifecycle below is valid.
                world.ConfirmUnitDeath(dummy);
            }
            world.BeginRespawn(dummy);
            dummy.StatHandler?.SetCurrentHealth(
                dummy.StatHandler.GetStat(
                    StatId.MaxHealth));
            dummy.StatHandler
                ?.SetCurrentCastResource(
                    dummy.StatHandler.GetStat(
                        StatId.MaxCastResource));
            dummy.MovementHandler
                ?.ForceSetPosition(
                    new fp2(
                        (fp)dummySpawn.x,
                        (fp)dummySpawn.y));
            world.CompleteRespawn(dummy);
        }

        private void BindTestHudBridge()
        {
            UnitType Local() => hero;

        GameFlowLuaBridge.GetLocalHp =
                () => UIDisplayConvert.ResourceInt(
                    Local()?.StatHandler?.CurrentHealth ??
                    fp.zero);
            GameFlowLuaBridge.GetLocalMaxHp =
                () => UIDisplayConvert.ResourceInt(
                    Local()?.StatHandler?.GetStat(
                        StatId.MaxHealth) ??
                    fp.zero);
            GameFlowLuaBridge.GetLocalResource =
                () => UIDisplayConvert.ResourceInt(
                    Local()?.StatHandler
                        ?.CurrentCastResource ??
                    fp.zero);
            GameFlowLuaBridge.GetLocalMaxResource =
                () => UIDisplayConvert.ResourceInt(
                    Local()?.StatHandler?.GetStat(
                        StatId.MaxCastResource) ??
                    fp.zero);
            GameFlowLuaBridge.GetLocalLevel =
                () => Local()?.StatHandler?.Level ?? 1;
            GameFlowLuaBridge.GetLocalExp =
                () => Local()?.StatHandler
                    ?.CurrentExperience ?? 0;
            GameFlowLuaBridge.GetLocalNextLevelExp =
                () => Local()?.StatHandler
                    ?.ExperienceRequiredForNextLevel ?? 100;
            // Skill-point / ability-level UI (design v15.2 1.12). The test
            // scene builds its own world, so the bridge must be wired here
            // just like GameBootstrap wires it in the full match flow.
            GameFlowLuaBridge.GetLocalPendingSkillPoints =
                () => Local()?.AbilityHandler
                    ?.PendingSkillPoints ?? 0;
            GameFlowLuaBridge.GetLocalAbilityLevel =
                slot => Local()?.AbilityHandler
                    ?.GetAbilityLevel((byte)slot) ?? 0;
            GameFlowLuaBridge.GetLocalAbilityIsUltimate =
                slot => Local()?.AbilityHandler
                    ?.IsUltimateSlot((byte)slot) ?? false;
            GameFlowLuaBridge.CanAllocateLocalSkillPoint =
                slot => Local()?.AbilityHandler
                    ?.CanAllocateSkillPoint((byte)slot) ?? false;
            GameFlowLuaBridge.AllocateLocalSkillPoint =
                slot =>
                {
                    if (Local()?.AbilityHandler == null)
                    {
                        return;
                    }
                    SubmitAllocateSkillPoint(
                        (byte)slot);
                };
            GameFlowLuaBridge.DebugLevelUpLocal =
                GrantDebugLevel;
            GameFlowLuaBridge.IsExpandStatsHeld =
                () => PresentationInputState
                    .ExpandStatsHeld;
            GameFlowLuaBridge.GetCooldownRemaining =
                slot =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler != null
                        ? unit.AbilityHandler
                            .GetDisplayCooldownRemainingTicks(
                                (byte)slot,
                                CurrentTick)
                        : 0;
                };
            GameFlowLuaBridge.GetCooldownTotal =
                slot =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler != null
                        ? unit.AbilityHandler
                            .GetDisplayCooldownTotalTicks(
                                (byte)slot)
                        : 0;
                };
            GameFlowLuaBridge.GetCooldownRemainingSeconds =
                slot =>
                {
                    UnitType unit = Local();
                    if (unit?.AbilityHandler == null)
                    {
                        return 0f;
                    }
                    int remaining =
                        unit.AbilityHandler
                            .GetDisplayCooldownRemainingTicks(
                                (byte)slot,
                                CurrentTick);
                    return remaining *
                        (1f / Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                ticksPerSecond)));
                };
            GameFlowLuaBridge.GetActiveAbilityId =
                slot =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler
                        ?.GetAbilityDef((byte)slot)
                        ?.AbilityId ?? 0;
                };
            GameFlowLuaBridge.GetActiveAbilityIcon =
                slot =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler
                        ?.GetActiveRuntime((byte)slot)
                        ?.GetCurrentIconAddress() is string address
                            ? ClientSpriteRegistry.Resolve(address)
                            : null;
                };
            GameFlowLuaBridge.GetPassiveAbilityIcon =
                () =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler
                        ?.FixedPassive
                        ?.GetCurrentIconAddress() is string address
                            ? ClientSpriteRegistry.Resolve(address)
                            : null;
                };
            GameFlowLuaBridge.GetLocalHeroAvatar =
                () =>
                {
                    if (heroDisplayTable == null ||
                        !heroDisplayTable.TryGetByPrototypeId(
                            heroPrototypeId,
                            out HeroDisplayEntry entry))
                    {
                        return null;
                    }
                    return ClientSpriteRegistry.Resolve(
                        entry.AvatarAddress);
                };
            GameFlowLuaBridge.GetHudGold =
                () => equipmentShop
                    ?.GetCurrentAvailableGold(0) ?? 0;
            // Local test scene has no network sync; keep the Ping label
            // hidden (value -1). The real client binds a live RTT instead.
            GameFlowLuaBridge.GetLocalPing =
                () => -1;
            GameFlowLuaBridge.CloseShop =
                () =>
                {
                    UIManager uiManager =
                        UIManager.Instance;
                    if (uiManager != null)
                    {
                        uiManager.HideOverlay(
                            UIPageId.Shop);
                    }
                };
            GameFlowLuaBridge.GetGameElapsedSeconds =
                () => (float)CurrentTick /
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            ticksPerSecond));
            GameFlowLuaBridge.GetBlueTeamScore =
                () => 0;
            GameFlowLuaBridge.GetRedTeamScore =
                () => 0;
            GameFlowLuaBridge.GetLocalCreepScore =
                () => 0;
            GameFlowLuaBridge.GetLocalKills =
                () => 0;
            GameFlowLuaBridge.GetLocalDeaths =
                () => 0;
            GameFlowLuaBridge.GetLocalAssists =
                () => 0;
            GameFlowLuaBridge.GetLocalStatValue =
                statId =>
                {
                    UnitType unit = Local();
                    if (unit?.StatHandler == null)
                    {
                        return 0;
                    }
                    fp value =
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
            GameFlowLuaBridge.GetLocalStatText =
                statName =>
                    FormatStatText(
                        Local(),
                        statName);
            GameFlowLuaBridge
                .GetLocalEquipmentSlotCount =
                () => EquipmentHandler.SlotCount;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotId =
                slot => hero?.EquipmentHandler
                    ?.GetSlotDef(slot)?.Id ?? 0;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotName =
                slot => hero?.EquipmentHandler
                    ?.GetSlotDef(slot)?.Name ?? "";
            GameFlowLuaBridge
                .GetLocalEquipmentSlotStack =
                slot => hero?.EquipmentHandler
                    ?.GetSlot(slot)?.StackCount ?? 0;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotIcon =
                slot => ClientSpriteRegistry.Resolve(
                    hero?.EquipmentHandler
                        ?.GetSlotDef(slot)?.IconAddress);
            GameFlowLuaBridge.FocusShopEquipment =
                (_, __) =>
                {
                    UIManager uiManager =
                        UIManager.Instance;
                    if (uiManager != null &&
                        uiManager.IsOpen(UIPageId.HUD))
                    {
                        uiManager.ShowOverlay(
                            UIPageId.Shop);
                    }
                };
            GameFlowLuaBridge
                .GetPassiveCooldownRemainingSeconds =
                () =>
                {
                    UnitType unit = Local();
                    PassiveAbilityRuntime passive =
                        unit?.AbilityHandler?.FixedPassive;
                    if (passive == null)
                        return 0f;
                    int remaining =
                        passive.EffectRuntime.State
                            .NextReadyLogicTick - CurrentTick;
                    return Mathf.Max(0, remaining) *
                        (1f / Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                ticksPerSecond)));
                };
            GameFlowLuaBridge
                .GetPassiveCooldownTotalSeconds =
                () =>
                {
                    UnitType unit = Local();
                    PassiveAbilityRuntime passive =
                        unit?.AbilityHandler?.FixedPassive;
                    if (passive == null ||
                        unit?.StatHandler == null)
                    {
                        return 0f;
                    }
                    int ticks = passive.Definition
                        .GetCooldownTicks(
                            unit.StatHandler.Level);
                    return ticks *
                        (1f / Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                ticksPerSecond)));
                };
            GameFlowLuaBridge.GetLocalBuffCount =
                () => Local()?.BuffHandler
                    ?.GetAllOrdered()?.Count ?? 0;
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
                    BuffRuntime buff = BuffAt(index);
                    if (buff == null ||
                        buff.IsPermanent)
                    {
                        return 0f;
                    }
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

            // ---- Shop (formal runtime, local Tick command submission) ----

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
                    {
                        return 0;
                    }
                    return equipmentShop
                        ?.CalculatePurchasePrice(
                            0,
                            defs[index].Id) ??
                        defs[index].Value;
                };
            GameFlowLuaBridge.GetShopItemNameById =
                equipmentId =>
                {
                    var def =
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    return def?.Name ?? "";
                };
            GameFlowLuaBridge.GetShopItemPriceById =
                equipmentId =>
                {
                    var def =
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    return def == null
                        ? 0
                        : equipmentShop
                            ?.CalculatePurchasePrice(
                                0,
                                equipmentId) ??
                            def.Value;
                };
            GameFlowLuaBridge.GetShopItemEffectById =
                equipmentId =>
                {
                    var def =
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    if (def?.Effects == null)
                    {
                        return "";
                    }
                    var parts =
                        new List<string>();
                    for (int i = 0;
                         i < def.Effects.Length;
                         i++)
                    {
                        var effect =
                            def.Effects[i];
                        if (effect == null ||
                            string.IsNullOrEmpty(
                                effect.Name))
                        {
                            continue;
                        }
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
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    if (def?.BakedFixedStats == null)
                    {
                        return "";
                    }
                    var parts =
                        new List<string>();
                    for (int i = 0;
                         i < def.BakedFixedStats.Length;
                         i++)
                    {
                        var stat =
                            def.BakedFixedStats[i];
                        parts.Add(
                            $"{stat.Stat} +" +
                            $"{(float)stat.Value}");
                    }
                    return string.Join(
                        ", ",
                        parts);
                };
            GameFlowLuaBridge.GetCurrentGold =
                () => equipmentShop
                    ?.GetCurrentAvailableGold(0) ?? 0;
            GameFlowLuaBridge.CanUndo =
                () => equipmentShop != null &&
                    equipmentShop.CanUndo(
                        0,
                        equipmentShop
                            .GetCurrentAvailableGold(0),
                        out _);
            GameFlowLuaBridge.RequestPurchase =
                equipmentId =>
                {
                    EquipmentShopRequestCheck check =
                        equipmentShop.RequestPurchase(
                            0,
                            equipmentId);
                    shopStatus = check.Allowed
                        ? ""
                        : check.FailureReason
                            .ToString();
                };
            GameFlowLuaBridge.RequestSell =
                slot =>
                {
                    EquipmentShopRequestCheck check =
                        equipmentShop.RequestSell(
                            0,
                            slot);
                    shopStatus = check.Allowed
                        ? ""
                        : check.FailureReason
                            .ToString();
                };
            GameFlowLuaBridge.RequestUndo =
                () =>
                {
                    EquipmentShopRequestCheck check =
                        equipmentShop.RequestUndo(0);
                    shopStatus = check.Allowed
                        ? ""
                        : check.FailureReason
                            .ToString();
                };
            GameFlowLuaBridge.IsEquipmentOwned =
                equipmentId =>
                {
                    var def =
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    return hero?.EquipmentHandler
                        ?.HasDefinition(def) ?? false;
                };
            GameFlowLuaBridge.GetShopStatus =
                () => shopStatus;
        }

        private BuffRuntime BuffAt(int index)
        {
            var buffs = hero?.BuffHandler
                ?.GetAllOrdered();
            return buffs != null &&
                index >= 0 &&
                index < buffs.Count
                    ? buffs[index]
                    : null;
        }

        private static string FormatStatText(
            UnitType unit,
            string statName)
        {
            if (unit?.StatHandler == null)
            {
                return "0";
            }
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
                        Get(StatId.HealthRegeneration))
                        .ToString();
                case "HealAndShieldPower":
                    return UIDisplayConvert.PercentInt(
                        Get(StatId.HealPower)) +
                        "%";
                case "ArmorPenetration":
                    return UIDisplayConvert.StatInt(
                        Get(StatId.FlatArmorPenetration)) +
                        "|" +
                        UIDisplayConvert.PercentInt(
                            Get(StatId
                                .ArmorPenetrationRatio)) +
                        "%";
                case "MagicPenetration":
                    return UIDisplayConvert.StatInt(
                        Get(StatId.FlatMagicPenetration)) +
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
        }

        private void RefreshTestHud()
        {
            UIManager uiManager =
                UIManager.Instance;
            if (uiManager == null ||
                !uiManager.IsOpen(UIPageId.HUD))
            {
                return;
            }
            uiManager.RefreshLuaHost(
                UIPageId.HUD);
            if (uiManager.IsOpen(UIPageId.Shop))
            {
                uiManager.RefreshLuaHost(
                    UIPageId.Shop);
            }
        }

        /// <summary>
        /// Per-frame hover detection: the ground point under the cursor is
        /// resolved on the main thread (Camera access), while the nearest
        /// unit inside the pick radius is computed asynchronously on a
        /// background task from a pure-data snapshot. The result drives the
        /// ally-green / enemy-red outline highlight. Presentation only.
        /// </summary>
        private void UpdateHoverDetection()
        {
            if (hero == null || world == null)
            {
                return;
            }

            if (hoverTask != null &&
                hoverTask.IsCompleted)
            {
                try
                {
                    hoveredUnit =
                        ResolveHoverByScreenDistance(
                            hoverTask.Result);
                }
                catch
                {
                    hoveredUnit = null;
                }
                hoverTask = null;
                ApplyHoverHighlight();
            }

            if (hoverTask != null)
            {
                return;
            }
            fp2? ground =
                ScreenToGround(
                    Input.mousePosition);
            if (!ground.HasValue)
            {
                return;
            }

            var snapshots =
                new List<HoverUnitSnapshot>();
            var units =
                world.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType unit = units[i];
                if (unit == null ||
                    unit.PhysicsEntity == null ||
                    unit.LifeState !=
                        LifeState.Alive)
                {
                    continue;
                }
                fp2 position =
                    unit.PhysicsEntity
                        .Transform2D.Position;
                snapshots.Add(
                    new HoverUnitSnapshot
                    {
                        Uid = unit.UnitUid,
                        X = (float)position.x,
                        Y = (float)position.y,
                        Alive = true,
                        IsFriendly =
                            unit.TeamId ==
                            hero.TeamId,
                    });
            }
            float groundX =
                (float)ground.Value.x;
            float groundY =
                (float)ground.Value.y;
            hoverTask = Task.Run(
                () => ComputeHoveredUnit(
                    groundX,
                    groundY,
                    snapshots));
        }

        /// <summary>
        /// Precise screen-space refinement on the main thread: projects the
        /// async ground candidates back to screen pixels and keeps the unit
        /// closest to the cursor. This makes hovering the (tall) unit model
        /// reliable even though the ground point lands offset from its base.
        /// </summary>
        private UnitUid?
            ResolveHoverByScreenDistance(
                List<UnitUid> candidates)
        {
            Vector2 mouse =
                Input.mousePosition;
            Camera camera =
                Camera.main;
            if (camera == null)
            {
                return null;
            }
            const float MaxScreenDistance = 90f;
            UnitUid best = default;
            float bestDistance =
                float.MaxValue;
            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                if (!world.TryGetUnit(
                        candidates[i],
                        out UnitType unit) ||
                    unit?.PhysicsEntity == null)
                {
                    continue;
                }
                // Project the visible model center instead of the logic
                // ground point: the cursor usually hovers the upper part of
                // the tall unit model, so a ground-projected point sits too
                // far below the cursor to match.
                Vector3 worldPosition =
                    unit.transform.position;
                var renderer =
                    unit.GetComponentInChildren<
                        Renderer>(true);
                if (renderer != null)
                {
                    worldPosition =
                        renderer.bounds.center;
                }
                Vector2 screen =
                    camera.WorldToScreenPoint(
                        worldPosition);
                float distance =
                    Vector2.Distance(
                        screen,
                        mouse);
                if (distance < bestDistance)
                {
                    bestDistance =
                        distance;
                    best = unit.UnitUid;
                }
            }
            return best.IsValid() &&
                bestDistance <=
                    MaxScreenDistance
                    ? best
                    : (UnitUid?)null;
        }

        private static List<UnitUid>
            ComputeHoveredUnit(
                float groundX,
                float groundY,
                List<HoverUnitSnapshot> units)
        {
            const float PickRadius = 8f;
            const float PickRadiusSq =
                PickRadius * PickRadius;
            var candidates =
                new List<UnitUid>();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                HoverUnitSnapshot unit =
                    units[i];
                if (!unit.Alive)
                {
                    continue;
                }
                float dx =
                    unit.X - groundX;
                float dy =
                    unit.Y - groundY;
                float sq =
                    dx * dx + dy * dy;
                if (sq > PickRadiusSq)
                {
                    continue;
                }
                candidates.Add(unit.Uid);
            }
            return candidates;
        }

        private void ApplyHoverHighlight()
        {
            var units =
                world.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType unit = units[i];
                if (unit == null ||
                    unit.LifeState !=
                        LifeState.Alive)
                {
                    continue;
                }
                ClientUnitOutline outline =
                    GetOrCreateOutline(
                        unit);
                if (outline == null)
                {
                    continue;
                }
                bool hovered =
                    hoveredUnit.HasValue &&
                    hoveredUnit.Value ==
                        unit.UnitUid;
                if (!hovered)
                {
                    outline.SetHighlighted(
                        false,
                        Color.white);
                    continue;
                }
                bool friendly =
                    unit.TeamId ==
                    hero.TeamId;
                outline.SetHighlighted(
                    true,
                    friendly
                        ? Color.green
                        : Color.red);
            }
        }

        private ClientUnitOutline GetOrCreateOutline(
            UnitType unit)
        {
            if (outlines.TryGetValue(
                    unit.UnitUid,
                    out ClientUnitOutline existing) &&
                existing != null)
            {
                return existing;
            }
            ClientUnitOutline outline =
                unit.GetComponentInChildren<
                    ClientUnitOutline>();
            if (outline == null)
            {
                // Fallback for prefabs that do not carry the component yet.
                outline =
                    unit.gameObject.AddComponent<
                        ClientUnitOutline>();
            }
            if (outline.OutlineMaterial == null &&
                outlineRimMaterial != null)
            {
                outline.OutlineMaterial =
                    outlineRimMaterial;
            }
            outlines[unit.UnitUid] =
                outline;
            return outline;
        }

        /// <summary>
        /// Finds or builds the generic 2D skill indicator driver used by the
        /// formal PlayerInputController.
        /// </summary>
        private async Task EnsureIndicatorDriverAsync()
        {
            if (indicatorDriver != null)
            {
                return;
            }
            indicatorDriver =
                FindObjectOfType<
                    SkillIndicatorDriver>();
            if (indicatorDriver == null)
            {
                var holder =
                    new GameObject(
                        "HeroTestSkillIndicators");
                indicatorDriver =
                    holder.AddComponent<
                        SkillIndicatorDriver>();
                IClientPresentationAssetLoader loader =
                    await ClientPresentationServices.GetLoaderAsync();
                IPresentationAssetLease<GameObject> direction =
                    await loader.AcquirePrefabAsync(
                        "ui/indicator/direction",
                        CancellationToken.None);
                IPresentationAssetLease<GameObject> range =
                    await loader.AcquirePrefabAsync(
                        "ui/indicator/range-circle",
                        CancellationToken.None);
                IPresentationAssetLease<GameObject> ground =
                    await loader.AcquirePrefabAsync(
                        "ui/indicator/ground-target",
                        CancellationToken.None);
                presentationLeases.Add(direction);
                presentationLeases.Add(range);
                presentationLeases.Add(ground);
                indicatorDriver.Configure(
                    direction.Asset,
                    range.Asset,
                    ground.Asset);
            }
        }

        /// <summary>
        /// D-048 split the render/Animator/outline tree into Addressable
        /// client views. Bind the spawned hero and dummies to their views so
        /// the scene still renders models, animates them and supports hover
        /// outlines, mirroring ClientUnitViewBinder.
        /// </summary>
        private async Task BindPresentationViewsAsync()
        {
            IClientPresentationAssetLoader loader =
                await ClientPresentationServices.GetLoaderAsync();
            await BindViewForUnitAsync(
                hero,
                loader);
            for (int i = 0;
                 i < dummies.Count;
                 i++)
            {
                await BindViewForUnitAsync(
                    dummies[i],
                    loader);
            }
            projectileViewBinder?.Dispose();
            projectileViewBinder =
                new ClientProjectileViewBinder(
                    world.ProjectileWorld,
                    world.GlobalPrefabTable,
                    loader);
        }

        private async Task BindViewForUnitAsync(
            UnitType unit,
            IClientPresentationAssetLoader loader)
        {
            if (unit == null ||
                !unit.UnitUid.IsValid())
            {
                return;
            }
            if (!world.GlobalPrefabTable.TryGetEntry(
                    PrefabKind.Unit,
                    unit.UnitUid.RuntimeEntityPrefabId,
                    out PrefabEntry entry) ||
                string.IsNullOrEmpty(entry.ClientViewAddress))
            {
                return;
            }

            IPresentationAssetLease<GameObject> lease = null;
            GameObject instance = null;
            try
            {
                lease = await loader.AcquirePrefabAsync(
                    entry.ClientViewAddress,
                    CancellationToken.None);
                if (unit == null)
                {
                    lease.Dispose();
                    return;
                }
                instance = Instantiate(
                    lease.Asset,
                    unit.transform,
                    false);
                instance.name =
                    $"HeroTestView_{unit.UnitUid}";
                UnitPresentationHost host =
                    instance.GetComponent<
                        UnitPresentationHost>();
                if (host == null)
                {
                    Destroy(instance);
                    instance = null;
                    lease.Dispose();
                    lease = null;
                    return;
                }
                host.Bind(unit);
                presentationViewInstances.Add(
                    instance);
                presentationLeases.Add(lease);
                instance = null;
                lease = null;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[HeroTest] View bind failed for " +
                    $"uid={unit?.UnitUid}: {exception}");
            }
            finally
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
                lease?.Dispose();
            }
        }

        private void OnDestroy()
        {
            projectileViewBinder?.Dispose();
            projectileViewBinder = null;
            if (resolvedPrefabTable != null)
            {
                Destroy(resolvedPrefabTable);
                resolvedPrefabTable = null;
            }
            for (int i = 0;
                 i < presentationViewInstances.Count;
                 i++)
            {
                if (presentationViewInstances[i] !=
                    null)
                {
                    Destroy(
                        presentationViewInstances[i]);
                }
            }
            presentationViewInstances.Clear();
            for (int i = 0; i < presentationLeases.Count; i++)
                presentationLeases[i].Dispose();
            presentationLeases.Clear();
        }

        /// <summary>
        /// Editor/runtime debug visualization visible in the Game view
        /// (with Gizmos enabled): grid cells, obstacle cells and the current
        /// hero A* route.
        /// </summary>
        private void DrawDebugLines()
        {
            if (world?.PathGrid == null)
            {
                return;
            }
            PathGridMap2D grid = world.PathGrid;
            fp2 min = grid.WorldMin;
            fp2 max = grid.WorldMax;
            float fxMin = (float)min.x;
            float fzMin = (float)min.y;
            float fxMax = (float)max.x;
            float fzMax = (float)max.y;
            float cell = (float)cellSize;

            // Map boundary.
            Debug.DrawLine(
                new Vector3(fxMin, 0.05f, fzMin),
                new Vector3(fxMax, 0.05f, fzMin),
                new Color(1f, 1f, 1f, 0.7f));
            Debug.DrawLine(
                new Vector3(fxMax, 0.05f, fzMin),
                new Vector3(fxMax, 0.05f, fzMax),
                new Color(1f, 1f, 1f, 0.7f));
            Debug.DrawLine(
                new Vector3(fxMax, 0.05f, fzMax),
                new Vector3(fxMin, 0.05f, fzMax),
                new Color(1f, 1f, 1f, 0.7f));
            Debug.DrawLine(
                new Vector3(fxMin, 0.05f, fzMax),
                new Vector3(fxMin, 0.05f, fzMin),
                new Color(1f, 1f, 1f, 0.7f));

            // Sparse grid lines (every other cell keeps the view readable).
            int lineStep = Mathf.Max(1, Mathf.RoundToInt(cell * 2f));
            var gridColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
            for (int x = 0; x <= grid.Width; x += lineStep)
            {
                float wx = fxMin + x * cell;
                Debug.DrawLine(
                    new Vector3(wx, 0.05f, fzMin),
                    new Vector3(wx, 0.05f, fzMax),
                    gridColor);
            }
            for (int z = 0; z <= grid.Height; z += lineStep)
            {
                float wz = fzMin + z * cell;
                Debug.DrawLine(
                    new Vector3(fxMin, 0.05f, wz),
                    new Vector3(fxMax, 0.05f, wz),
                    gridColor);
            }

            // Obstacle cells.
            bool[] walkable = grid.GetWalkableLayer(
                RadiusClass.Medium);
            if (walkable != null)
            {
                var obstacleColor =
                    new Color(1f, 0.25f, 0.25f, 0.9f);
                for (int cy = 0; cy < grid.Height; cy++)
                {
                    for (int cx = 0; cx < grid.Width; cx++)
                    {
                        if (walkable[cy * grid.Width + cx])
                        {
                            continue;
                        }
                        fp2 c = grid.CellToWorld(cx, cy);
                        float ox = (float)c.x - cell * 0.5f;
                        float oz = (float)c.y - cell * 0.5f;
                        Debug.DrawLine(
                            new Vector3(ox, 0.06f, oz),
                            new Vector3(ox + cell, 0.06f, oz),
                            obstacleColor);
                        Debug.DrawLine(
                            new Vector3(ox + cell, 0.06f, oz),
                            new Vector3(ox + cell, 0.06f, oz + cell),
                            obstacleColor);
                        Debug.DrawLine(
                            new Vector3(ox + cell, 0.06f, oz + cell),
                            new Vector3(ox, 0.06f, oz + cell),
                            obstacleColor);
                        Debug.DrawLine(
                            new Vector3(ox, 0.06f, oz + cell),
                            new Vector3(ox, 0.06f, oz),
                            obstacleColor);
                    }
                }
            }

            // Hero A* route.
            if (hero != null &&
                hero.Locomotion != null &&
                hero.Locomotion.Route.AStarPathCellIndices != null)
            {
                int[] cells =
                    hero.Locomotion.Route.AStarPathCellIndices;
                var routeColor =
                    new Color(0.2f, 1f, 0.2f, 0.95f);
                Vector3? previous = null;
                for (int i = 0; i < cells.Length; i++)
                {
                    int index = cells[i];
                    int cx = index % grid.Width;
                    int cy = index / grid.Width;
                    fp2 c = grid.CellToWorld(cx, cy);
                    Vector3 current = new Vector3(
                        (float)c.x + cell * 0.5f,
                        0.07f,
                        (float)c.y + cell * 0.5f);
                    if (previous.HasValue)
                    {
                        Debug.DrawLine(
                            previous.Value,
                            current,
                            routeColor);
                    }
                    previous = current;
                }
            }
        }

        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                paused = !paused;
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                pipeline.ExecuteTick(
                    tickController,
                    ExecutionMode.ServerAuthority);
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                ticksPerSecond =
                    Mathf.Min(300f,
                        ticksPerSecond * 2f);
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                ticksPerSecond =
                    Mathf.Max(1f,
                        ticksPerSecond * 0.5f);
            }
            if (Input.GetKeyDown(KeyCode.T) &&
                dummies.Count > 0)
            {
                GetOrCreatePlayerCommandRequester()
                    .RequestAttack(dummies[0].UnitUid);
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                GrantDebugLevel();
            }
        }

        private void DamageHero100()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            hero.StatHandler.SetCurrentHealth(
                hero.StatHandler.CurrentHealth -
                (fp)100);
        }

        private void HealHero100()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            hero.StatHandler.SetCurrentHealth(
                hero.StatHandler.CurrentHealth +
                (fp)100);
        }

        private void RestoreMana100()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            hero.StatHandler.SetCurrentCastResource(
                hero.StatHandler
                    .CurrentCastResource +
                (fp)100);
        }

        private void DrainMana100()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            hero.StatHandler.SetCurrentCastResource(
                hero.StatHandler
                    .CurrentCastResource -
                (fp)100);
        }

        private void ResetDummies()
        {
            for (int i = 0;
                 i < dummies.Count;
                 i++)
            {
                if (dummies[i] == null)
                {
                    continue;
                }
                dummies[i].StatHandler
                    .SetCurrentHealth(
                        dummies[i].StatHandler
                            .GetStat(
                                StatId.MaxHealth));
            }
        }

        private void RefillMana()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            hero.StatHandler.SetCurrentCastResource(
                hero.StatHandler.GetStat(
                    StatId.MaxCastResource));
        }

        private void ResetCooldowns()
        {
            if (hero?.AbilityHandler == null)
            {
                return;
            }
            for (byte slot = 0;
                 slot < 4;
                 slot++)
            {
                hero.AbilityHandler
                    .GetActiveRuntime(slot)
                    ?.ResetCooldown(CurrentTick);
            }
        }

        private void SubmitAllocateSkillPoint(
            byte slot)
        {
            GetOrCreatePlayerCommandRequester()
                .RequestAllocateAbilitySkillPoint(slot);
        }

        private void GrantDebugLevel()
        {
            if (hero?.StatHandler == null)
            {
                return;
            }
            int required =
                hero.StatHandler
                    .ExperienceRequiredForNextLevel;
            if (required > 0)
            {
                hero.StatHandler
                    .AddExperience(required);
                Debug.Log(
                    $"[HeroTest] LevelUp -> " +
                    $"{hero.StatHandler.Level} " +
                    $"pendingPoints=" +
                    $"{hero.AbilityHandler?.PendingSkillPoints}");
            }
        }

        private fp2? ScreenToGround(
            Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }
            Ray ray = camera.ScreenPointToRay(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    0f));
            var plane = new Plane(
                Vector3.up,
                Vector3.zero);
            if (!plane.Raycast(
                    ray,
                    out float enter))
            {
                return null;
            }
            Vector3 hit = ray.GetPoint(enter);
            return new fp2(
                (fp)hit.x,
                (fp)hit.z);
        }

        private void OnGUI()
        {
            try
            {
                GUILayout.BeginArea(
                    new Rect(10f, 10f, 360f, 460f));
                GUILayout.Label(
                    "Hero Test (local ticks, no frame-sync)");
                GUILayout.Label(
                    "Tick " + CurrentTick +
                    "  paused=" + paused);
                if (hero != null)
                {
                    fp2 pos =
                        hero.PhysicsEntity
                            .Transform2D.Position;
                    GUILayout.Label(
                        "Hero pos=(" +
                        ((float)pos.x).ToString("F1") + "," +
                        ((float)pos.y).ToString("F1") + ")");
                    GUILayout.Label(
                        "Hero HP=" +
                        hero.StatHandler.CurrentHealth +
                        "/" +
                        hero.StatHandler.GetStat(
                            StatId.MaxHealth));
                    GUILayout.Label(
                        "Hero AD=" +
                        hero.StatHandler.GetStat(
                            StatId.AttackDamage) +
                        "  Range=" +
                        hero.AttackHandler
                            .CurrentAttackRange);
                }
                if (dummies.Count > 0 &&
                    dummies[0] != null)
                {
                    GUILayout.Label(
                        "Dummy HP=" +
                        dummies[0].StatHandler
                            .CurrentHealth +
                        "/" +
                        dummies[0].StatHandler
                            .GetStat(
                                StatId.MaxHealth));
                }
                GUILayout.Space(8f);
                if (GUILayout.Button(
                        "扣100血"))
                {
                    DamageHero100();
                }
                if (GUILayout.Button(
                        "加100血"))
                {
                    HealHero100();
                }
                if (GUILayout.Button(
                        "重置技能CD"))
                {
                    ResetCooldowns();
                }
                if (GUILayout.Button(
                        "升级"))
                {
                    GrantDebugLevel();
                }
                if (GUILayout.Button(
                        "回100蓝"))
                {
                    RestoreMana100();
                }
                if (GUILayout.Button(
                        "扣100蓝"))
                {
                    DrainMana100();
                }
                GUILayout.Label(
                    "Controls: WASD move, RMB move, " +
                    "Q/W cast toward cursor, E aim + LMB, " +
                    "R self-cast, T attack, L level up, " +
                    "Space pause, " +
                    "N step, F/V speed");
                GUILayout.EndArea();
            }
            catch (System.Exception exception)
            {
                GUILayout.EndArea();
                Debug.LogWarning(
                    "HeroTest OnGUI display error: " +
                    exception.GetBaseException()?.Message);
            }
        }

        private void OnDrawGizmos()
        {
            if (world?.PathGrid == null)
            {
                return;
            }
            PathGridMap2D grid = world.PathGrid;
            // Obstacle cells (Medium layer) as red cubes.
            bool[] layer = grid.GetWalkableLayer(
                RadiusClass.Medium);
            if (layer != null)
            {
                Gizmos.color =
                    new Color(1f, 0.2f, 0.2f, 0.5f);
                int step = Mathf.Max(
                    1,
                    Mathf.RoundToInt(cellSize * 2f));
                for (int cy = 0;
                     cy < grid.Height;
                     cy += step)
                {
                    for (int cx = 0;
                         cx < grid.Width;
                         cx += step)
                    {
                        if (layer[
                                cy * grid.Width +
                                cx])
                        {
                            continue;
                        }
                        fp2 c =
                            grid.CellToWorld(
                                cx, cy);
                        Gizmos.DrawCube(
                            new Vector3(
                                (float)c.x,
                                0.5f,
                                (float)c.y),
                            new Vector3(
                                (float)cellSize,
                                1f,
                                (float)cellSize));
                    }
                }
            }

            // Hero A* route, if any.
            if (hero?.Locomotion != null &&
                hero.Locomotion
                    .Route
                    .AStarPathCellIndices != null)
            {
                int[] cells =
                    hero.Locomotion
                        .Route
                        .AStarPathCellIndices;
                Gizmos.color =
                    new Color(0.2f, 1f, 0.2f, 0.9f);
                Vector3? previous = null;
                for (int i = 0;
                     i < cells.Length;
                     i++)
                {
                    int index = cells[i];
                    int cx = index %
                        grid.Width;
                    int cy = index /
                        grid.Width;
                    fp2 c =
                        grid.CellToWorld(
                            cx, cy);
                    Vector3 current =
                        new Vector3(
                            (float)c.x,
                            0.4f,
                            (float)c.y);
                    if (previous.HasValue)
                    {
                        Gizmos.DrawLine(
                            previous.Value,
                            current);
                    }
                    previous = current;
                }
            }

            // Spawn markers.
            Gizmos.color =
                new Color(0.2f, 0.5f, 1f, 0.9f);
            Gizmos.DrawWireCube(
                new Vector3(
                    heroSpawn.x,
                    0.5f,
                    heroSpawn.y),
                new Vector3(2f, 1f, 2f));
            Gizmos.color =
                new Color(1f, 0.6f, 0f, 0.9f);
            Gizmos.DrawWireCube(
                new Vector3(
                    dummySpawn.x,
                    0.5f,
                    dummySpawn.y),
                new Vector3(2f, 1f, 2f));
        }
    }
}
