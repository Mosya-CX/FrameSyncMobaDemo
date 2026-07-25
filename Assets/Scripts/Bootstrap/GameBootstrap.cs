using System;
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.Physics;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Project-wide deterministic configuration")]
        [SerializeField] private GlobalGameplayData globalGameplayData;
        [SerializeField] private bool dedicatedServer;

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

        [Header("Gameplay Config (0088)")]
        [SerializeField] private JungleCampConfig jungleCampConfig;

        public FrameSyncGameRuntime Runtime { get; private set; }
        public UnitWorld UnitWorld { get; private set; }
        public PhysicsWorld PhysicsWorld { get; private set; }
        public bool IsInitialized => Runtime != null;
        public MatchFlowStateMachine MatchFlow { get; private set; }

        private void Awake()
        {
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();

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
                UnitPrototypeTable = new GlobalUnitPrototypeTable(),
                StatDefinitionTable = new StatDefinitionTable(),
                EquipmentDatabase = new EquipmentDatabase(),
                AbilityDefinitions = new AbilityDefinitionRegistry(),
                BuffDefinitions = new BuffDefinitionRegistry(),
                StatGrowthC = config.StatGrowthC,
                StatGrowthD = config.StatGrowthD,
                TickRate = config.TickRate,
            };
            Runtime = new FrameSyncGameRuntime(UnitWorld, PhysicsWorld, config);
            Runtime.MatchRule.BeginCountdown(0, config.CountdownTicks);

            // Create MatchFlowStateMachine (0090)
            MatchFlow = new MatchFlowStateMachine(Runtime.MatchRule, config.CountdownTicks);

            // Initialize JungleCampSystem with config (0088)
            InitializeJungleCamps(config);

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
                // Advance match flow (0090)
                MatchFlow.AdvanceTick(Runtime.CurrentTick, UnitWorld);

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
                if (deathPresenter != null) presentationDispatcher.RegisterVfxHandler(deathPresenter);
            }
        }

        /// <summary>
        /// Initialize JungleCampSystem from JungleCampConfig ScriptableObject.
        /// </summary>
        private void InitializeJungleCamps(in BakedGlobalGameplayData config)
        {
            var system = Runtime.TickPipeline.JungleCampSystem;
            if (system == null || jungleCampConfig == null) return;

            var camps = jungleCampConfig.Camps;
            if (camps == null || camps.Count == 0) return;

            for (int i = 0; i < camps.Count; i++)
            {
                var entry = camps[i];
                int memberCount = entry.MonsterPrototypeIds?.Length ?? 1;
                var camp = system.CreateCamp(entry.CampId, memberCount);
                // Camp topology is created; actual unit spawning is handled by
                // the map initialization flow (spawning units via UnitWorld).
            }
        }

        private void Update()
        {
            if (shopPage != null) shopPage.TickVisual();
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
                () => Runtime.CurrentTick,
                () => Runtime.CurrentTick);
            playerInputController.Initialize(buffer, resolver, requester);
            if (indicatorDriver != null)
            {
                playerInputController.SetIndicatorDriver(indicatorDriver);
            }

            // Update shop page with controlled unit
            if (shopPage != null)
            {
                shopPage.Inject(Runtime, Runtime.TickPipeline.EquipmentShop,
                    UnitWorld.EquipmentDatabase, controlledUnit);
            }

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

            var dto = new UiSnapshotDto
            {
                CurrentHealth = unit.StatHandler?.CurrentHealth ?? fp.zero,
                MaxHealth = unit.StatHandler?.GetStat(StatId.MaxHealth) ?? fp.one,
                CurrentGold = Runtime.GoldIncome?.GetConfirmedAvailableGold(unit.ControlledByPlayerSlot) ?? 0,
                ConfirmedGold = Runtime.GoldIncome?.GetConfirmedAvailableGold(unit.ControlledByPlayerSlot) ?? 0,
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
    }
}
