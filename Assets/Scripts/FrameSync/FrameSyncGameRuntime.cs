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

        public Unit.UnitWorld UnitWorld { get; }
        public PhysicsWorld PhysicsWorld { get; }
        public Unit.CombatSystem CombatSystem { get; }
        public MatchRuleRuntime MatchRule { get; }
        public GoldIncomeRuntime GoldIncome { get; }
        public CommandCollector CommandCollector => _pipeline.CommandCollector;
        public int CurrentTick => _pipeline.LocalSimulationTick;
        public uint LastChecksum => _pipeline.LastChecksum;
        public SimulationTickPipeline TickPipeline => _pipeline;

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
                config.RandomSeed)
        {
            var minionSystem = new Unit.MinionSystem(
                unitWorld, 0, config.MinionWaveIntervalTicks);
            var jungleCampSystem = new Unit.JungleCampSystem(
                unitWorld,
                new Unit.JungleCampTiming(
                    config.JungleResetTimeoutTicks,
                    config.JungleResetDurationTicks,
                    config.JungleRespawnDelayTicks));
            _pipeline.MinionSystem = minionSystem;
            _pipeline.JungleCampSystem = jungleCampSystem;
            _pipeline.NonHeroHelper = new NonHeroRestoreHelper(
                unitWorld, minionSystem, jungleCampSystem);
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
            uint randomSeed)
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
                new SnapshotStore(), _pipeline, _tickController);
        }

        public void SubmitCommand(GameplayCommand command)
        {
            _pipeline.SubmitCommand(command);
        }

        public void ExecuteOneTick()
        {
            _rollbackCoordinator.EnsureRollbackAnchor();
            _pipeline.ExecuteTick(_tickController);
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
