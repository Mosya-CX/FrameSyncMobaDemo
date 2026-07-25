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
        public RespawnTimer RespawnTimer { get; set; }
        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public PathGridMap2D PathGrid { get; set; }
        public FlowFieldRegistry FlowFieldRegistry { get; set; }
        public CombatSystem CombatSystem { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public RangeQueryService RangeQuery { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public IReadOnlyList<UnitAIController> AIControllers => aiControllers;
        public int RuntimeRevision => runtimeRevision;

        public void RegisterAIController(UnitAIController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (!aiControllers.Contains(controller))
            {
                int index = aiControllers.Count;
                while (index > 0 &&
                    aiControllers[index - 1].OwnerUnitUid.CompareTo(controller.OwnerUnitUid) > 0)
                {
                    index--;
                }
                aiControllers.Insert(index, controller);
            }
        }

        public void UnregisterAIController(UnitAIController controller)
        {
            if (controller == null) return;
            aiControllers.Remove(controller);
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
                    request.Position);
                unit.EquipmentHandler.DefinitionDatabase = EquipmentDatabase;
                unit.World = this;
                unit.AbilityHandler.DefinitionRegistry = AbilityDefinitions;
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
                UnitAIControllerKind.Monster => new MonsterAIController(owner, state.CampId),
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
                    StatGrowthC, StatGrowthD, TickRate, position);
                unit.EquipmentHandler.DefinitionDatabase = EquipmentDatabase;
                unit.World = this;
                unit.AbilityHandler.DefinitionRegistry = AbilityDefinitions;
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

            // Pathfinding Design v13.1 section 11.10 — formal death cleanup chain
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
