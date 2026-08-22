using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Fixed Main/Base lifecycle ownership. Handler snapshots remain the
    /// mechanism authority; these slots own arbitration resources only.
    /// </summary>
    public sealed class ActionRuntimeSet
    {
        private readonly Unit _owner;
        private ActionRuntimeSlotSnapshot _main;
        private ActionRuntimeSlotSnapshot _base;

        public ActionRuntimeSet(Unit owner) { _owner = owner; }

        public int Count => (_main.IsOccupied ? 1 : 0) + (_base.IsOccupied ? 1 : 0);
        public ref readonly ActionRuntimeSlotSnapshot Main => ref _main;
        public ref readonly ActionRuntimeSlotSnapshot Base => ref _base;
        public ActionKind MainKind => _main.IsOccupied ? _main.Kind : ActionKind.None;
        public ActionKind BaseKind => _base.IsOccupied ? _base.Kind : ActionKind.None;
        public ActionResource OccupiedResources =>
            (_main.IsOccupied ? _main.OccupiedResources : ActionResource.None) |
            (_base.IsOccupied ? _base.OccupiedResources : ActionResource.None);

        public bool TryGet(ActionSlot slot, out ActionRuntimeSlotSnapshot state)
        {
            state = slot == ActionSlot.Main ? _main : _base;
            return state.IsOccupied;
        }

        public bool IsEquivalentMoveActive(MoveActionRequest request)
        {
            if (request == null ||
                !_base.IsOccupied ||
                _base.Kind != ActionKind.Move ||
                _owner.Locomotion == null)
                return false;
            ref readonly MovementTask task = ref
                _owner.Locomotion.CurrentTask;
            if (task.State != MovementTaskState.Active ||
                task.Purpose != request.Purpose ||
                task.StopDistance != request.StopRange)
                return false;
            if (request.ChaseTarget.IsValid())
                return task.Target.TargetUid.HasValue &&
                    task.Target.TargetUid.Value == request.ChaseTarget;
            return task.Target.Position.HasValue &&
                task.Target.Position.Value.Equals(request.TargetPosition);
        }

        public bool IsEquivalentAttackActive(AttackActionRequest request)
        {
            return request != null &&
                _main.IsOccupied &&
                _main.Kind == ActionKind.Attack &&
                _main.TargetUnitUid == request.TargetUnit &&
                _owner.AttackHandler != null &&
                _owner.AttackHandler.IsAttackCycleActive &&
                !_owner.AttackHandler.ImpactCommitted;
        }

        public void Start(
            ActionKind kind,
            in ActionStartSpec spec,
            UnitUid targetUnitUid = default,
            byte abilitySlot = 0,
            bool isControlAction = false)
        {
            var state = new ActionRuntimeSlotSnapshot
            {
                IsOccupied = true,
                Slot = spec.Slot,
                Kind = kind,
                Phase = kind switch
                {
                    ActionKind.Move => ActionRuntimePhase.Moving,
                    ActionKind.Attack => ActionRuntimePhase.AttackWindup,
                    ActionKind.Cast => ActionRuntimePhase.AbilityStage,
                    _ => ActionRuntimePhase.None,
                },
                OccupiedResources = spec.OccupiedResources,
                Interruptible = spec.Interruptible,
                BlocksVoluntaryMove = spec.BlocksVoluntaryMove,
                IsControlAction = isControlAction,
                TargetUnitUid = targetUnitUid,
                AbilitySlot = abilitySlot,
            };

            if (spec.Slot == ActionSlot.Main) _main = state;
            else if (spec.Slot == ActionSlot.Base) _base = state;
            else throw new DeterministicSimulationException(
                "An active ActionRuntime requires Main or Base slot ownership.");
        }

        public void CancelSlot(ActionSlot slot, MoveCancelReason moveReason)
        {
            ref ActionRuntimeSlotSnapshot state = ref GetSlot(slot);
            if (!state.IsOccupied) return;

            if (state.Kind == ActionKind.Move)
            {
                _owner.Locomotion?.CancelRoute(moveReason);
            }
            else if (state.Kind == ActionKind.Attack &&
                     _owner.AttackHandler != null &&
                     _owner.AttackHandler.IsAttackCycleActive &&
                     !_owner.AttackHandler.ImpactCommitted)
            {
                _owner.AttackHandler.CancelBeforeCommit();
            }
            else if (state.Kind == ActionKind.Cast)
            {
                _owner.AbilityHandler?.HandleSignal(new AbilitySignal
                {
                    Slot = state.AbilitySlot,
                    Verb = AbilitySignalVerb.Cancel,
                    Aim = AimSnapshot.None,
                });
            }

            state = ActionRuntimeSlotSnapshot.Empty;
        }

        public void RefreshFromHandlers()
        {
            RefreshSlot(ActionSlot.Main);
            RefreshSlot(ActionSlot.Base);
        }

        private void RefreshSlot(ActionSlot slot)
        {
            ref ActionRuntimeSlotSnapshot state = ref GetSlot(slot);
            if (!state.IsOccupied) return;

            bool remainsActive = state.Kind switch
            {
                ActionKind.Move => _owner.Locomotion != null &&
                    _owner.Locomotion.CurrentTask.State == MovementTaskState.Active,
                ActionKind.Attack => _owner.AttackHandler != null &&
                    _owner.AttackHandler.IsAttackCycleActive &&
                    !_owner.AttackHandler.ImpactCommitted,
                ActionKind.Cast => _owner.AbilityHandler != null &&
                    _owner.AbilityHandler.IsActionStageActive(state.AbilitySlot),
                _ => false,
            };
            if (!remainsActive) state = ActionRuntimeSlotSnapshot.Empty;
        }

        public void Capture(ref ActionRuntimeSetSnapshot snapshot)
        {
            snapshot.Main = _main;
            snapshot.Base = _base;
        }

        public void Restore(in ActionRuntimeSetSnapshot snapshot)
        {
            ValidateSlot(snapshot.Main, ActionSlot.Main);
            ValidateSlot(snapshot.Base, ActionSlot.Base);
            _main = snapshot.Main;
            _base = snapshot.Base;
        }

        public void Resolve()
        {
            ResolveSlot(in _main);
            ResolveSlot(in _base);
        }

        private void ResolveSlot(in ActionRuntimeSlotSnapshot state)
        {
            if (!state.IsOccupied) return;
            switch (state.Kind)
            {
                case ActionKind.Move:
                    if (_owner.Locomotion == null ||
                        _owner.Locomotion.CurrentTask.State != MovementTaskState.Active)
                        FailRestore(state, "has no active locomotion task");
                    break;
                case ActionKind.Attack:
                    if (!state.TargetUnitUid.IsValid() ||
                        _owner.World == null ||
                        !_owner.World.TryGetUnit(state.TargetUnitUid, out _) ||
                        _owner.AttackHandler == null ||
                        !_owner.AttackHandler.IsAttackCycleActive ||
                        _owner.AttackHandler.ImpactCommitted ||
                        _owner.AttackHandler.CurrentTargetUid !=
                            state.TargetUnitUid)
                        FailRestore(state, "has no matching attack windup");
                    break;
                case ActionKind.Cast:
                    CastStage stage = default;
                    bool isDash = false;
                    if (_owner.AbilityHandler == null ||
                        _owner.AbilityHandler.GetAbilityDef(state.AbilitySlot) == null ||
                        !_owner.AbilityHandler.TryDescribeActiveStage(
                            state.AbilitySlot,
                            out stage,
                            out isDash))
                        FailRestore(state, "has no matching active ability stage");
                    ActionSlot expectedSlot = isDash
                        ? ActionSlot.Base
                        : ActionSlot.Main;
                    ActionResource expectedResources = isDash
                        ? ActionResource.BaseAction |
                            ActionResource.Movement
                        : ActionResource.MainAction |
                            ActionResource.Ability |
                            (stage.LockMovement
                                ? ActionResource.Facing
                                : ActionResource.None);
                    if (state.Slot != expectedSlot ||
                        state.OccupiedResources != expectedResources ||
                        state.Interruptible != stage.Interruptible ||
                        state.BlocksVoluntaryMove != stage.LockMovement)
                        FailRestore(state, "disagrees with its authored ability stage");
                    break;
                default:
                    FailRestore(state, "contains an invalid action kind");
                    break;
            }
        }

        private void FailRestore(in ActionRuntimeSlotSnapshot state, string detail)
        {
            throw new DeterministicSimulationException(
                $"Unit {_owner.UnitUid} restored {state.Slot} ActionRuntime {state.Kind} {detail}.");
        }

        private static void ValidateSlot(
            in ActionRuntimeSlotSnapshot state,
            ActionSlot expectedSlot)
        {
            if (!state.IsOccupied)
            {
                if (state.Slot != ActionSlot.None ||
                    state.Kind != ActionKind.None ||
                    state.Phase != ActionRuntimePhase.None ||
                    state.OccupiedResources != ActionResource.None ||
                    state.Interruptible ||
                    state.BlocksVoluntaryMove ||
                    state.IsControlAction ||
                    state.TargetUnitUid != default ||
                    state.AbilitySlot != 0)
                    throw new DeterministicSimulationException(
                        $"Empty {expectedSlot} ActionRuntime contains state.");
                return;
            }
            ActionResource knownResources =
                ActionResource.MainAction |
                ActionResource.BaseAction |
                ActionResource.Movement |
                ActionResource.Facing |
                ActionResource.Attack |
                ActionResource.Ability;
            ActionRuntimePhase expectedPhase = state.Kind switch
            {
                ActionKind.Move => ActionRuntimePhase.Moving,
                ActionKind.Attack => ActionRuntimePhase.AttackWindup,
                ActionKind.Cast => ActionRuntimePhase.AbilityStage,
                _ => ActionRuntimePhase.None,
            };
            ActionResource ownedSlotResource = expectedSlot == ActionSlot.Main
                ? ActionResource.MainAction
                : ActionResource.BaseAction;
            ActionResource forbiddenSlotResource = expectedSlot == ActionSlot.Main
                ? ActionResource.BaseAction
                : ActionResource.MainAction;
            ActionSlot matrixSlot = state.Kind switch
            {
                ActionKind.Move => ActionSlot.Base,
                ActionKind.Attack => ActionSlot.Main,
                ActionKind.Cast => expectedSlot,
                _ => ActionSlot.None,
            };
            ActionResource matrixResources = state.Kind switch
            {
                ActionKind.Move => ActionResource.BaseAction |
                    ActionResource.Movement |
                    ActionResource.Facing,
                ActionKind.Attack => ActionResource.MainAction |
                    ActionResource.Attack |
                    ActionResource.Facing,
                ActionKind.Cast => state.OccupiedResources,
                _ => ActionResource.None,
            };
            if (state.Slot != expectedSlot ||
                state.Kind <= ActionKind.None ||
                state.Kind > ActionKind.Cast ||
                state.Phase != expectedPhase ||
                (state.OccupiedResources & ~knownResources) != 0 ||
                (state.OccupiedResources & ownedSlotResource) == 0 ||
                (state.OccupiedResources & forbiddenSlotResource) != 0 ||
                state.Slot != matrixSlot ||
                state.OccupiedResources != matrixResources ||
                (state.Kind == ActionKind.Attack &&
                 !state.TargetUnitUid.IsValid()) ||
                (state.Kind != ActionKind.Attack &&
                 state.TargetUnitUid != default) ||
                (state.Kind != ActionKind.Cast &&
                 state.AbilitySlot != 0) ||
                (state.Kind == ActionKind.Cast &&
                 state.IsControlAction) ||
                (state.Kind != ActionKind.Cast &&
                 (!state.Interruptible || state.BlocksVoluntaryMove)))
                throw new DeterministicSimulationException(
                    $"Invalid restored {expectedSlot} ActionRuntime state.");
        }

        private ref ActionRuntimeSlotSnapshot GetSlot(ActionSlot slot)
        {
            if (slot == ActionSlot.Main) return ref _main;
            if (slot == ActionSlot.Base) return ref _base;
            throw new DeterministicSimulationException(
                "ActionRuntime slot must be Main or Base.");
        }

        public void CancelAll()
        {
            CancelSlot(ActionSlot.Main, MoveCancelReason.Death);
            CancelSlot(ActionSlot.Base, MoveCancelReason.Death);
        }

        public void ReleaseSlotWithoutCancel(ActionSlot slot)
        {
            GetSlot(slot) = ActionRuntimeSlotSnapshot.Empty;
        }

        public void ClearWithoutCancel()
        {
            _main = ActionRuntimeSlotSnapshot.Empty;
            _base = ActionRuntimeSlotSnapshot.Empty;
        }
    }
}
