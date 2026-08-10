using System.Collections.Generic;
using System.Threading.Tasks;
using FrameSyncMoba.Bootstrap.Tests;
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
    /// IMGUI panel + grid/A* gizmos. Loads the FullMatchTest config assets
    /// directly (editor play only, not packaged).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTestDriver : MonoBehaviour
    {
        [Header("Map (logic units)")]
        [SerializeField] private float mapWidth = 40f;
        [SerializeField] private float mapHeight = 40f;
        [SerializeField] private Vector2 mapCenter = Vector2.zero;
        [SerializeField] private float cellSize = 0.5f;

        [Header("Hero")]
        [SerializeField] private int heroPrototypeId = 1001;
        [SerializeField] private Vector2 heroSpawn = new Vector2(-15f, -15f);
        [SerializeField] private int dummyPrototypeId = 1001;
        [SerializeField] private Vector2 dummySpawn = new Vector2(-10f, -10f);
        [Tooltip("Seconds after the dummy dies before it respawns at its spawn point.")]
        [SerializeField] private float dummyRespawnSeconds = 3f;

        [Header("Simulation")]
        [SerializeField] private float ticksPerSecond = 30f;
        [SerializeField] private bool paused;
        [Tooltip("Optional camera that should follow the hero.")]
        [SerializeField] private CameraController followCamera;

        private UnitWorld world;
        private PhysicsWorld physicsWorld;
        private SimulationTickPipeline pipeline;
        private CombatSystem combat;
        private SimulationTickContextController tickController =
            new SimulationTickContextController();
        private UnitType hero;
        private readonly List<UnitType> dummies =
            new List<UnitType>();
        private float accumulator;
        private uint commandSeq = 1;
        private UnitUid attackTarget;
        private bool eAiming;
        private bool rAiming;
        private bool qAiming;
        private bool qSessionWasActive;
        private int qAimingGraceFrames;
        private SkillIndicatorDriver indicatorDriver;
        private PresentationEventDispatcher vfxDispatcher;

        private readonly Dictionary<UnitUid, ClientUnitOutline>
            outlines =
                new Dictionary<UnitUid, ClientUnitOutline>();
        private Task<List<UnitUid>> hoverTask;
        private UnitUid? hoveredUnit;
        private HeroDisplayTable heroDisplayTable;
        private EquipmentDatabase equipmentDatabase;
        private LineRenderer attackRangeRing;
        private float dummyRespawnTimer = -1f;
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

        public UnitType Hero => hero;
        public IReadOnlyList<UnitType> Dummies => dummies;
        public UnitWorld World => world;
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

        private void Start()
        {
            BuildWorld();
            BuildMap();
            SpawnHero();
            SpawnDummiesAtScenePoints();
            EnsureIndicatorDriver();
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
                GameObject prefab =
                    Resources.Load<GameObject>(
                        "Prefab/UI/UIManager");
                if (prefab != null)
                {
                    uiManager = Instantiate(prefab)
                        .GetComponent<UIManager>();
                }
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

            // Persistent Blight stack marks (1 = left, 2 = left+right,
            // 3 = triangle). Presentation-only.
            var blightMarks =
                new GameObject(
                    "HeroTestBlightMarks");
            var marksPresenter =
                blightMarks.AddComponent<
                    BlightStackMarkPresenter>();
            marksPresenter.Initialize(
                AssetDatabase
                    .LoadAssetAtPath<GameObject>(
                        "Assets/Resources/Prefab/VFX/RevengeMarkVFX.prefab"),
                () => world != null
                    ? (System.Collections.Generic
                        .IReadOnlyList<UnitType>)
                        world.GetAllUnits()
                    : System.Array
                        .Empty<UnitType>());

        }

        private void BuildWorld()
        {
            var config = AssetDatabase
                .LoadAssetAtPath<GlobalGameplayData>(
                    "Assets/Config/Formal/GlobalGameplayData.asset")
                .BakeOrThrow();
            outlineRimMaterial =
                AssetDatabase
                    .LoadAssetAtPath<Material>(
                        "Assets/Config/Formal/UnitOutlineRim.mat");
            heroDisplayTable =
                AssetDatabase
                    .LoadAssetAtPath<GlobalGameplayData>(
                        "Assets/Config/Formal/GlobalGameplayData.asset")
                    ?.HeroDisplayTable;
            var unitCatalog = AssetDatabase
                .LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset")
                .BakeOrThrow(config.PrefabTable);
            var abilityCatalog = AssetDatabase
                .LoadAssetAtPath<AbilityRuntimeCatalogAsset>(
                    "Assets/Config/Formal/Abilities/VarusAbilityRuntimeCatalog.asset")
                .BakeOrThrow();

            physicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = config.UnitGridCellSize,
                },
            };
            // No formal equipment yet (test fixture removed): the shop runs
            // with an empty database until production items are authored.
            equipmentDatabase =
                new EquipmentDatabase();
            world = new UnitWorld
            {
                PhysicsWorld = physicsWorld,
                GlobalPrefabTable = config.PrefabTable,
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
            };
            world.RangeQuery =
                new RangeQueryService(physicsWorld);
            var buffCatalog = AssetDatabase
                .LoadAssetAtPath<BuffCatalogAsset>(
                    "Assets/Config/Formal/Buffs/FullMatchTestBuffCatalog.asset");
            buffCatalog?.RegisterAll(
                world.BuffDefinitions);
            var ccCatalog = AssetDatabase
                .LoadAssetAtPath<CrowdControlCatalogAsset>(
                    "Assets/Config/Formal/CrowdControl/CrowdControlCatalog.asset");
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
                        AssetDatabase
                            .LoadAssetAtPath<
                                ProjectileRuntimeCatalogAsset>(
                                "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset")
                            .BakeOrThrow(
                                config.PrefabTable),
                    UnitWorld = world,
                    PhysicsWorld = physicsWorld,
                    PrefabTable =
                        config.PrefabTable,
                    LogicSecondsPerTick =
                        fp.one / (fp)config.TickRate,
                };
            pipeline = new SimulationTickPipeline(
                world,
                physicsWorld)
            {
                CombatSystem = combat,
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
        /// index so the Corruption Vines spread can chain between adjacent
        /// dummies (spread only affects enemy heroes).
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
            HandleHeroInput();
            LogAttackDiagnostics();
            UpdateIndicators();
            UpdateUnitRadiusCircles();
            UpdateAttackRangeRing();
            UpdateDummyRespawn();
            UpdateHoverDetection();
            DrawDebugLines();
            RefreshTestHud();

            if (paused)
            {
                return;
            }
            double step =
                1.0 / Mathf.Max(
                    1f,
                    Mathf.RoundToInt(
                        ticksPerSecond));
            accumulator += Time.deltaTime;
            int guard = 0;
            while (accumulator >= step &&
                   guard++ < 8)
            {
                accumulator -= (float)step;
                pipeline.ExecuteTick(
                    tickController,
                    ExecutionMode.ServerAuthority);
                // Consume deterministic presentation events per Tick so
                // events from earlier Ticks in the same frame survive
                // (VisualEventOutput is cleared at the next Tick start).
                vfxDispatcher?.DispatchCurrentFrame();
            }
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
        /// dummyRespawnSeconds. Local test convenience, not frame-synced.
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
                dummyRespawnTimer = -1f;
                return;
            }
            if (dummyRespawnTimer < 0f)
            {
                dummyRespawnTimer =
                    Mathf.Max(
                        0f,
                        dummyRespawnSeconds);
            }
            dummyRespawnTimer -=
                Time.deltaTime;
            if (dummyRespawnTimer <= 0f)
            {
                RespawnDummy(deadDummy);
                dummyRespawnTimer = -1f;
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
                            .GetCooldownRemainingTicks(
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
                            .GetCooldownTotalTicks(
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
                            .GetCooldownRemainingTicks(
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
                        ?.GetCurrentIcon();
                };
            GameFlowLuaBridge.GetPassiveAbilityIcon =
                () =>
                {
                    UnitType unit = Local();
                    return unit?.AbilityHandler
                        ?.FixedPassive
                        ?.GetCurrentIcon();
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
                    return entry.Avatar;
                };
            GameFlowLuaBridge.GetHudGold =
                () => 999999;
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
                () => 0;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotId =
                _ => 0;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotName =
                _ => "";
            GameFlowLuaBridge
                .GetLocalEquipmentSlotStack =
                _ => 0;
            GameFlowLuaBridge
                .GetLocalEquipmentSlotIcon =
                _ => null;
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
                () => 0f;
            GameFlowLuaBridge
                .GetPassiveCooldownTotalSeconds =
                () => 0f;
            GameFlowLuaBridge.GetLocalBuffCount =
                () => Local()?.BuffHandler
                    ?.GetAllOrdered()?.Count ?? 0;
            GameFlowLuaBridge.GetLocalBuffIcon =
                index =>
                    BuffAt(index)
                        ?.Definition?.Display?.Icon;
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

            // ---- Shop (infinite gold test mode) ----

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
            GameFlowLuaBridge.GetShopItemPrice =
                index =>
                {
                    var defs =
                        equipmentDatabase
                            ?.AllDefinitions;
                    return defs != null &&
                        index >= 0 &&
                        index < defs.Count
                            ? defs[index].Value
                            : 0;
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
                    return def?.Value ?? 0;
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
                () => 999999;
            GameFlowLuaBridge.CanUndo =
                () => false;
            GameFlowLuaBridge.RequestPurchase =
                equipmentId =>
                {
                    if (hero == null)
                    {
                        return;
                    }
                    EquipmentDefinition def =
                        equipmentDatabase
                            ?.GetDefinition(
                                equipmentId);
                    if (def == null)
                    {
                        return;
                    }
                    int slot =
                        hero.EquipmentHandler
                            .FirstEmptySlot();
                    if (slot < 0)
                    {
                        return;
                    }
                    hero.EquipmentHandler.Add(
                        def,
                        slot);
                };
            GameFlowLuaBridge.RequestSell =
                slot =>
                {
                    if (hero?.EquipmentHandler == null)
                    {
                        return;
                    }
                    hero.EquipmentHandler.Remove(
                        slot);
                };
            GameFlowLuaBridge.RequestUndo =
                () => { };
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
                () => "";
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
                unit.GetComponent<
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

        private void HandleHeroInput()
        {
            if (hero == null ||
                hero.LifeState != LifeState.Alive)
            {
                return;
            }

            // Right click: attack the hovered unit, otherwise move/A* to the
            // ground point (MOBA convention).
            if (Input.GetMouseButtonDown(1))
            {
                // Right click cancels local aim (SecondaryClick ->
                // CancelLocalAim in the current default aim profile).
                eAiming = false;
                rAiming = false;
                if (hoveredUnit.HasValue)
                {
                    SubmitAttack(
                        hoveredUnit.Value);
                }
                else
                {
                    fp2? point =
                        ScreenToGround(
                            Input.mousePosition);
                    if (point.HasValue)
                    {
                        SubmitMove(point.Value);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                if (dummies.Count > 0)
                {
                    SubmitAttack(
                        dummies[0].UnitUid);
                }
            }

            // Debug: L grants one level (and the skill point it carries),
            // useful for testing skill point allocation in the hero scene.
            if (Input.GetKeyDown(KeyCode.L))
            {
                GrantDebugLevel();
            }

            fp2? ground = ScreenToGround(
                Input.mousePosition);
            fp2 aimPoint =
                ground ?? hero.PhysicsEntity
                    .Transform2D.Position;
            fp2 heroPos =
                hero.PhysicsEntity
                    .Transform2D.Position;
            fp2 direction =
                aimPoint - heroPos;
            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (!IsAbilityLearned(0))
                {
                    return;
                }
                qAiming = true;
                qSessionWasActive = false;
                qAimingGraceFrames = 0;
                SubmitCast(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.None);
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                if (!IsAbilityLearned(1))
                {
                    return;
                }
                SubmitCast(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None);
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                // Local aim only: no Command until the primary click commits.
                if (!IsAbilityLearned(2))
                {
                    return;
                }
                eAiming = true;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                // Local aim only: no Command until the primary click commits.
                if (!IsAbilityLearned(3))
                {
                    return;
                }
                rAiming = true;
            }

            // Primary click commits the currently aiming / charging ability
            // (Player Input v1.1 current charge profile: press=Focus,
            // release=None, click=Commit).
            if (Input.GetMouseButtonDown(0))
            {
                if (hero.AbilityHandler != null &&
                    hero.AbilityHandler.HasActiveSession(0) &&
                    hero.AbilityHandler.IsWaitingForCommit(0))
                {
                    SubmitCast(
                        0,
                        AbilitySignalVerb.Commit,
                        AimSnapshot.ForDirection(
                            direction));
                }
                else if (eAiming)
                {
                    SubmitCast(
                        2,
                        AbilitySignalVerb.Commit,
                        AimSnapshot.ForPoint(
                            aimPoint));
                    eAiming = false;
                }
                else if (rAiming)
                {
                    SubmitCast(
                        3,
                        AbilitySignalVerb.Commit,
                        AimSnapshot.ForDirection(
                            direction));
                    rAiming = false;
                }
            }
        }

        private bool IsAbilityLearned(byte slot)
        {
            int level =
                hero?.AbilityHandler
                    ?.GetAbilityLevel(slot) ?? 0;
            if (level > 0)
            {
                return true;
            }
            Debug.Log(
                $"[HeroTest] Slot {slot} is not learned " +
                $"(level 0); input ignored.");
            return false;
        }

        /// <summary>
        /// Finds or builds the 2D skill indicator driver used by the hero
        /// test scene (Q charge bar, E ground circle, R direction bar).
        /// </summary>
        private void EnsureIndicatorDriver()
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
                indicatorDriver.Configure(
                    Resources.Load<GameObject>(
                        "Prefab/Indicators/DirectionIndicator"),
                    Resources.Load<GameObject>(
                        "Prefab/Indicators/RangeCircleIndicator"),
                    Resources.Load<GameObject>(
                        "Prefab/Indicators/GroundTargetIndicator"));
            }
        }

        /// <summary>
        /// Drives the skill indicators every frame:
        /// - Q (Direction): cast-range circle at max range + a rounded bar
        ///   that grows with the charge ratio; other commands cannot cancel
        ///   it, only the charge ending (release/interrupt/timeout) hides it.
        /// - E (Point): cast-range circle + a cursor-following circle with
        ///   the desecrated-ground radius.
        /// - R (Direction): cast-range circle + a direction bar.
        /// - W has no indicator.
        /// </summary>
        private void UpdateIndicators()
        {
            if (indicatorDriver == null ||
                hero == null ||
                hero.MovementHandler == null)
            {
                return;
            }

            fp2 heroPos =
                hero.PhysicsEntity
                    .Transform2D.Position;
            fp2 forward =
                hero.MovementHandler.Facing;
            fp2? ground =
                ScreenToGround(
                    Input.mousePosition);
            fp2 aimPoint =
                ground ?? heroPos;

            if (qAiming)
            {
                UpdateQIndicator(
                    heroPos,
                    forward,
                    aimPoint);
                return;
            }
            if (eAiming)
            {
                UpdateEIndicator(
                    heroPos,
                    forward,
                    aimPoint);
                return;
            }
            if (rAiming)
            {
                UpdateRIndicator(
                    heroPos,
                    forward,
                    aimPoint);
                return;
            }

            if (indicatorDriver.IsVisible)
            {
                indicatorDriver.Hide();
            }
        }

        private void UpdateQIndicator(
            fp2 heroPos,
            fp2 forward,
            fp2 aimPoint)
        {
            AbilityRuntime q =
                hero.AbilityHandler?
                    .GetActiveRuntime(0);
            AbilitySession session =
                q?.ActiveSession;
            bool hasSession =
                session != null &&
                !session.Cancelled &&
                !session.Interrupted;

            if (!hasSession &&
                qSessionWasActive)
            {
                // Charge ended: released, interrupted or timed out.
                qAiming = false;
                qSessionWasActive = false;
                return;
            }
            qSessionWasActive =
                hasSession;

            fp minRange = (fp)9.25m;
            fp maxRange = (fp)16.25m;
            int maxChargeTicks = 45;
            if (hasSession)
            {
                CastStage? hold =
                    q.Definition?.CastModel
                        ?.GetStage(
                            session
                                .CurrentStageKey);
                if (hold.HasValue &&
                    hold.Value.Def is
                        ChargeStageDef charge)
                {
                    maxChargeTicks =
                        Mathf.Max(
                            1,
                            charge.MaxChargeTicks);
                }
                if (q.Definition?.CastModel is
                    HoldReleaseCastModelDef
                        holdModel &&
                    holdModel.Release.Def is
                        ChargeProjectileStageDef
                            release)
                {
                    minRange =
                        release.MinRange;
                    maxRange =
                        release.MaxRange;
                }
            }

            float ratio = 0f;
            if (hasSession)
            {
                ratio =
                    Mathf.Clamp01(
                        (float)session
                            .StageElapsedTicks /
                        maxChargeTicks);
            }
            else
            {
                // The Focus command was pressed but the logic session has
                // not been created yet (same-frame). Keep the indicator at
                // the minimum length; abandon it after a grace period when
                // the charge never starts (e.g. not ready).
                qAimingGraceFrames++;
                if (qAimingGraceFrames >
                    120)
                {
                    qAiming = false;
                    qSessionWasActive =
                        false;
                    return;
                }
            }
            fp length =
                minRange +
                (maxRange - minRange) *
                (fp)ratio;

            if (!indicatorDriver.IsVisible ||
                indicatorDriver.ActiveKind !=
                    AimKind.Direction)
            {
                indicatorDriver.Show(
                    AimKind.Direction,
                    maxRange,
                    heroPos,
                    forward);
            }
            indicatorDriver.UpdateCursor(
                aimPoint,
                heroPos,
                forward);
            indicatorDriver
                .UpdateDirectionLength(
                    length);
        }

        private void UpdateEIndicator(
            fp2 heroPos,
            fp2 forward,
            fp2 aimPoint)
        {
            fp castRange = (fp)9.25m;
            fp groundRadius = (fp)3m;
            AbilityDef eDef =
                hero.AbilityHandler?
                    .GetAbilityDef(2);
            if (eDef != null &&
                eDef.IsValid)
            {
                castRange =
                    eDef.CastRange;
                if (eDef.CastModel is
                    CommitCastModelDef eModel &&
                    eModel.Cast.Def is
                        AreaDamageStageDef area)
                {
                    groundRadius =
                        area.Radius;
                }
            }

            if (!indicatorDriver.IsVisible ||
                indicatorDriver.ActiveKind !=
                    AimKind.Point)
            {
                indicatorDriver.Show(
                    AimKind.Point,
                    castRange,
                    heroPos,
                    forward,
                    true,
                    groundRadius);
            }
            indicatorDriver.UpdateCursor(
                aimPoint,
                heroPos,
                forward);
        }

        private void UpdateRIndicator(
            fp2 heroPos,
            fp2 forward,
            fp2 aimPoint)
        {
            fp castRange = (fp)10.75m;
            AbilityDef rDef =
                hero.AbilityHandler?
                    .GetAbilityDef(3);
            if (rDef != null &&
                rDef.IsValid)
            {
                castRange =
                    rDef.CastRange;
            }

            if (!indicatorDriver.IsVisible ||
                indicatorDriver.ActiveKind !=
                    AimKind.Direction)
            {
                indicatorDriver.Show(
                    AimKind.Direction,
                    castRange,
                    heroPos,
                    forward);
            }
            indicatorDriver.UpdateCursor(
                aimPoint,
                heroPos,
                forward);
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
            if (Input.GetKeyDown(KeyCode.J))
            {
                ResetDummies();
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                RefillMana();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                ResetCooldowns();
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                SpawnDummy();
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                SpawnDummiesAtScenePoints();
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                RebuildGridFromSceneObstacles();
            }
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

        private void SubmitMove(fp2 target)
        {
            if (hero == null)
            {
                return;
            }
            if (hero.AbilityHandler != null &&
                hero.AbilityHandler.IsCastMovementLocked())
            {
                return;
            }
            pipeline.SubmitCommand(
                GameplayCommand.CreateMove(
                    MakeHeader(
                        GameplayCommandKind.Move),
                    target));
        }

        private void SubmitAttack(UnitUid target)
        {
            if (hero == null || !target.IsValid())
            {
                return;
            }
            if (hero.AbilityHandler != null &&
                hero.AbilityHandler.IsCastMovementLocked())
            {
                return;
            }
            attackTarget = target;
            Debug.Log(
                $"[HeroTest][RMB] tick={CurrentTick} " +
                $"attackTarget={target}");
            pipeline.SubmitCommand(
                GameplayCommand.CreateAttack(
                    MakeHeader(
                        GameplayCommandKind.Attack),
                    target));
        }

        private int lastAttackDiagTick = -1;

        /// <summary>
        /// Periodic diagnostics while an attack target is active: prints the
        /// hero's Intent, attack plan status, attack-cycle state, center
        /// distance vs range, and the locomotion task, so a "chased but not
        /// attacking" stall can be pinpointed.
        /// </summary>
        private void LogAttackDiagnostics()
        {
            if (hero == null ||
                !attackTarget.IsValid())
            {
                return;
            }
            int tick = CurrentTick;
            if (tick - lastAttackDiagTick < 5)
            {
                return;
            }
            lastAttackDiagTick = tick;

            AttackHandler attack =
                hero.AttackHandler;
            UnitIntent intent = default;
            if (hero.Planner != null)
            {
                intent = hero.Planner.CurrentIntent;
            }
            string taskInfo = "none";
            if (hero.Locomotion != null)
            {
                MovementTask task =
                    hero.Locomotion.CurrentTask;
                taskInfo =
                    $"{task.Purpose}/{task.State}";
            }
            fp2 heroPos =
                hero.PhysicsEntity
                    ?.Transform2D.Position ??
                fp2.zero;
            fp2 targetPos = heroPos;
            if (world != null &&
                world.TryGetUnit(
                    attackTarget,
                    out UnitType target) &&
                target?.PhysicsEntity != null)
            {
                targetPos =
                    target.PhysicsEntity
                        .Transform2D.Position;
            }
            fp distance =
                fpmath.length(
                    targetPos - heroPos);
            Debug.Log(
                $"[HeroTest][AttackDiag] tick={tick} " +
                $"intent={intent.Kind} " +
                $"target={attackTarget} " +
                $"plan={attack?.GetAttackPlanStatus(attackTarget)} " +
                $"cycle={attack?.IsAttackCycleActive} " +
                $"atkTarget={attack?.CurrentTargetUid} " +
                $"ready={attack?.IsAttackReady()} " +
                $"range={attack?.CurrentAttackRange} " +
                $"dist={distance} " +
                $"task={taskInfo}");
        }

        private void SubmitCast(
            byte slot,
            AbilitySignalVerb verb,
            AimSnapshot aim)
        {
            if (hero == null)
            {
                return;
            }
            pipeline.SubmitCommand(
                GameplayCommand.CreateCastAbility(
                    MakeHeader(
                        GameplayCommandKind.CastAbility),
                    slot,
                    verb,
                    aim));
        }

        private void SubmitAllocateSkillPoint(
            byte slot)
        {
            if (hero == null)
            {
                return;
            }
            pipeline.SubmitCommand(
                GameplayCommand
                    .CreateAllocateAbilitySkillPoint(
                        MakeHeader(
                            GameplayCommandKind
                                .AllocateAbilitySkillPoint),
                        slot));
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

        private CommandHeader MakeHeader(
            GameplayCommandKind kind)
        {
            int tick =
                pipeline.LocalSimulationTick;
            return new CommandHeader(
                commandSeq++,
                clientId: 0,
                playerSlot: 0,
                hero.UnitUid,
                tick + 1,
                kind,
                tick,
                0);
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
                        paused
                            ? "Resume (Space)"
                            : "Pause (Space)"))
                {
                    paused = !paused;
                }
                if (GUILayout.Button(
                        "Step 1 Tick (N)"))
                {
                    pipeline.ExecuteTick(
                        tickController,
                        ExecutionMode.ServerAuthority);
                }
                if (GUILayout.Button(
                        "Reset Dummies (J)"))
                {
                    ResetDummies();
                }
                if (GUILayout.Button(
                        "Refill Mana (M)"))
                {
                    RefillMana();
                }
                if (GUILayout.Button(
                        "Reset Cooldowns (K)"))
                {
                    ResetCooldowns();
                }
                if (GUILayout.Button(
                        "Level Up (L)"))
                {
                    GrantDebugLevel();
                }
                if (GUILayout.Button(
                        "Spawn Dummy (B)"))
                {
                    SpawnDummy();
                }
                if (GUILayout.Button(
                        "Spawn Scene Dummies (G)"))
                {
                    SpawnDummiesAtScenePoints();
                }
                if (GUILayout.Button(
                        "Rebake Grid (H)"))
                {
                    RebuildGridFromSceneObstacles();
                }
                if (GUILayout.Button(
                        "Attack Dummy (T)") &&
                    dummies.Count > 0)
                {
                    SubmitAttack(
                        dummies[0].UnitUid);
                }
                GUILayout.Label(
                    "Controls: WASD move, RMB move, " +
                    "Q charge + LMB release, W toggle, " +
                    "E/R aim + LMB cast, T attack, " +
                    "M refill mana, K reset CD, " +
                    "B dummy, G scene dummies, " +
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
