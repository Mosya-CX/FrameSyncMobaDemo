using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public sealed class FrameSyncGameRuntime
    {
        private readonly SimulationTickPipeline _pipeline;
        private readonly SimulationTickContextController _tickController;
        private readonly PredictionRollbackCoordinator _rollbackCoordinator;
        private readonly CommandRelayBuffer _commandRelayBuffer;
        private readonly AuthorityRecoveryArchive _authorityRecoveryArchive;
        private readonly AuthorityFrameReplicator _authorityFrameReplicator;
        private readonly AuthorityRecoveryCoordinator _authorityRecoveryCoordinator;
        private PlayerSlotUnitMapping[] _playerSlotMappings =
            Array.Empty<PlayerSlotUnitMapping>();
        private int _naturalGoldIntervalTicks = 15;
        private int _naturalGoldAmount = 2;

        public Unit.UnitWorld UnitWorld { get; }
        public PhysicsWorld PhysicsWorld { get; }
        public Unit.CombatSystem CombatSystem { get; }
        public MatchRuleRuntime MatchRule { get; }
        public GoldIncomeRuntime GoldIncome { get; }

        public Unit.IEquipmentShopView
            CreateEquipmentShopView(
                int playerSlot)
        {
            return new Unit.EquipmentShopView(
                _pipeline.EquipmentShop,
                GoldIncome,
                playerSlot);
        }
        public CommandCollector CommandCollector => _pipeline.CommandCollector;
        public int CurrentTick => _pipeline.LocalSimulationTick;
        public int LastCompletedTick => _pipeline.LocalSimulationTick - 1;
        public int LatestSynchronizedServerTick { get; private set; } = -1;
        public int MinCommandLeadTicks { get; private set; } = 1;
        public int MaxFutureCommandTicks => _pipeline.MaxFutureCommandTicks;
        public uint LastChecksum => _pipeline.LastChecksum;
        public SimulationTickPipeline TickPipeline => _pipeline;
        public PredictionRollbackCoordinator Prediction =>
            _rollbackCoordinator;
        public AuthorityFrameReplicator AuthorityFrames =>
            _authorityFrameReplicator;
        public AuthorityRecoveryCoordinator AuthorityRecovery =>
            _authorityRecoveryCoordinator;

        /// <summary>
        /// Active composition-root runtime that Lua UI pages query. It is set by
        /// the application layer only; deterministic simulation never depends on it.
        /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 5.3, 10.11.
        /// </summary>
        public static FrameSyncGameRuntime Instance { get; private set; }

        public static void RegisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            Instance = runtime;
        }

        public static void UnregisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
            if (ReferenceEquals(Instance, runtime))
                Instance = null;
        }

        /// <summary>
        /// Player slot bound to the local client by the applied bootstrap payload.
        /// </summary>
        public int LocalPlayerSlot { get; private set; } = -1;

        /// <summary>
        /// IEquipmentShopView pre-bound to the local player (read-only).
        /// </summary>
        public Unit.IEquipmentShopView LocalEquipmentShopView { get; private set; }

        public Unit.EquipmentShopRuntime EquipmentShop =>
            _pipeline.EquipmentShop;

        public void ConfigureShopCommandSubmitter(
            Unit.IEquipmentShopCommandSubmitter submitter)
        {
            _pipeline.EquipmentShop
                .SetCommandSubmitter(submitter);
        }

        public Unit.Unit GetLocalControlledUnit()
        {
            if (LocalPlayerSlot < 0)
                return null;
            return TryGetControlledUnit(
                LocalPlayerSlot,
                out Unit.Unit unit)
                ? unit
                : null;
        }

        public void BindLocalPlayerSlot(int playerSlot)
        {
            if (playerSlot < 0)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));
            LocalPlayerSlot = playerSlot;
            LocalEquipmentShopView =
                CreateEquipmentShopView(playerSlot);
        }

        public void ConfigureNonHeroTopology(
            in BakedMinionWaveConfig schedule,
            Unit.LaneRuntimeData[] lanes)
        {
            var minionSystem = new Unit.MinionSystem(
                UnitWorld,
                schedule,
                lanes);
            UnitWorld.MinionSystem = minionSystem;
            _pipeline.NonHeroHelper =
                new NonHeroRestoreHelper(
                    UnitWorld,
                    minionSystem);
        }

        public FrameSyncGameRuntime(
            Unit.UnitWorld unitWorld,
            PhysicsWorld physicsWorld,
            in BakedGlobalGameplayData config)
            : this(
                unitWorld,
                physicsWorld,
                config.MaxPlayers,
                config.InitialEarnedGold,
                config.EndingDurationTicks,
                config.HeroRespawnBaseTicks,
                config.HeroRespawnPerMinuteTicks,
                config.EquipmentSellRate,
                config.RandomSeed,
                config.SnapshotWindowTicks,
                config.MaxPredictionLeadTicks,
                config.AuthorityRecoveryRetryTicks,
                config.MaxAuthorityRecoveryAttemptsBeforeDisconnect)
        {
            var minionSystem = new Unit.MinionSystem(
                unitWorld,
                config.MinionWaveConfig,
                System.Array.Empty<Unit.LaneRuntimeData>());
            unitWorld.MinionSystem = minionSystem;
            _pipeline.NonHeroHelper = new NonHeroRestoreHelper(
                unitWorld, minionSystem);
            _pipeline.MaxFutureCommandTicks = config.MaxFutureCommandTicks;
            MinCommandLeadTicks = config.MinCommandLeadTicks;
            _pipeline.CombatSystem.NaturalRegenIntervalMilliseconds =
                config.NaturalRegenIntervalMilliseconds;
            _pipeline.NaturalGoldIncome = new NaturalGoldIncomeSystem(
                GoldIncome,
                MatchRule,
                config.PeriodicGoldIntervalTicks,
                config.PeriodicGoldAmount,
                config.MaxPlayers);
            _naturalGoldIntervalTicks =
                config.PeriodicGoldIntervalTicks;
            _naturalGoldAmount =
                config.PeriodicGoldAmount;
        }

        public FrameSyncGameRuntime(
            Unit.UnitWorld unitWorld,
            PhysicsWorld physicsWorld,
            int maxPlayers,
            int initialEarnedGold,
            int endingDurationTicks,
            int heroRespawnBaseTicks,
            int heroRespawnPerMinuteTicks,
            fp equipmentSellRate,
            uint randomSeed,
            int snapshotWindowTicks = 512,
            int maxPredictionLeadTicks = int.MaxValue,
            int authorityRecoveryRetryTicks = 15,
            int maxAuthorityRecoveryAttempts = 4)
        {
            UnitWorld = unitWorld;
            PhysicsWorld = physicsWorld;
            CombatSystem = new Unit.CombatSystem(
                unitWorld,
                heroRespawnBaseTicks,
                heroRespawnPerMinuteTicks,
                randomSeed);
            MatchRule = new MatchRuleRuntime(endingDurationTicks);
            GoldIncome = new GoldIncomeRuntime();
            GoldIncome.Initialize(maxPlayers, initialEarnedGold);
            var projectileWorld = new Unit.ProjectileWorld
            {
                DefRegistry = new Unit.ProjectileDefRegistry(),
                UnitWorld = unitWorld,
                PhysicsWorld = physicsWorld,
                PrefabTable = unitWorld.GlobalPrefabTable,
                LogicSecondsPerTick =
                    fp.one / (fp)unitWorld.TickRate,
            };
            var equipmentShop = new Unit.EquipmentShopRuntime();
            equipmentShop.Initialize(
                maxPlayers,
                unitWorld.EquipmentDatabase ?? new Unit.EquipmentDatabase(),
                equipmentSellRate,
                unitWorld);
            equipmentShop.ConfigureIncomeView(
                GoldIncome);
            _pipeline = new SimulationTickPipeline(unitWorld, physicsWorld)
            {
                CombatSystem = CombatSystem,
                MatchRule = MatchRule,
                GoldIncome = GoldIncome,
                ProjectileWorld = projectileWorld,
                ProjectileHitResolver = physicsWorld != null
                    ? new ProjectileHitResolver(physicsWorld, unitWorld)
                    : null,
                EquipmentShop = equipmentShop,
            };
            unitWorld.RespawnTimer ??= new Unit.RespawnTimer(unitWorld);
            unitWorld.DeathEffectDispatcher ??=
                new Unit.DeathEffectDispatcher(unitWorld, CombatSystem);
            CombatSystem.RespawnTimer = unitWorld.RespawnTimer;
            CombatSystem.DeathEffectDispatcher = unitWorld.DeathEffectDispatcher;
            unitWorld.CombatSystem = CombatSystem;
            unitWorld.ProjectileWorld = projectileWorld;
            if (physicsWorld != null)
                unitWorld.RangeQuery = new Unit.RangeQueryService(physicsWorld);
            var randomService = new DeterministicRandomService(randomSeed);
            _pipeline.RandomService = randomService;
            unitWorld.RandomService = randomService;
            _tickController = new SimulationTickContextController();
            _rollbackCoordinator = new PredictionRollbackCoordinator(
                new SnapshotStore(snapshotWindowTicks),
                _pipeline,
                _tickController,
                maxPredictionLeadTicks);
            _commandRelayBuffer = new CommandRelayBuffer();
            _authorityRecoveryArchive =
                new AuthorityRecoveryArchive(snapshotWindowTicks);
            _authorityFrameReplicator =
                new AuthorityFrameReplicator(
                    _pipeline,
                    _tickController,
                    _commandRelayBuffer,
                    _authorityRecoveryArchive);
            _authorityRecoveryCoordinator =
                new AuthorityRecoveryCoordinator(
                    _rollbackCoordinator,
                    authorityRecoveryRetryTicks,
                    maxAuthorityRecoveryAttempts);
            _pipeline.RestoreStaticBindings =
                ReapplyPlayerSlotMappings;
        }

        public void ConfigureMatchStart(
            int startTick,
            uint initialRandomSeed,
            int playerCount,
            int initialEarnedGold)
        {
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(startTick));
            if (initialRandomSeed == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(initialRandomSeed));
            if (playerCount < 1 || playerCount > 10)
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount));
            if (initialEarnedGold < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(initialEarnedGold));

            var initialGold = new int[playerCount];
            for (int i = 0; i < initialGold.Length; i++)
                initialGold[i] = initialEarnedGold;
            GoldIncome.Initialize(
                startTick,
                initialGold);
            var randomState =
                new DeterministicRandomSnapshot(
                    initialRandomSeed);
            _pipeline.RandomService.Restore(
                randomState);
            UnitWorld.RandomService.Restore(
                randomState);
            CombatSystem.ConfigureInitialMatchSeed(
                initialRandomSeed);
            _pipeline.NaturalGoldIncome =
                new NaturalGoldIncomeSystem(
                    GoldIncome,
                    MatchRule,
                    _naturalGoldIntervalTicks,
                    _naturalGoldAmount,
                    playerCount);
        }

        public Unit.UnitUid[]
            MaterializeInitialSpawnsForBootstrap(
                int startTick)
        {
            return _pipeline
                .MaterializeInitialSpawnsForBootstrap(
                    _tickController,
                    startTick);
        }

        public void ConfigurePlayerSlotMappings(
            PlayerSlotUnitMapping[] mappings)
        {
            if (mappings == null)
                throw new ArgumentNullException(
                    nameof(mappings));
            var copy =
                (PlayerSlotUnitMapping[])mappings.Clone();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].PlayerSlot != i)
                    throw new DeterministicSimulationException(
                        "Player slot mappings must be stored in ascending order.");
            }
            _playerSlotMappings = copy;
            for (int i = 0; i < copy.Length; i++)
            {
                UnityEngine.Debug.Log(
                    $"[SlotMap] slot={copy[i].PlayerSlot} " +
                    $"uid={copy[i].ControlledUnitUid} " +
                    $"team={(UnitWorld.TryGetUnit(copy[i].ControlledUnitUid, out var mu) ? mu.TeamId.Value.ToString() : "?")}");
            }
            bool canResolveAll = true;
            for (int i = 0; i < copy.Length; i++)
                if (!UnitWorld.TryGetUnit(
                        copy[i].ControlledUnitUid,
                        out _))
                {
                    canResolveAll = false;
                    break;
                }
            if (canResolveAll)
                ReapplyPlayerSlotMappings();
        }

        public bool TryGetControlledUnit(
            int playerSlot,
            out Unit.Unit unit)
        {
            if ((uint)playerSlot >=
                    (uint)_playerSlotMappings.Length)
            {
                unit = null;
                return false;
            }
            return UnitWorld.TryGetUnit(
                _playerSlotMappings[playerSlot]
                    .ControlledUnitUid,
                out unit);
        }

        public void RestoreInitialSnapshot(
            in GameplaySnapshot snapshot,
            int snapshotTick,
            ExecutionMode executionMode)
        {
            // The initial authority snapshot owns the complete Unit topology.
            // Client-side authoring queues must not materialize the same Units again.
            _pipeline
                .DiscardPendingInitialSpawnsForAuthoritativeRestore();
            _pipeline.RestoreFromSnapshot(
                snapshot,
                snapshotTick,
                executionMode);
            _rollbackCoordinator
                .InitializeAuthorityBaseline(
                    snapshotTick);
            ReapplyPlayerSlotMappings();
        }

        private void ReapplyPlayerSlotMappings()
        {
            var units = UnitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
                units[i].ControlledByPlayerSlot = -1;
            for (int i = 0;
                 i < _playerSlotMappings.Length;
                 i++)
            {
                PlayerSlotUnitMapping mapping =
                    _playerSlotMappings[i];
                if (!UnitWorld.TryGetUnit(
                        mapping.ControlledUnitUid,
                        out Unit.Unit unit))
                    throw new DeterministicSimulationException(
                        $"PlayerSlot {mapping.PlayerSlot} cannot resolve controlled Unit.");
                unit.ControlledByPlayerSlot =
                    mapping.PlayerSlot;
            }
        }

        public void SubmitCommand(GameplayCommand command)
        {
            _pipeline.SubmitCommand(command);
        }

        public AcceptedCommandRelay[] AcceptCommandBundle(
            in GameplayCommandBundle bundle,
            System.Func<GameplayCommand, bool> authorizeCommand = null)
        {
            return _commandRelayBuffer.AcceptBundle(
                bundle,
                _pipeline.LocalSimulationTick,
                _pipeline.MaxFutureCommandTicks,
                authorizeCommand);
        }

        public void ApplyAcceptedCommandRelay(
            in AcceptedCommandRelay relay)
        {
            _rollbackCoordinator.SetPredictedCommandFrame(
                relay.TargetTick,
                relay.RelayRevision,
                relay.DecodeCommands());
        }

        public AuthorityFrame ExecuteAuthorityTick()
        {
            AuthorityFrame frame =
                _authorityFrameReplicator.ExecuteNextTick();
            _rollbackCoordinator
                .ReleaseServerAuthorityHistory(
                    frame.Tick);
            LatestSynchronizedServerTick = frame.Tick;
            return frame;
        }

        public bool ExecutePredictionTick()
        {
            return _rollbackCoordinator.ExecutePredictionTick();
        }

        public void ReceiveAuthorityFrame(in AuthorityFrame frame)
        {
            _rollbackCoordinator.OnAuthorityFrameReceived(frame);
            LatestSynchronizedServerTick =
                _rollbackCoordinator.LatestAuthorityFrameTick;
        }

        public AuthorityRecoveryResponse BuildRecoveryResponse(
            in AuthorityRecoveryRequest request)
        {
            return _authorityRecoveryArchive.BuildResponse(request);
        }

        public void ExecuteOneTick()
        {
            int authorityTick = _pipeline.LocalSimulationTick;
            _rollbackCoordinator.EnsureRollbackAnchor();
            _pipeline.ExecuteTick(
                _tickController,
                ExecutionMode.ServerAuthority);
            _pipeline.GoldIncome?.ConfirmThroughTick(
                authorityTick);
            _rollbackCoordinator
                .ReleaseServerAuthorityHistory(
                    authorityTick);
            LatestSynchronizedServerTick = authorityTick;
        }

        public void QueueInitialSpawn(in Unit.UnitSpawnRequest request)
        {
            _pipeline.QueueInitialSpawn(request);
        }

        public CommandTargetTickResolver CreateCommandTargetTickResolver(
            ICommandNetworkTimingProvider networkTimingProvider = null)
        {
            return new CommandTargetTickResolver(
                () => CurrentTick,
                () => LatestSynchronizedServerTick,
                MinCommandLeadTicks,
                MaxFutureCommandTicks,
                networkTimingProvider);
        }

        public void ExecuteTicks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ExecuteOneTick();
            }
        }

        /// <summary>
        /// Binds a Unit to its PhysicsEntity2D for spatial queries and
        /// presentation sync. Called during spawn/bootstrap by the
        /// GameObject layer.
        /// </summary>
        public void BindUnitPhysics(Unit.Unit unit, PhysicsEntity2D entity)
        {
            if (unit == null || entity == null) return;

            Unit.UnitUid uid = unit.UnitUid;
            entity.SetQueryInfo(new PhysicsEntityQueryInfo(
                uidSnapshot: new RuntimeUidQueryValue(
                    uid.SpawnLogicTick,
                    uid.RuntimeEntityPrefabId,
                    uid.SpawnSequenceInTick),
                kind: PhysicsEntityKind.Unit,
                teamSnapshot: unit.TeamId.Value,
                owner: unit));
            PhysicsWorld?.RegisterUnit(entity);
        }

        /// <summary>
        /// Unbinds a Unit from its PhysicsEntity2D. Called during
        /// death/despawn by the GameObject layer.
        /// </summary>
        public void UnbindUnitPhysics(PhysicsEntity2D entity)
        {
            if (entity == null) return;
            PhysicsWorld?.Unregister(entity);
        }
    }
}
