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
                config.HeroRespawnPerLevelTicks,
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
            _pipeline.NaturalGoldIncome = new NaturalGoldIncomeSystem(
                GoldIncome,
                MatchRule,
                config.PeriodicGoldIntervalTicks,
                config.PeriodicGoldAmount,
                config.MaxPlayers);
        }

        public FrameSyncGameRuntime(
            Unit.UnitWorld unitWorld,
            PhysicsWorld physicsWorld,
            int maxPlayers,
            int initialEarnedGold,
            int endingDurationTicks,
            int heroRespawnBaseTicks,
            int heroRespawnPerLevelTicks,
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
                unitWorld, heroRespawnBaseTicks, heroRespawnPerLevelTicks);
            MatchRule = new MatchRuleRuntime(endingDurationTicks);
            GoldIncome = new GoldIncomeRuntime();
            GoldIncome.Initialize(maxPlayers, initialEarnedGold);
            var projectileWorld = new Unit.ProjectileWorld
            {
                DefRegistry = new Unit.ProjectileDefRegistry(),
                UnitWorld = unitWorld,
                PhysicsWorld = physicsWorld,
                PrefabTable = unitWorld.GlobalPrefabTable,
            };
            var equipmentShop = new Unit.EquipmentShopRuntime();
            equipmentShop.Initialize(
                maxPlayers,
                unitWorld.EquipmentDatabase ?? new Unit.EquipmentDatabase(),
                equipmentSellRate,
                unitWorld);
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
            _rollbackCoordinator.EnsureRollbackAnchor();
            _pipeline.ExecuteTick(_tickController);
            LatestSynchronizedServerTick = _pipeline.LocalSimulationTick - 1;
        }

        public void QueueInitialSpawn(in Unit.UnitSpawnRequest request)
        {
            _pipeline.QueueInitialSpawn(request);
        }

        public CommandTargetTickResolver CreateCommandTargetTickResolver()
        {
            return new CommandTargetTickResolver(
                () => CurrentTick,
                () => LatestSynchronizedServerTick,
                MinCommandLeadTicks,
                MaxFutureCommandTicks);
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
