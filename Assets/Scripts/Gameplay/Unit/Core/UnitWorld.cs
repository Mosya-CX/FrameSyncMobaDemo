using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitWorld
    {
        private readonly UnitRegistry registry = new UnitRegistry();
        private readonly List<UnitAIController> aiControllers = new List<UnitAIController>();
        private readonly List<JungleCamp> jungleCamps =
            new List<JungleCamp>();
        private bool isTickingAIControllers;
        private int currentSequenceLogicTick = -1;
        private byte nextSpawnSequenceInTick;
        private bool spawnSequenceExhausted;
        private int runtimeRevision;

        public GlobalUnitPrototypeTable UnitPrototypeTable { get; set; }
        public GlobalPrefabTable GlobalPrefabTable { get; set; }
        public StatDefinitionTable StatDefinitionTable { get; set; }
        public EquipmentDatabase EquipmentDatabase { get; set; }
        public AbilityDefinitionRegistry AbilityDefinitions { get; set; }
        public BuffDefinitionRegistry BuffDefinitions { get; set; }
        public PhysicsWorld PhysicsWorld { get; set; }
        public fp StatGrowthC { get; set; }
        public fp StatGrowthD { get; set; }
        public int TickRate { get; set; }
        public int AttackSequenceResetIntervalTicks { get; set; } = 90;
        public RespawnTimer RespawnTimer { get; set; }
        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public PathGridMap2D PathGrid { get; set; }
        public FlowFieldRegistry FlowFieldRegistry { get; set; }
        public CombatSystem CombatSystem { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public RangeQueryService RangeQuery { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public IReadOnlyList<UnitAIController> AIControllers => aiControllers;
        public IReadOnlyList<JungleCamp> JungleCamps => jungleCamps;
        public MinionSystem MinionSystem { get; set; }
        public int RuntimeRevision => runtimeRevision;

        public void RegisterAIController(UnitAIController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (!RegisterAIController(
                    controller.OwnerUnitUid,
                    controller))
                throw new DeterministicSimulationException(
                    $"Cannot register AI for {controller.OwnerUnitUid}.");
        }

        public void UnregisterAIController(UnitAIController controller)
        {
            if (controller == null) return;
            UnregisterAIController(
                controller.OwnerUnitUid);
        }

        // Design-aligned overloads (NonHero v5 ?2.2)
        public bool RegisterAIController(UnitUid ownerUnitUid, UnitAIController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (isTickingAIControllers)
                throw new DeterministicSimulationException(
                    "AI controller registration cannot change while ticking.");
            if (controller.OwnerUnitUid != ownerUnitUid ||
                !TryGetUnit(ownerUnitUid, out _))
                return false;
            int index = 0;
            while (index < aiControllers.Count &&
                   aiControllers[index].OwnerUnitUid
                       .CompareTo(ownerUnitUid) < 0)
                index++;
            if (index < aiControllers.Count &&
                aiControllers[index].OwnerUnitUid ==
                ownerUnitUid)
                return false;
            aiControllers.Insert(index, controller);
            return true;
        }

        public bool UnregisterAIController(UnitUid ownerUnitUid)
        {
            if (isTickingAIControllers)
                throw new DeterministicSimulationException(
                    "AI controller registration cannot change while ticking.");
            for (int i = 0; i < aiControllers.Count; i++)
                if (aiControllers[i].OwnerUnitUid == ownerUnitUid)
                {
                    aiControllers.RemoveAt(i);
                    return true;
                }
            return false;
        }

        public bool TryGetAIController(UnitUid ownerUnitUid, out UnitAIController controller)
        {
            for (int i = 0; i < aiControllers.Count; i++)
                if (aiControllers[i].OwnerUnitUid == ownerUnitUid)
                { controller = aiControllers[i]; return true; }
            controller = null; return false;
        }

        public void TickAIControllers()
        {
            if (isTickingAIControllers)
                throw new DeterministicSimulationException(
                    "AI controller ticking is not reentrant.");
            isTickingAIControllers = true;
            try
            {
                for (int i = 0;
                     i < aiControllers.Count;
                     i++)
                {
                    UnitAIController controller =
                        aiControllers[i];
                    if (!TryGetUnit(
                            controller.OwnerUnitUid,
                            out Unit owner) ||
                        owner.LifeState !=
                            LifeState.Alive ||
                        !owner
                            .CanRunActiveGameplayThisTick)
                        continue;
                    controller.AIThink();
                }
            }
            finally
            {
                isTickingAIControllers = false;
            }
        }

        public void RegisterJungleCamp(
            JungleCamp camp)
        {
            if (camp == null)
                throw new ArgumentNullException(
                    nameof(camp));
            int index = 0;
            while (index < jungleCamps.Count &&
                   jungleCamps[index].CampId <
                   camp.CampId)
                index++;
            if (index < jungleCamps.Count &&
                jungleCamps[index].CampId ==
                camp.CampId)
                throw new InvalidOperationException(
                    $"Duplicate JungleCamp id {camp.CampId}.");
            jungleCamps.Insert(index, camp);
        }

        public bool TryGetJungleCamp(
            int campId,
            out JungleCamp camp)
        {
            for (int i = 0;
                 i < jungleCamps.Count;
                 i++)
            {
                if (jungleCamps[i].CampId ==
                    campId)
                {
                    camp = jungleCamps[i];
                    return true;
                }
            }
            camp = null;
            return false;
        }

        public void TickJungleCamps()
        {
            for (int i = 0;
                 i < jungleCamps.Count;
                 i++)
                jungleCamps[i].TickLogic();
        }

        public bool TryGetUnit(UnitUid unitUid, out Unit unit) => registry.TryGet(unitUid, out unit);
        public IReadOnlyList<Unit> GetAllUnits() => registry.GetAll();
        public IReadOnlyList<Unit> GetUnitsByKind(UnitKind kind) => registry.GetByKind(kind);
        public IReadOnlyList<Unit> GetUnitsBySubKind(UnitKind kind, ushort subKindId) => registry.GetBySubKind(kind, subKindId);
        public IReadOnlyList<Unit> GetUnitsByTeam(TeamId teamId) => registry.GetByTeam(teamId);

        public UnitUid SpawnUnit(in UnitSpawnRequest request)
        {
            RequireSpawnDependencies();

            if (!UnitPrototypeTable.TryGet(request.UnitPrototypeId, out UnitPrototype prototype))
            {
                throw new InvalidOperationException(
                    $"No UnitPrototype with id {request.UnitPrototypeId} is registered.");
            }

            GameObject prefab = GlobalPrefabTable.GetRequiredPrefab(
                PrefabKind.Unit, prototype.RuntimeEntityPrefabId);
            byte spawnSequence = AllocateSpawnSequence();
            int spawnTick = SimulationTickContext.Current.Tick;
            var unitUid = new UnitUid(
                spawnTick, prototype.RuntimeEntityPrefabId, spawnSequence);

            GameObject instance = null;
            PhysicsEntity2D physicsEntity = null;
            bool physicsRegistered = false;
            bool unitRegistered = false;

            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                Unit unit = instance.GetComponent<Unit>();
                if (unit == null)
                {
                    throw new InvalidOperationException(
                        $"Unit prefab id {prototype.RuntimeEntityPrefabId} must have Unit on its root GameObject.");
                }

                unit.InitializeForNewRuntime(
                    unitUid,
                    request.OwnerUid,
                    prototype,
                    request.TeamId,
                    StatDefinitionTable,
                    StatGrowthC,
                    StatGrowthD,
                    TickRate,
                    AttackSequenceResetIntervalTicks,
                    request.Position);
                unit.EquipmentHandler.DefinitionDatabase = EquipmentDatabase;
                unit.World = this;
                unit.AbilityHandler.DefinitionRegistry = AbilityDefinitions;
                unit.AbilityHandler.InitializeConfiguredLoadoutOrThrow();
                unit.BuffHandler.DefinitionRegistry = BuffDefinitions;

                physicsEntity = unit.PhysicsEntity;
                physicsEntity.SetLogicPose(request.Position, request.Forward);
                physicsEntity.SetQueryInfo(new PhysicsEntityQueryInfo(
                    new RuntimeUidQueryValue(
                        unitUid.SpawnLogicTick,
                        unitUid.RuntimeEntityPrefabId,
                        unitUid.SpawnSequenceInTick),
                    PhysicsEntityKind.Unit,
                    request.TeamId.Value,
                    unit));

                PhysicsWorld.RegisterUnit(physicsEntity);
                physicsRegistered = true;
                RegisterUnit(unit);
                unitRegistered = true;
                runtimeRevision++;

                if (PathGrid != null)
                {
                    unit.Locomotion = new UnitLocomotionAgent(unit, PathGrid);
                }

                return unitUid;
            }
            catch
            {
                if (unitRegistered && instance != null)
                {
                    Unit registeredUnit = instance.GetComponent<Unit>();
                    if (registeredUnit != null)
                    {
                        UnregisterUnit(registeredUnit);
                    }
                }

                if (physicsRegistered && physicsEntity != null)
                {
                    PhysicsWorld.UnregisterUnit(physicsEntity);
                }

                DestroyFailedInstance(instance);
                throw;
            }
        }

        public void CleanupNonHeroDeath(UnitAIController controller)
        {
            if (controller == null) return;
            controller.ClearForDeath();
            UnregisterAIController(controller);
        }

        public void FinalizeNonHeroDeath(Unit unit)
        {
            if (unit == null ||
                !TryGetAIController(
                    unit.UnitUid,
                    out UnitAIController controller))
                return;

            if (controller is MinionAIController)
            {
                MinionSystem?.UnregisterManagedUnit(
                    unit.UnitUid);
            }
            else if (controller is
                     MonsterAIController monster)
            {
                if (!TryGetJungleCamp(
                        monster.CampId,
                        out JungleCamp camp))
                    throw new DeterministicSimulationException(
                        $"Dead monster {unit.UnitUid} references missing camp {monster.CampId}.");
                camp.OnMemberDeath(unit.UnitUid);
            }

            controller.ClearForDeath();
            UnregisterAIController(unit.UnitUid);
        }
        /// <summary>
        /// Synchronously remove a Unit without triggering death events, death rewards,
        /// or kill statistics. Used for summon expiration, owner removal, scripted
        /// cleanup, and match cleanup.
        ///
        /// Design: Unit Framework v27.3 ?7.12, ?9.6.1
        /// </summary>
        public bool DespawnUnit(in UnitDespawnRequest request)
        {
            if (!request.UnitUid.IsValid()) return false;
            if (!TryGetUnit(request.UnitUid, out Unit unit)) return false;

            // 1. Stop active behaviours, intents, and movement.
            unit.Planner?.ClearIntent();
            unit.ActionRuntimes?.CancelAll();
            unit.Locomotion?.ClearForDeath();
            unit.MovementHandler?.ClearForDeath();

            // 2. Notify all Handlers that this is a non-death removal.
            unit.CrowdControl?.ClearForDespawn(request.Reason);
            unit.AttackHandler?.ClearForDespawn(request.Reason);
            unit.AbilityHandler?.ClearForDespawn(request.Reason);
            unit.BuffHandler?.ClearForDespawn(request.Reason);
            unit.EquipmentHandler?.ClearForDespawn(request.Reason);
            unit.StatHandler?.ClearForDespawn(request.Reason);
            unit.MovementHandler?.ClearForDespawn(request.Reason);

            // 3. Full cleanup: shields, modifiers, control, and runtime state.
            unit.StatHandler?.ClearModifiers();
            unit.CombatModifiers?.Clear();
            unit.CrowdControl?.ClearForDeath();

            // 4. Notify non-hero management (minion / jungle camp).
            unit.BuffHandler?.ClearForDespawn(request.Reason);

            // 5. Unregister AI controller.
            for (int i = aiControllers.Count - 1; i >= 0; i--)
            {
                if (aiControllers[i].OwnerUnitUid == request.UnitUid)
                {
                    aiControllers[i].ClearForDeath();
                    aiControllers.RemoveAt(i);
                }
            }

            // 6. Unregister physics and unit registry.
            if (unit.PhysicsEntity != null)
                PhysicsWorld?.UnregisterUnit(unit.PhysicsEntity);
            UnregisterUnit(unit);

            // 7. Dispose GameObject per mode.
            switch (request.Mode)
            {
                case UnitDespawnMode.Pool:
                    unit.ResetForPool();
                    unit.gameObject.SetActive(false);
                    break;
                case UnitDespawnMode.Destroy:
                    unit.gameObject.SetActive(false);
                    if (UnityEngine.Application.isPlaying)
                        UnityEngine.Object.Destroy(unit.gameObject);
                    else
                        UnityEngine.Object.DestroyImmediate(unit.gameObject);
                    break;
            }

            runtimeRevision++;
            return true;
        }


        public UnitAIController ReconstructAIController(in UnitAIControllerSnapshot state)
        {
            if (!TryGetUnit(state.OwnerUnitUid, out Unit owner))
            {
                throw new DeterministicSimulationException(
                    $"AI snapshot references missing owner {state.OwnerUnitUid}.");
            }
            return state.ControllerKind switch
            {
                UnitAIControllerKind.Minion => new MinionAIController(owner, state.LaneId),
                UnitAIControllerKind.Monster => new MonsterAIController(
                    owner,
                    state.CampId,
                    state.MonsterCampSlotIndex),
                UnitAIControllerKind.Tower => new TowerAIController(owner),
                _ => throw new DeterministicSimulationException(
                    $"AI snapshot has invalid controller kind {state.ControllerKind}."),
            };
        }

        internal void ClearAIControllersForRestore()
        {
            aiControllers.Clear();
        }

        internal Unit CreateUnitForRollbackRestore(
            UnitUid unitUid,
            UnitUid ownerUid,
            int unitPrototypeId,
            TeamId teamId,
            fp2 position,
            fp2 forward)
        {
            RequireSpawnDependencies();
            if (!unitUid.IsValid())
                throw new DeterministicSimulationException("Cannot restore an invalid UnitUid.");
            if (!UnitPrototypeTable.TryGet(unitPrototypeId, out UnitPrototype prototype))
                throw new DeterministicSimulationException(
                    $"Unit snapshot references missing prototype {unitPrototypeId}.");
            if (prototype.RuntimeEntityPrefabId != unitUid.RuntimeEntityPrefabId)
                throw new DeterministicSimulationException(
                    $"Unit {unitUid} prototype/prefab identity mismatch.");

            GameObject prefab = GlobalPrefabTable.GetRequiredPrefab(
                PrefabKind.Unit, prototype.RuntimeEntityPrefabId);
            GameObject instance = null;
            PhysicsEntity2D physicsEntity = null;
            bool physicsRegistered = false;
            bool unitRegistered = false;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                Unit unit = instance.GetComponent<Unit>();
                if (unit == null)
                    throw new DeterministicSimulationException(
                        $"Restored Unit prefab {prototype.RuntimeEntityPrefabId} has no Unit root.");
                unit.InitializeForNewRuntime(
                    unitUid, ownerUid, prototype, teamId, StatDefinitionTable,
                    StatGrowthC, StatGrowthD, TickRate,
                    AttackSequenceResetIntervalTicks, position);
                unit.EquipmentHandler.DefinitionDatabase = EquipmentDatabase;
                unit.World = this;
                unit.AbilityHandler.DefinitionRegistry = AbilityDefinitions;
                unit.AbilityHandler.InitializeConfiguredLoadoutOrThrow();
                unit.BuffHandler.DefinitionRegistry = BuffDefinitions;
                physicsEntity = unit.PhysicsEntity;
                physicsEntity.SetLogicPose(position, forward);
                physicsEntity.SetQueryInfo(new PhysicsEntityQueryInfo(
                    new RuntimeUidQueryValue(
                        unitUid.SpawnLogicTick,
                        unitUid.RuntimeEntityPrefabId,
                        unitUid.SpawnSequenceInTick),
                    PhysicsEntityKind.Unit,
                    teamId.Value,
                    unit));
                PhysicsWorld.RegisterUnit(physicsEntity);
                physicsRegistered = true;
                RegisterUnit(unit);
                unitRegistered = true;
                if (PathGrid != null) unit.Locomotion = new UnitLocomotionAgent(unit, PathGrid);
                if (FlowFieldRegistry != null) unit.Locomotion?.SetFlowFieldRegistry(FlowFieldRegistry);
                runtimeRevision++;
                return unit;
            }
            catch
            {
                if (unitRegistered && instance != null)
                {
                    Unit registered = instance.GetComponent<Unit>();
                    if (registered != null) UnregisterUnit(registered);
                }
                if (physicsRegistered && physicsEntity != null)
                    PhysicsWorld.UnregisterUnit(physicsEntity);
                DestroyFailedInstance(instance);
                throw;
            }
        }

        internal void RemoveUnitForRollbackRestore(Unit unit)
        {
            if (unit == null) return;
            for (int i = aiControllers.Count - 1; i >= 0; i--)
                if (aiControllers[i].OwnerUnitUid == unit.UnitUid)
                    aiControllers.RemoveAt(i);
            if (unit.PhysicsEntity != null) PhysicsWorld?.UnregisterUnit(unit.PhysicsEntity);
            UnregisterUnit(unit);
            unit.gameObject.SetActive(false);
            DestroyFailedInstance(unit.gameObject);
            runtimeRevision++;
        }

        internal void RestoreRuntimeRevision(int revision)
        {
            if (revision < 0)
                throw new DeterministicSimulationException("UnitWorld RuntimeRevision cannot be negative.");
            runtimeRevision = revision;
        }

        internal byte AllocateSpawnSequence()
        {
            int tick = SimulationTickContext.Current.Tick;
            if (currentSequenceLogicTick != tick)
            {
                currentSequenceLogicTick = tick;
                nextSpawnSequenceInTick = 0;
                spawnSequenceExhausted = false;
            }

            if (spawnSequenceExhausted)
            {
                throw new DeterministicSimulationException("Unit spawn sequence overflow.");
            }

            byte result = nextSpawnSequenceInTick;
            if (nextSpawnSequenceInTick == byte.MaxValue)
            {
                spawnSequenceExhausted = true;
            }
            else
            {
                nextSpawnSequenceInTick++;
            }

            return result;
        }

        public void RequestEnterDying(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            ValidateTransition(unit, LifeState.Dying, LifeState.Alive);
            unit.ApplyLifeStateFromUnitWorld(LifeState.Dying);
        }

        public void RequestRecoverFromDying(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            ValidateTransition(unit, LifeState.Alive, LifeState.Dying);
            unit.ApplyLifeStateFromUnitWorld(LifeState.Alive);
        }

        public void ConfirmUnitDeath(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            ValidateTransition(unit, LifeState.Dead, LifeState.Dying);

            // Pathfinding Design v13.1 section 11.10 �?formal death cleanup chain
            // Each module clears only its own runtime state.
            unit.CrowdControl?.ClearForDeath();
            unit.MovementHandler?.ClearForDeath();
            unit.Locomotion?.ClearForDeath();

            unit.ApplyLifeStateFromUnitWorld(LifeState.Dead);
            ref CapabilityState capability = ref unit.RefCapabilityState();
            capability.DisableAllActions();
        }

        public void BeginRespawn(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            ValidateTransition(unit, LifeState.Respawning, LifeState.Dead);
            unit.ApplyLifeStateFromUnitWorld(LifeState.Respawning);
        }

        public void CompleteRespawn(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            ValidateTransition(unit, LifeState.Alive, LifeState.Respawning);
            unit.ApplyLifeStateFromUnitWorld(LifeState.Alive);
            ref CapabilityState capability = ref unit.RefCapabilityState();
            capability.ResetAliveDefault();

            // Apply respawn health and resource rules from UnitPrototype config (Unit v27.3 ��1.6)
            if (UnitPrototypeTable != null
                && UnitPrototypeTable.TryGet(unit.UnitPrototypeId, out UnitPrototype prototype))
            {
                ApplyRespawnHealth(unit, prototype.RespawnConfig);
                ApplyRespawnResource(unit, prototype.RespawnConfig);
            }
        }

        private static void ApplyRespawnHealth(Unit unit, in UnitRespawnConfig config)
        {
            if (unit.StatHandler == null) return;
            fp maxHp = unit.StatHandler.GetStat(StatId.MaxHealth);
            fp newHp;
            switch (config.HealthRule)
            {
                case RespawnHealthRule.FullHealth:
                    newHp = maxHp;
                    break;
                case RespawnHealthRule.PercentOfMax:
                    newHp = maxHp * (fp)config.HealthRespawnValue / (fp)100;
                    break;
                case RespawnHealthRule.FixedValue:
                    newHp = (fp)config.HealthRespawnValue;
                    if (newHp > maxHp) newHp = maxHp;
                    break;
                default:
                    newHp = maxHp;
                    break;
            }
            unit.StatHandler.SetCurrentHealth(newHp);
        }

        /// <summary>
        /// Resolve post-death disposal based on the UnitPrototype's DisposePolicy.
        /// Called after death animation completes. For heroes this is deferred;
        /// for non-hero units this happens immediately on ConfirmUnitDeath.
        /// Design: Unit Framework v27.3 section 9.6, UnitDisposePolicy.
        /// </summary>
        public void ResolveDeathDispose(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (unit.LifeState != LifeState.Dead) return;

            UnitDisposePolicyConfig policy = UnitDisposePolicyConfig.Default;
            if (UnitPrototypeTable != null
                && UnitPrototypeTable.TryGet(unit.UnitPrototypeId, out UnitPrototype prototype)
                && prototype.UnitDisposePolicyId != 0)
            {
                policy = ResolveDisposePolicy(unit, prototype);
            }
            else
            {
                if (unit.UnitKind == UnitKind.Hero)
                {
                    policy.Kind = UnitDisposePolicyKind.KeepAlive;
                }
                else
                {
                    policy.Kind = UnitDisposePolicyKind.Destroy;
                }
            }

            switch (policy.Kind)
            {
                case UnitDisposePolicyKind.KeepAlive:
                    break;

                case UnitDisposePolicyKind.Pool:
                    UnregisterUnit(unit);
                    if (unit.PhysicsEntity != null)
                        PhysicsWorld?.UnregisterUnit(unit.PhysicsEntity);
                    unit.ResetForPool();
                    unit.gameObject.SetActive(false);
                    break;

                case UnitDisposePolicyKind.Destroy:
                    UnregisterUnit(unit);
                    if (unit.PhysicsEntity != null)
                        PhysicsWorld?.UnregisterUnit(unit.PhysicsEntity);
                    unit.gameObject.SetActive(false);
                    DestroyFailedInstance(unit.gameObject);
                    break;

                case UnitDisposePolicyKind.SpawnRuin:
                    UnregisterUnit(unit);
                    if (unit.PhysicsEntity != null)
                        PhysicsWorld?.UnregisterUnit(unit.PhysicsEntity);
                    if (policy.RuinPrototypeId > 0
                        && UnitPrototypeTable != null
                        && UnitPrototypeTable.TryGet(policy.RuinPrototypeId, out _))
                    {
                        SpawnRuinUnit(policy.RuinPrototypeId, unit);
                    }
                    unit.gameObject.SetActive(false);
                    DestroyFailedInstance(unit.gameObject);
                    break;
            }
        }

        private UnitDisposePolicyConfig ResolveDisposePolicy(Unit unit, UnitPrototype prototype)
        {
            return unit.UnitKind switch
            {
                UnitKind.Hero => new UnitDisposePolicyConfig
                {
                    Kind = UnitDisposePolicyKind.KeepAlive,
                    RuinPrototypeId = 0,
                },
                UnitKind.Structure => new UnitDisposePolicyConfig
                {
                    Kind = UnitDisposePolicyKind.SpawnRuin,
                    RuinPrototypeId = prototype.UnitDisposePolicyId,
                },
                UnitKind.Minion or UnitKind.Monster => new UnitDisposePolicyConfig
                {
                    Kind = UnitDisposePolicyKind.Destroy,
                    RuinPrototypeId = 0,
                },
                _ => UnitDisposePolicyConfig.Default,
            };
        }

        private void SpawnRuinUnit(int ruinPrototypeId, Unit originalTower)
        {
            if (ruinPrototypeId <= 0 || UnitPrototypeTable == null) return;
            if (!UnitPrototypeTable.TryGet(ruinPrototypeId, out UnitPrototype ruinProto)) return;
            if (GlobalPrefabTable == null) return;

            fp2 position = originalTower.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
            var request = new UnitSpawnRequest(
                ruinPrototypeId,
                originalTower.TeamId,
                position,
                new fp2(fp.zero, fp.one),
                originalTower.OwnerUid);
            SpawnUnit(request);
        }

private static void ApplyRespawnResource(Unit unit, in UnitRespawnConfig config)
        {
            if (unit.StatHandler == null) return;
            fp maxRes = unit.StatHandler.GetStat(StatId.MaxCastResource);
            fp newRes;
            switch (config.ResourceRule)
            {
                case RespawnResourceRule.FullResource:
                    newRes = maxRes;
                    break;
                case RespawnResourceRule.PercentOfMax:
                    newRes = maxRes * (fp)config.ResourceRespawnValue / (fp)100;
                    break;
                case RespawnResourceRule.FixedValue:
                    newRes = (fp)config.ResourceRespawnValue;
                    if (newRes > maxRes) newRes = maxRes;
                    break;
                default:
                    newRes = maxRes;
                    break;
            }
            unit.StatHandler.SetCurrentCastResource(newRes);
        }

        internal void RegisterUnit(Unit unit)
        {
            registry.Register(unit);
            if (CombatEvents.TryResolveUnit == null)
            {
                CombatEvents.TryResolveUnit = uid => TryGetUnit(uid, out Unit resolved) ? resolved : null;
            }
        }

        internal void UnregisterUnit(Unit unit) => registry.Unregister(unit);

        public ExperienceGainResult GrantExperience(UnitUid unitUid, int amount)
        {
            if (!TryGetUnit(unitUid, out Unit unit) || unit.StatHandler == null)
                return ExperienceGainResult.None;

            ExperienceGainResult result = unit.StatHandler.AddExperience(amount);
            if (result.LeveledUp)
            {
                AbilityHandler ability = unit.AbilityHandler;
                if (ability != null)
                {
                    for (int i = 0; i < result.SkillPointsGained; i++)
                    {
                        ability.GrantSkillPoint();
                    }
                }

                for (int level = result.PreviousLevel; level < result.NewLevel; level++)
                    CombatEvents.RaiseLevelUp(unitUid, level, level + 1);
            }

            return result;
        }

        private void RequireSpawnDependencies()
        {
            if (UnitPrototypeTable == null)
            {
                throw new InvalidOperationException(
                    "UnitWorld.UnitPrototypeTable must be set before SpawnUnit.");
            }

            if (GlobalPrefabTable == null)
            {
                throw new InvalidOperationException(
                    "UnitWorld.GlobalPrefabTable must be set before SpawnUnit.");
            }

            if (StatDefinitionTable == null)
            {
                throw new InvalidOperationException(
                    "UnitWorld.StatDefinitionTable must be set before SpawnUnit.");
            }

            if (PhysicsWorld == null)
            {
                throw new InvalidOperationException(
                    "UnitWorld.PhysicsWorld must be set before SpawnUnit.");
            }

            if (TickRate <= 0)
            {
                throw new InvalidOperationException(
                    "UnitWorld.TickRate must be set before SpawnUnit.");
            }
            if (AttackSequenceResetIntervalTicks < 1)
            {
                throw new InvalidOperationException(
                    "UnitWorld.AttackSequenceResetIntervalTicks must be at least 1.");
            }
        }

        private static void ValidateTransition(Unit unit, LifeState target, LifeState requiredCurrent)
        {
            if (unit.LifeState != requiredCurrent)
            {
                throw new InvalidOperationException(
                    $"Illegal LifeState transition: Unit {unit.UnitUid} is in {unit.LifeState}, "
                    + $"but {target} requires {requiredCurrent}.");
            }
        }

        private static void DestroyFailedInstance(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
