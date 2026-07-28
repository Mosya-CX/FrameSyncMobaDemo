using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnitCollisionParticipant
    {
        [Header("Deterministic composition")]
        [Tooltip("Authoritative 2D physics component owned by this Unit prefab.")]
        [SerializeField] private PhysicsEntity2D physicsEntity;
        [SerializeField] private StatHandler statHandler;
        [SerializeField] private MovementHandler movementHandler;
        [SerializeField] private AttackHandler attackHandler;
        [SerializeField] private AbilityHandler abilityHandler;
        [SerializeField] private BuffHandler buffHandler;
        [SerializeField] private CrowdControlHandler crowdControlHandler;
        [SerializeField] private EquipmentHandler equipmentHandler;

        private CapabilityState capabilityState;
        private UnitAbilityMask abilityMask;

        public UnitUid UnitUid { get; private set; }
        public UnitWorld World { get; internal set; }
        public UnitUid OwnerUid { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public ushort UnitSubKindId { get; private set; }
        public TeamId TeamId { get; private set; }
        public int UnitPrototypeId { get; private set; }
        public int BaseGoldValue { get; private set; }
        public int BaseExperienceValue { get; private set; }
        public LifeState LifeState { get; private set; }
        public ref readonly CapabilityState CapabilityState => ref capabilityState;
        public UnitAbilityMask AbilityMask => abilityMask;

        public PhysicsEntity2D PhysicsEntity => physicsEntity;
        public StatHandler StatHandler => statHandler;
        public CombatModifierSet CombatModifiers { get; private set; }
        public MovementHandler MovementHandler => movementHandler;
        public AttackHandler AttackHandler => attackHandler;
        public AbilityHandler AbilityHandler => abilityHandler;
        public BuffHandler BuffHandler => buffHandler;
        public CrowdControlHandler CrowdControl => crowdControlHandler;
        public EquipmentHandler EquipmentHandler => equipmentHandler;
        public UnitEventBus EventBus { get; private set; }

        public UnitIntent Intent { get => Planner?.CurrentIntent ?? UnitIntent.None; internal set => Planner?.SetIntent(value); }
        public BehaviorPlanner Planner { get; private set; }
        public ActionArbiter Arbiter { get; private set; }
        public ActionRuntimeSet ActionRuntimes { get; private set; }

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;

        public UnitActionStateView GetActionStateView()
        {
            if (LifeState == LifeState.Dead || LifeState == LifeState.Respawning)
                return UnitActionStateView.Dead;

            ActionKind mainKind = ActionRuntimes?.MainKind ?? ActionKind.None;
            bool isActing = mainKind != ActionKind.None;

            ActionMainKind animMain = mainKind switch
            {
                ActionKind.Attack => ActionMainKind.Attack,
                ActionKind.Cast => ActionMainKind.Cast,
                ActionKind.Move => ActionMainKind.Move,
                _ => ActionMainKind.Idle,
            };

            ActionBaseKind animBase = CrowdControl is not null && CrowdControl.ActiveConstraint.IsActive
                ? ActionBaseKind.ForcedMove
                : mainKind == ActionKind.Move ? ActionBaseKind.Move : ActionBaseKind.Idle;

            return new UnitActionStateView(animMain, animBase, isActing);
        }

        public void ApplyOrder(in Order order)
        {
            if (Planner == null)
                throw new InvalidOperationException(
                    $"Unit {UnitUid} has no BehaviorPlanner.");
            UnitIntent intent =
                OrderTranslator.ToIntent(order);
            if (order.Kind == OrderKind.LaneAdvance)
            {
                if (World?.MinionSystem == null ||
                    !World.MinionSystem.TryGetLane(
                        order.LaneAdvance_LaneIndex,
                        out LaneRuntimeData lane) ||
                    !lane.TryGetAdvanceTarget(
                        TeamId,
                        out intent.TargetPosition))
                    throw new DeterministicSimulationException(
                        $"Unit {UnitUid} cannot resolve Lane {order.LaneAdvance_LaneIndex}.");
            }
            else if (order.Kind ==
                     OrderKind.ReturnToCamp)
            {
                if (World == null ||
                    !World.TryGetJungleCamp(
                        order.ReturnToCamp_CampId,
                        out JungleCamp camp) ||
                    !camp.TryGetMemberSpawnPosition(
                        UnitUid,
                        out intent.TargetPosition))
                    throw new DeterministicSimulationException(
                        $"Unit {UnitUid} cannot resolve JungleCamp {order.ReturnToCamp_CampId}.");
            }
            Planner.SetIntent(intent);
        }
        public HitReactionState HitReaction;

        [Tooltip("Local routing only. -1 means AI or unassigned; never enters Gameplay authority.")]
        [SerializeField] private int controlledByPlayerSlot = -1;
        public int ControlledByPlayerSlot
        {
            get => controlledByPlayerSlot;
            set => controlledByPlayerSlot = value;
        }

        public bool CanRunActiveGameplayThisTick =>
            SimulationTickContext.Current.Tick > UnitUid.SpawnLogicTick;

        bool IUnitCollisionParticipant.CanParticipateInUnitCollision =>
            LifeState == LifeState.Alive || LifeState == LifeState.Dying;

        void IUnitCollisionParticipant.PublishUnitCollisionEnter(
            RuntimeUidQueryValue otherUid,
            fp2 contactNormal)
        {
            EventBus?.PublishUnitCollisionEnter(new UnitCollisionEnterEvent(
                ToUnitUid(otherUid), contactNormal));
        }

        void IUnitCollisionParticipant.PublishUnitCollisionExit(
            RuntimeUidQueryValue otherUid)
        {
            EventBus?.PublishUnitCollisionExit(new UnitCollisionExitEvent(
                ToUnitUid(otherUid)));
        }

        private static UnitUid ToUnitUid(RuntimeUidQueryValue uid) =>
            new UnitUid(
                uid.SpawnLogicTick,
                uid.RuntimeEntityPrefabId,
                uid.SpawnSequenceInTick);

        internal void InitializeForNewRuntime(
            UnitUid unitUid,
            UnitUid ownerUid,
            UnitPrototype prototype,
            TeamId teamId,
            StatDefinitionTable statDefinitions,
            fp statGrowthC,
            fp statGrowthD,
            int tickRate,
            int attackSequenceResetIntervalTicks,
            fp2 startPosition)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            if (statDefinitions == null) throw new ArgumentNullException(nameof(statDefinitions));

            ResolveComponentReferences();
            ValidateCompositionOrThrow();

            UnitUid = unitUid;
            OwnerUid = ownerUid;
            UnitKind = prototype.UnitKind;
            UnitSubKindId = prototype.UnitSubKindId;
            TeamId = teamId;
            UnitPrototypeId = prototype.UnitPrototypeId;
            BaseGoldValue = prototype.BaseGoldValue;
            BaseExperienceValue = prototype.BaseExperienceValue;
            LifeState = LifeState.Alive;
            capabilityState = CapabilityState.CreateAliveDefault();
            controlledByPlayerSlot = -1;
            HitReaction = default;
            EventBus = new UnitEventBus(this);

            Planner = new BehaviorPlanner(this);
            Arbiter = new ActionArbiter(this);
            ActionRuntimes = new ActionRuntimeSet();
            abilityMask = prototype.Loadout.BuildAbilityMask();
            Intent = UnitIntent.None;

            BindHandlersInStableOrder();

            StatPreset preset = prototype.BaseStats ?? new StatPreset();
            statHandler.InitializeRuntime(
                statDefinitions, preset, unitUid, 1, statGrowthC, statGrowthD,
                preset.LevelExperience ?? LevelExperienceConfig.Disabled);
            CombatModifiers = new CombatModifierSet(this);
            physicsEntity.SetLogicShape(CreatePhysicsShape(prototype.PhysicsProfile));
            physicsEntity.SetLogicPose(startPosition, prototype.PhysicsProfile.InitialForward);
            movementHandler.InitializeRuntime(
                startPosition,
                prototype.LocomotionProfile.BaseMoveSpeed);
            attackHandler.InitializeForNewRuntime(
                tickRate, attackSequenceResetIntervalTicks);
            abilityHandler.InitializeForNewRuntime();
            buffHandler.InitializeForNewRuntime();
            crowdControlHandler.InitializeForNewRuntime();
            equipmentHandler.InitializeForNewRuntime();

        }

        internal ref CapabilityState RefCapabilityState() => ref capabilityState;

        private static FrameSyncMoba.Physics.PhysicsShape2D CreatePhysicsShape(
            in PhysicsProfile2D profile)
        {
            switch (profile.DefaultShape)
            {
                case PhysicsShapeKind.Point:
                    return FrameSyncMoba.Physics.PhysicsShape2D.CreatePoint(fp2.zero);
                case PhysicsShapeKind.Circle:
                    return FrameSyncMoba.Physics.PhysicsShape2D.CreateCircle(
                        fp2.zero, profile.ShapeParam);
                default:
                    throw new InvalidOperationException(
                        $"Unit PhysicsProfile shape {profile.DefaultShape} is not supported by the current scalar ShapeParam contract.");
            }
        }

        internal void ApplyLifeStateFromUnitWorld(LifeState newState)
        {
            LifeState = newState;
        }

        internal void RestoreCoreState(
            UnitUid expectedUnitUid,
            UnitUid ownerUid,
            UnitKind unitKind,
            ushort unitSubKindId,
            TeamId teamId,
            int unitPrototypeId,
            LifeState lifeState,
            in CapabilityState restoredCapabilityState,
            in HitReactionState restoredHitReactionState)
        {
            if (UnitUid != expectedUnitUid ||
                OwnerUid != ownerUid ||
                UnitKind != unitKind ||
                UnitSubKindId != unitSubKindId ||
                TeamId != teamId ||
                UnitPrototypeId != unitPrototypeId)
            {
                throw new DeterministicSimulationException(
                    $"Unit snapshot identity mismatch for {expectedUnitUid}.");
            }

            LifeState = lifeState;
            capabilityState = restoredCapabilityState;
            HitReaction = restoredHitReactionState;
        }

        internal void ValidateActionRuntimeSnapshotBoundary()
        {
            if (ActionRuntimes != null && ActionRuntimes.Count != 0)
            {
                throw new DeterministicSimulationException(
                    $"Unit {UnitUid} has live IActionRuntime state, but no restorable IActionRuntime snapshot contract exists.");
            }
        }

        internal void RestoreBehaviorState(in UnitIntent restoredIntent)
        {
            if (restoredIntent.Kind < IntentKind.None ||
                restoredIntent.Kind > IntentKind.ReturnToCamp)
            {
                throw new DeterministicSimulationException(
                    $"Unit {UnitUid} snapshot contains invalid IntentKind {(byte)restoredIntent.Kind}.");
            }
            if (restoredIntent.Kind == IntentKind.CastAbility &&
                (restoredIntent.AbilityVerb < AbilitySignalVerb.Focus ||
                 restoredIntent.AbilityVerb > AbilitySignalVerb.Cancel ||
                 restoredIntent.AbilityAim.Kind < AimKind.None ||
                 restoredIntent.AbilityAim.Kind > AimKind.Direction))
            {
                throw new DeterministicSimulationException(
                    $"Unit {UnitUid} snapshot contains invalid Ability intent data.");
            }
            if (restoredIntent.Kind == IntentKind.CastAbility &&
                restoredIntent.AbilityAim.Kind == AimKind.Direction &&
                fpmath.lengthsq(restoredIntent.AbilityAim.Direction) == fp.zero)
            {
                throw new DeterministicSimulationException(
                    $"Unit {UnitUid} snapshot contains a zero Ability aim direction.");
            }

            Planner.SetIntent(restoredIntent);
        }

        internal void ResolveBehaviorState()
        {
            UnitIntent intent = Intent;
            if (intent.Kind == IntentKind.AttackTarget)
            {
                if (!intent.TargetUnit.IsValid() ||
                    !World.TryGetUnit(intent.TargetUnit, out _))
                {
                    throw new DeterministicSimulationException(
                        $"Unit {UnitUid} restored AttackTarget intent references missing Unit {intent.TargetUnit}.");
                }
            }
            else if (intent.Kind == IntentKind.CastAbility &&
                     intent.AbilityAim.Kind == AimKind.Unit &&
                     (!intent.AbilityAim.TargetUnitUid.IsValid() ||
                      !World.TryGetUnit(
                          intent.AbilityAim.TargetUnitUid,
                          out _)))
            {
                throw new DeterministicSimulationException(
                    $"Unit {UnitUid} restored CastAbility intent references missing Unit {intent.AbilityAim.TargetUnitUid}.");
            }
        }

        internal void ClearForDeath()
        {
            // D-009: StatHandler and CombatModifiers survive ordinary death.
            // Stat modifiers survive, while StatHandler-owned shields do not.
            statHandler.ClearForDeath();
            movementHandler.ClearForDeath();
            attackHandler.ClearForDeath();
            abilityHandler.ClearForDeath();
            buffHandler.ClearForDeath();
            crowdControlHandler.ClearForDeath();
            equipmentHandler.ClearForDeath();
            Locomotion?.CancelRoute(MoveCancelReason.Death);
            Planner?.ClearForDeath();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }

        internal void ClearForRespawn()
        {
            statHandler.ClearForRespawn();
            movementHandler.ClearForRespawn();
            attackHandler.ClearForRespawn();
            abilityHandler.ClearForRespawn();
            buffHandler.ClearForRespawn();
            crowdControlHandler.ClearForRespawn();
            equipmentHandler.ClearForRespawn();
            Planner?.ClearForRespawn();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }

        internal void ResetForPool()
        {
            statHandler.ResetForPool();
            movementHandler.ResetForPool();
            attackHandler.ResetForPool();
            abilityHandler.ResetForPool();
            buffHandler.ResetForPool();
            crowdControlHandler.ResetForPool();
            equipmentHandler.ResetForPool();
            CombatModifiers?.Clear();
            LifeState = LifeState.Alive;
            capabilityState = CapabilityState.CreateAliveDefault();
            HitReaction = default;
            Locomotion = null;
            EventBus = null;
            UnitUid = default;
            World = null;
            OwnerUid = default;
            Planner = null;
            Arbiter = null;
            ActionRuntimes = null;
            Intent = UnitIntent.None;
            abilityMask = default;
        }

        internal void ResolveComponentReferences()
        {
            physicsEntity ??= GetComponentInChildren<PhysicsEntity2D>(true);
            statHandler ??= GetComponentInChildren<StatHandler>(true);
            movementHandler ??= GetComponentInChildren<MovementHandler>(true);
            attackHandler ??= GetComponentInChildren<AttackHandler>(true);
            abilityHandler ??= GetComponentInChildren<AbilityHandler>(true);
            buffHandler ??= GetComponentInChildren<BuffHandler>(true);
            crowdControlHandler ??= GetComponentInChildren<CrowdControlHandler>(true);
            equipmentHandler ??= GetComponentInChildren<EquipmentHandler>(true);
        }

        internal void ValidateCompositionOrThrow()
        {
            RequireExactlyOne(physicsEntity, nameof(PhysicsEntity2D));
            RequireExactlyOne(statHandler, nameof(StatHandler));
            RequireExactlyOne(movementHandler, nameof(MovementHandler));
            RequireExactlyOne(attackHandler, nameof(AttackHandler));
            RequireExactlyOne(abilityHandler, nameof(AbilityHandler));
            RequireExactlyOne(buffHandler, nameof(BuffHandler));
            RequireExactlyOne(crowdControlHandler, nameof(CrowdControlHandler));
            RequireExactlyOne(equipmentHandler, nameof(EquipmentHandler));
        }

        private void BindHandlersInStableOrder()
        {
            statHandler.BindOwner(this);
            movementHandler.BindOwner(this);
            attackHandler.BindOwner(this);
            abilityHandler.BindOwner(this);
            buffHandler.BindOwner(this);
            crowdControlHandler.BindOwner(this);
            equipmentHandler.BindOwner(this);
        }

        private void RequireExactlyOne<T>(T component, string label) where T : Component
        {
            T[] matches = GetComponentsInChildren<T>(true);
            if (component == null || matches.Length != 1 || matches[0] != component)
            {
                throw new InvalidOperationException(
                    $"Unit prefab '{name}' must contain exactly one {label} assigned to its Unit root; found {matches.Length}.");
            }
        }

        private void Reset()
        {
            ResolveComponentReferences();
        }

        private void OnValidate()
        {
            ResolveComponentReferences();
        }
    }
}
