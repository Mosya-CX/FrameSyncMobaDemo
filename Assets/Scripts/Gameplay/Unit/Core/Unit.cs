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

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;
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

            BindHandlersInStableOrder();

            StatPreset preset = prototype.BaseStats ?? new StatPreset();
            statHandler.InitializeRuntime(
                statDefinitions, preset, unitUid, 1, statGrowthC, statGrowthD,
                preset.LevelExperience ?? LevelExperienceConfig.Disabled);
            CombatModifiers = new CombatModifierSet(this);
            movementHandler.InitializeRuntime(startPosition, fp.one);
            attackHandler.InitializeForNewRuntime(tickRate);
            abilityHandler.InitializeForNewRuntime();
            buffHandler.InitializeForNewRuntime();
            crowdControlHandler.InitializeForNewRuntime();
            equipmentHandler.InitializeForNewRuntime();

        }

        internal ref CapabilityState RefCapabilityState() => ref capabilityState;

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
