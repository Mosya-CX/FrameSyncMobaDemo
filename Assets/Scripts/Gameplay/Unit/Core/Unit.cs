using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Sirenix.OdinInspector;
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
        private readonly List<UnitTag> tags =
            new List<UnitTag>();

        /// <summary>Deterministic runtime identity (SpawnLogicTick /
        /// prefab id / spawn sequence). Displayed in the Inspector for
        /// debugging spawned unit instances.</summary>
        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-120)]
        public UnitUid UnitUid { get; private set; }
        public GameplayParticipantId GameplayParticipantId { get; private set; }
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
        /// <summary>
        /// The deterministic home spawn position captured when this runtime
        /// instance was first materialized. Heroes respawn here after death.
        /// </summary>
        public fp2 RespawnPosition { get; private set; }
        public IReadOnlyList<UnitTag> Tags => tags;

        // ---- Lightweight invisible tags (UnitTag) ----

        /// <summary>
        /// True when a tag with the given key is currently alive on this
        /// Unit (used for deduplication, e.g. one R infection per target).
        /// </summary>
        public bool HasTag(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            for (int i = 0;
                 i < tags.Count;
                 i++)
            {
                if (string.Equals(
                        tags[i].Key,
                        key,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGetTag(
            string key,
            out UnitTag tag)
        {
            if (!string.IsNullOrEmpty(key))
            {
                for (int i = 0;
                     i < tags.Count;
                     i++)
                {
                    if (string.Equals(
                            tags[i].Key,
                            key,
                            StringComparison.Ordinal))
                    {
                        tag = tags[i];
                        return true;
                    }
                }
            }
            tag = default;
            return false;
        }

        /// <summary>
        /// Add or replace a tag. The same key with a different Uid (e.g. a
        /// second R cast) replaces the old tag and starts a fresh lifetime;
        /// the same Uid refreshes the remaining ticks.
        /// </summary>
        public void AddTag(
            string key,
            int durationTicks,
            in UnitTagUid uid)
        {
            if (string.IsNullOrEmpty(key) ||
                !uid.IsValid)
            {
                return;
            }
            for (int i = 0;
                 i < tags.Count;
                 i++)
            {
                if (string.Equals(
                        tags[i].Key,
                        key,
                        StringComparison.Ordinal))
                {
                    if (tags[i].Uid == uid)
                    {
                        UnitTag refreshed = tags[i];
                        refreshed.RemainingTicks =
                            durationTicks;
                        tags[i] = refreshed;
                    }
                    else
                    {
                        tags[i] = new UnitTag
                        {
                            Key = key,
                            RemainingTicks =
                                durationTicks,
                            Uid = uid,
                        };
                    }
                    return;
                }
            }
            tags.Add(new UnitTag
            {
                Key = key,
                RemainingTicks =
                    durationTicks,
                Uid = uid,
            });
        }

        public void RemoveTag(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            for (int i = tags.Count - 1;
                 i >= 0;
                 i--)
            {
                if (string.Equals(
                        tags[i].Key,
                        key,
                        StringComparison.Ordinal))
                {
                    tags.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Advances every tag lifetime by one Tick and removes expired tags.
        /// Called by the simulation pipeline in the Handler Tick phase.
        /// </summary>
        public void TickTags()
        {
            for (int i = tags.Count - 1;
                 i >= 0;
                 i--)
            {
                UnitTag tag = tags[i];
                if (tag.RemainingTicks > 0)
                {
                    tag.RemainingTicks--;
                    tags[i] = tag;
                    if (tag.RemainingTicks <= 0)
                    {
                        tags.RemoveAt(i);
                    }
                }
            }
        }

        public UnitTag[] CaptureTags()
        {
            tags.Sort();
            return tags.ToArray();
        }

        public void RestoreTags(UnitTag[] state)
        {
            tags.Clear();
            if (state != null)
            {
                tags.AddRange(state);
                tags.Sort();
            }
        }

        public void ClearTags()
        {
            tags.Clear();
        }

        public UnitActionStateView GetActionStateView()
        {
            if (LifeState == LifeState.Dead || LifeState == LifeState.Respawning)
                return UnitActionStateView.Dead;

            ActionKind mainKind = ActionRuntimes?.MainKind ?? ActionKind.None;
            ActionKind baseKind = ActionRuntimes?.BaseKind ?? ActionKind.None;
            bool isActing = mainKind != ActionKind.None ||
                baseKind != ActionKind.None;

            ActionMainKind animMain = mainKind switch
            {
                ActionKind.Attack => ActionMainKind.Attack,
                ActionKind.Cast => ActionMainKind.Cast,
                ActionKind.Move => ActionMainKind.Move,
                _ when baseKind == ActionKind.Move => ActionMainKind.Move,
                _ => ActionMainKind.Idle,
            };

            ActionBaseKind animBase = CrowdControl is not null && CrowdControl.ActiveForcedMoveHandle.IsValid
                ? ActionBaseKind.ForcedMove
                : baseKind == ActionKind.Move
                    ? ActionBaseKind.Move
                    : baseKind == ActionKind.Cast
                        ? ActionBaseKind.Dash
                        : ActionBaseKind.Idle;

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
            ReplaceIntent(intent);
        }

        public void ReplaceIntent(in UnitIntent intent)
        {
            if (Planner == null)
                throw new InvalidOperationException(
                    $"Unit {UnitUid} has no BehaviorPlanner.");
            UnitIntent previous = Planner.CurrentIntent;
            Arbiter?.OnIntentReplaced(previous, intent);
            Planner.ReplaceIntent(intent);
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
            (LifeState == LifeState.Alive || LifeState == LifeState.Dying) &&
            !(BuffHandler?.HasTag(GameplayBuffTags.Ghosting) ?? false);

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
            GameplayParticipantId gameplayParticipantId,
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
            if (!gameplayParticipantId.IsValid)
                throw new DeterministicSimulationException(
                    "Unit runtime requires a valid GameplayParticipantId.");

            ResolveComponentReferences();
            abilityMask = prototype.Loadout.BuildAbilityMask();
            ValidateCompositionOrThrow();

            UnitUid = unitUid;
            GameplayParticipantId = gameplayParticipantId;
            OwnerUid = ownerUid;
            UnitKind = prototype.UnitKind;
            UnitSubKindId = prototype.UnitSubKindId;
            TeamId = teamId;
            UnitPrototypeId = prototype.UnitPrototypeId;
            BaseGoldValue = prototype.BaseGoldValue;
            BaseExperienceValue = prototype.BaseExperienceValue;
            RespawnPosition = startPosition;
            LifeState = LifeState.Alive;
            capabilityState = CapabilityState.CreateAliveDefault();
            controlledByPlayerSlot = -1;
            HitReaction = default;
            EventBus = new UnitEventBus(this);

            Planner = new BehaviorPlanner(this);
            Arbiter = new ActionArbiter(this);
            ActionRuntimes = new ActionRuntimeSet(this);
            Intent = UnitIntent.None;
            tags.Clear();

            BindHandlersInStableOrder();
            BuffHandler.SetInitialBuffConfigs(
                prototype.InitialBuffConfigIds);

            StatPreset preset = prototype.BaseStats ?? new StatPreset();
            statHandler.InitializeRuntime(
                statDefinitions, preset, unitUid, 1, statGrowthC, statGrowthD,
                preset.LevelExperience ?? LevelExperienceConfig.Disabled);
            CombatModifiers = new CombatModifierSet(this);
            physicsEntity.SetLogicShape(CreatePhysicsShape(prototype.PhysicsProfile));
            physicsEntity.SetLogicPose(startPosition, prototype.PhysicsProfile.InitialForward);
            movementHandler?.InitializeRuntime(
                startPosition,
                prototype.LocomotionProfile.BaseMoveSpeed);
            attackHandler?.InitializeForNewRuntime(
                tickRate, attackSequenceResetIntervalTicks);
            abilityHandler?.InitializeForNewRuntime();
            buffHandler.InitializeForNewRuntime();
            crowdControlHandler?.InitializeForNewRuntime();
            equipmentHandler?.InitializeForNewRuntime();

        }

        internal ref CapabilityState RefCapabilityState() => ref capabilityState;

        /// <summary>
        /// Rebuild the coarse CapabilityState from LifeState plus the control
        /// system's aggregated BlockedActions (Unit Framework v27.3 1.9/8.4).
        /// Called in the pipeline fixed phase after CrowdControlHandler
        /// advances, and after rollback Rebuild.
        /// </summary>
        public void RefreshCapabilityState()
        {
            if (LifeState == LifeState.Dead ||
                LifeState == LifeState.Respawning)
            {
                capabilityState.DisableAllActions();
                return;
            }

            // Unit Framework v27.3 1.7/1.9: the default action capability is
            // derived from the authored HandlerLoadout (abilityMask), not a
            // hand-typed per-unit flag. Towers configure HasMovement=0, so
            // CanMove/CanTurn are false for them; minions configure
            // HasAbility=0, so CanCast is false. Rotation stays tied to
            // movement (CanTurn mirrors CanMove).
            capabilityState =
                CapabilityState.CreateAliveDefault();
            capabilityState.CanMove =
                abilityMask.HasMovement;
            capabilityState.CanAttack =
                abilityMask.HasAttack;
            capabilityState.CanCast =
                abilityMask.HasAbility;
            capabilityState.CanTurn =
                abilityMask.HasMovement;
            if (CrowdControl == null)
            {
                return;
            }
            UnitActionBlockMask blocked =
                CrowdControl.State.BlockedActions;
            if ((blocked &
                 UnitActionBlockMask.VoluntaryMove) != 0)
            {
                capabilityState.CanMove = false;
            }
            if ((blocked &
                 UnitActionBlockMask.VoluntaryAttack) != 0)
            {
                capabilityState.CanAttack = false;
            }
            if ((blocked &
                 UnitActionBlockMask.AbilityCast) != 0)
            {
                capabilityState.CanCast = false;
            }
            if ((blocked &
                 UnitActionBlockMask.Turn) != 0)
            {
                capabilityState.CanTurn = false;
            }
        }

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
            GameplayParticipantId expectedGameplayParticipantId,
            UnitUid ownerUid,
            UnitKind unitKind,
            ushort unitSubKindId,
            TeamId teamId,
            int unitPrototypeId,
            LifeState lifeState,
            in CapabilityState restoredCapabilityState,
            in HitReactionState restoredHitReactionState,
            fp2 restoredRespawnPosition)
        {
            if (UnitUid != expectedUnitUid ||
                GameplayParticipantId != expectedGameplayParticipantId ||
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
            RespawnPosition = restoredRespawnPosition;
        }

        internal void CaptureActionRuntimeState(
            ref ActionRuntimeSetSnapshot snapshot)
        {
            ActionRuntimes?.Capture(ref snapshot);
        }

        internal void RestoreActionRuntimeState(
            in ActionRuntimeSetSnapshot snapshot)
        {
            ActionRuntimes?.Restore(snapshot);
        }

        internal void ResolveActionRuntimeState()
        {
            ActionRuntimes?.Resolve();
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
            movementHandler?.ClearForDeath();
            attackHandler?.ClearForDeath();
            abilityHandler?.ClearForDeath();
            buffHandler.ClearForDeath();
            crowdControlHandler?.ClearForDeath();
            equipmentHandler?.ClearForDeath();
            ClearTags();
            Locomotion?.CancelRoute(MoveCancelReason.Death);
            Planner?.ClearForDeath();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }

        internal void ClearForRespawn()
        {
            statHandler.ClearForRespawn();
            movementHandler?.ClearForRespawn();
            attackHandler?.ClearForRespawn();
            abilityHandler?.ClearForRespawn();
            buffHandler.ClearForRespawn();
            crowdControlHandler?.ClearForRespawn();
            equipmentHandler?.ClearForRespawn();
            ClearTags();
            Planner?.ClearForRespawn();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }

        internal void ResetForPool()
        {
            statHandler.ResetForPool();
            movementHandler?.ResetForPool();
            attackHandler?.ResetForPool();
            abilityHandler?.ResetForPool();
            buffHandler.ResetForPool();
            crowdControlHandler?.ResetForPool();
            equipmentHandler?.ResetForPool();
            ClearTags();
            CombatModifiers?.Clear();
            LifeState = LifeState.Alive;
            capabilityState = CapabilityState.CreateAliveDefault();
            HitReaction = default;
            Locomotion = null;
            EventBus = null;
            UnitUid = default;
            GameplayParticipantId = default;
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
            // Unit Framework v27.3 1.7: only BuffHandler and StatHandler are
            // universal. Movement / Attack / Ability presence must match the
            // authored HandlerLoadout (e.g. towers have no MovementHandler or
            // AbilityHandler; minions have no AbilityHandler).
            RequireExactlyOne(buffHandler, nameof(BuffHandler));
            if ((movementHandler != null) !=
                abilityMask.HasMovement)
                throw new InvalidOperationException(
                    $"Unit prefab '{name}' MovementHandler presence disagrees with its HandlerLoadout.");
            if ((attackHandler != null) !=
                abilityMask.HasAttack)
                throw new InvalidOperationException(
                    $"Unit prefab '{name}' AttackHandler presence disagrees with its HandlerLoadout.");
            if ((abilityHandler != null) !=
                abilityMask.HasAbility)
                throw new InvalidOperationException(
                    $"Unit prefab '{name}' AbilityHandler presence disagrees with its HandlerLoadout.");
        }

        private void BindHandlersInStableOrder()
        {
            statHandler.BindOwner(this);
            movementHandler?.BindOwner(this);
            attackHandler?.BindOwner(this);
            abilityHandler?.BindOwner(this);
            buffHandler.BindOwner(this);
            crowdControlHandler?.BindOwner(this);
            equipmentHandler?.BindOwner(this);
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
