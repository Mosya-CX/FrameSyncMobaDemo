using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// The sole ordinary action-start boundary. It knows capabilities,
    /// control blocks and structural resources, but no named hero rules.
    /// </summary>
    public sealed class ActionArbiter
    {
        private readonly Unit _owner;
        private readonly ActionStartSpecResolver _specResolver;
        private readonly ActionHandlerStarter _handlerStarter;
        private readonly ActionRuntimeReconciler _runtimeReconciler;

        public ActionArbiter(Unit owner)
        {
            _owner = owner;
            _specResolver = new ActionStartSpecResolver(owner);
            _handlerStarter = new ActionHandlerStarter(owner);
            _runtimeReconciler = new ActionRuntimeReconciler(owner);
        }

        public ActionSubmitResult Submit(ActionRequest request)
        {
            ActionResource occupiedBefore =
                _owner.ActionRuntimes?.OccupiedResources ??
                ActionResource.None;
            if (request == null)
                return Reject(
                    request,
                    ActionRejectReason.InvalidRequest,
                    occupiedBefore);
            if (!HasCapability(request))
                return Reject(
                    request,
                    ActionRejectReason.MissingCapability,
                    occupiedBefore);
            if (!_specResolver.TryResolve(
                    request,
                    out ActionStartSpec spec,
                    out ActionRejectReason reason))
                return Reject(request, reason, occupiedBefore);
            if (IsActionBlockedByControl(request, spec))
                return Reject(
                    request,
                    ActionRejectReason.BlockedByControl,
                    occupiedBefore);

            bool preemptMain = false;
            bool preemptBase = false;
            if (!EvaluateSlotConflict(
                    request,
                    spec,
                    ActionSlot.Main,
                    ref preemptMain,
                    out reason) ||
                !EvaluateSlotConflict(
                    request,
                    spec,
                    ActionSlot.Base,
                    ref preemptBase,
                    out reason))
                return Reject(request, reason, occupiedBefore);

            bool isControlAction = IsControlRequest(request);
            if (!_handlerStarter.TryStart(request, isControlAction))
                return Reject(
                    request,
                    ActionRejectReason.HandlerRejected,
                    occupiedBefore);

            if (preemptMain)
                CompletePreemption(
                    ActionSlot.Main,
                    request,
                    MoveCancelReason.AbilityCastStarted);
            if (preemptBase)
                CompletePreemption(
                    ActionSlot.Base,
                    request,
                    request.Kind == ActionKind.Attack
                        ? MoveCancelReason.AttackStarted
                        : MoveCancelReason.AbilityCastStarted);

            UnitUid target = request is AttackActionRequest attack
                ? attack.TargetUnit
                : default;
            byte abilitySlot = request is CastActionRequest cast
                ? (byte)cast.AbilityId
                : (byte)0;
            bool endedBySignal = request is CastActionRequest &&
                !_owner.AbilityHandler.IsActionStageActive(abilitySlot);
            bool migratedCast = request is CastActionRequest &&
                ReleaseMatchingCastOutsideSlot(abilitySlot, spec.Slot);
            if (endedBySignal)
                _owner.ActionRuntimes.RefreshFromHandlers();
            else
            {
                _owner.ActionRuntimes.Start(
                    request.Kind,
                    spec,
                    target,
                    abilitySlot,
                    isControlAction);
                if (request is CastActionRequest)
                    RefreshRuntimeStateFromHandlers();
            }
            if (spec.Slot != ActionSlot.None)
                _owner.CrowdControl?.OnOwnerActionStarted();
            ActionSubmitResult granted = ActionSubmitResult.Grant(
                spec,
                preemptMain || preemptBase || migratedCast);
            Trace(request, granted, occupiedBefore);
            return granted;
        }

        /// <summary>
        /// Reconciles authored Handler stage transitions with the fixed action
        /// slots. This is required for automatic transitions (for example a
        /// Hold timeout entering Release) that do not submit a new Intent.
        /// </summary>
        public void RefreshRuntimeStateFromHandlers()
        {
            _runtimeReconciler.Refresh();
        }

        public bool CancelAbility(byte abilitySlot)
        {
            if (_owner.AbilityHandler == null) return false;
            bool handled = _owner.AbilityHandler.HandleSignal(
                new AbilitySignal
                {
                    Slot = abilitySlot,
                    Verb = AbilitySignalVerb.Cancel,
                    Aim = AimSnapshot.None,
                });
            RefreshRuntimeStateFromHandlers();
            return handled;
        }

        private void CompletePreemption(
            ActionSlot slot,
            ActionRequest replacement,
            MoveCancelReason reason)
        {
            if (!_owner.ActionRuntimes.TryGet(
                    slot,
                    out ActionRuntimeSlotSnapshot previous))
                return;
            bool handlerAlreadyReplaced =
                previous.Kind == replacement.Kind &&
                (replacement.Kind == ActionKind.Move ||
                 replacement.Kind == ActionKind.Attack ||
                 (replacement is CastActionRequest cast &&
                  previous.AbilitySlot == (byte)cast.AbilityId));
            if (handlerAlreadyReplaced)
                _owner.ActionRuntimes.ReleaseSlotWithoutCancel(slot);
            else
                _owner.ActionRuntimes.CancelSlot(slot, reason);
        }

        private bool ReleaseMatchingCastOutsideSlot(
            byte abilitySlot,
            ActionSlot destination)
        {
            ActionSlot source = destination == ActionSlot.Main
                ? ActionSlot.Base
                : ActionSlot.Main;
            if (!_owner.ActionRuntimes.TryGet(
                    source,
                    out ActionRuntimeSlotSnapshot state) ||
                state.Kind != ActionKind.Cast ||
                state.AbilitySlot != abilitySlot)
                return false;
            _owner.ActionRuntimes.ReleaseSlotWithoutCancel(source);
            return true;
        }

        private ActionSubmitResult Reject(
            ActionRequest request,
            ActionRejectReason reason,
            ActionResource occupiedBefore)
        {
            ActionSubmitResult rejected =
                ActionSubmitResult.Reject(reason);
            Trace(request, rejected, occupiedBefore);
            return rejected;
        }

        private void Trace(
            ActionRequest request,
            in ActionSubmitResult result,
            ActionResource occupiedBefore)
        {
            // Keep the deterministic per-Tick path allocation-free unless the
            // optional diagnostic worker has explicitly been started.
            if (!FrameSyncDiagnostics.IsRunning) return;

            var record = new ActionDecisionTraceRecord(
                SimulationTickContext.Current.Tick,
                _owner.UnitUid,
                _owner.Intent.Kind,
                request?.Kind ?? ActionKind.None,
                result.Outcome,
                result.RejectReason,
                result.StartSpec.Slot,
                result.StartSpec.RequiredFreeResources,
                occupiedBefore);
            FrameSyncDiagnostics.LogTrace(
                $"[ActionDecision] tick={record.LogicTick} " +
                $"unit={record.UnitUid} intent={record.IntentKind} " +
                $"request={record.ActionKind} outcome={record.Outcome} " +
                $"reason={record.RejectReason} slot={record.Slot} " +
                $"required={record.RequiredResources} " +
                $"occupied={record.OccupiedBefore}");
        }

        public void OnIntentReplaced(
            in UnitIntent previous,
            in UnitIntent next)
        {
            if (SameBehavior(previous, next))
                return;
            if (_owner.AttackHandler == null ||
                !_owner.AttackHandler.IsAttackCycleActive ||
                _owner.AttackHandler.ImpactCommitted)
                return;
            if (_owner.ActionRuntimes.TryGet(
                    ActionSlot.Main,
                    out ActionRuntimeSlotSnapshot main) &&
                main.Kind == ActionKind.Attack)
            {
                _owner.ActionRuntimes.CancelSlot(
                    ActionSlot.Main,
                    MoveCancelReason.NewRoute);
                return;
            }
            // Handler-direct test/setup compatibility: the Arbiter still owns
            // the interruption decision even when no Runtime token was made.
            _owner.AttackHandler.CancelBeforeCommit();
        }

        /// <summary>
        /// Runs after this Tick's CC aggregation and before planning/Handler
        /// advance, so a newly blocked runtime cannot advance once more.
        /// </summary>
        public void EvaluateCurrentRuntimes()
        {
            if (_owner.ActionRuntimes == null || _owner.CrowdControl == null)
                return;
            CrowdControlStateView control = _owner.CrowdControl.State;
            EvaluateBlockedSlot(ActionSlot.Main, control.BlockedActions);
            EvaluateBlockedSlot(ActionSlot.Base, control.BlockedActions);
        }

        private void EvaluateBlockedSlot(
            ActionSlot slot,
            UnitActionBlockMask blocked)
        {
            if (!_owner.ActionRuntimes.TryGet(
                    slot,
                    out ActionRuntimeSlotSnapshot state))
                return;
            bool mustCancel = state.Kind switch
            {
                ActionKind.Move => (blocked &
                    (state.IsControlAction
                        ? UnitActionBlockMask.ControlMove
                        : UnitActionBlockMask.VoluntaryMove)) != 0,
                ActionKind.Attack => (blocked &
                    (state.IsControlAction
                        ? UnitActionBlockMask.ControlAttack
                        : UnitActionBlockMask.VoluntaryAttack)) != 0,
                ActionKind.Cast => (blocked &
                    (UnitActionBlockMask.AbilityCast |
                     ((state.OccupiedResources & ActionResource.Movement) != 0
                         ? UnitActionBlockMask.Mobility
                         : UnitActionBlockMask.None))) != 0,
                _ => false,
            };
            if (mustCancel)
                _owner.ActionRuntimes.CancelSlot(
                    slot,
                    MoveCancelReason.ControlInterrupt);
        }

        private bool EvaluateSlotConflict(
            ActionRequest request,
            in ActionStartSpec spec,
            ActionSlot existingSlot,
            ref bool preempt,
            out ActionRejectReason reason)
        {
            reason = ActionRejectReason.None;
            if (!_owner.ActionRuntimes.TryGet(
                    existingSlot,
                    out ActionRuntimeSlotSnapshot existing))
                return true;

            bool sameCastContinuation =
                request is CastActionRequest cast &&
                existing.Kind == ActionKind.Cast &&
                existing.AbilitySlot == (byte)cast.AbilityId &&
                existingSlot == spec.Slot;
            if (sameCastContinuation)
                return true;

            bool sameSlot = existingSlot == spec.Slot;
            bool resourceConflict =
                (existing.OccupiedResources &
                 spec.RequiredFreeResources) != ActionResource.None;
            if (!sameSlot && !resourceConflict)
                return true;
            if (!existing.Interruptible &&
                spec.InterruptLevel < ActionInterruptLevel.Forced)
            {
                reason = ActionRejectReason.ActiveActionUninterruptible;
                return false;
            }
            preempt = true;
            return true;
        }

        private bool HasCapability(ActionRequest request)
        {
            if (IsControlRequest(request))
            {
                return request.Kind switch
                {
                    ActionKind.Move => _owner.AbilityMask.HasMovement,
                    ActionKind.Attack => _owner.AbilityMask.HasAttack,
                    _ => false,
                };
            }
            ref readonly CapabilityState cap = ref _owner.CapabilityState;
            return request.Kind switch
            {
                ActionKind.Move => cap.CanMove,
                ActionKind.Attack => cap.CanAttack,
                ActionKind.Cast => cap.CanCast,
                _ => false,
            };
        }

        private bool IsActionBlockedByControl(
            ActionRequest request,
            in ActionStartSpec spec)
        {
            if (_owner.CrowdControl == null) return false;
            CrowdControlStateView state = _owner.CrowdControl.State;
            switch (request)
            {
                case MoveActionRequest move:
                    return (state.BlockedActions &
                        (move.Purpose == MovePurpose.ControlMove
                            ? UnitActionBlockMask.ControlMove
                            : UnitActionBlockMask.VoluntaryMove)) != 0;
                case AttackActionRequest attack:
                    bool controlAttack = _owner.CrowdControl
                        .TryGetBehaviorOverride(out CrowdControlBehaviorOverride behavior) &&
                        behavior.Kind == CrowdControlBehaviorKind.AttackTarget &&
                        behavior.TargetUnitUid == attack.TargetUnit;
                    return (state.BlockedActions &
                        (controlAttack
                            ? UnitActionBlockMask.ControlAttack
                            : UnitActionBlockMask.VoluntaryAttack)) != 0;
                case CastActionRequest:
                    UnitActionBlockMask castBlocks =
                        UnitActionBlockMask.AbilityCast;
                    if (spec.Slot == ActionSlot.Base &&
                        (spec.OccupiedResources &
                         ActionResource.Movement) != 0)
                        castBlocks |= UnitActionBlockMask.Mobility;
                    return (state.BlockedActions & castBlocks) != 0;
                default:
                    return true;
            }
        }

        private bool IsControlRequest(ActionRequest request)
        {
            return ActionRequestClassifier.IsControl(_owner, request);
        }

        private static bool SameBehavior(
            in UnitIntent left,
            in UnitIntent right)
        {
            if (left.Kind != right.Kind) return false;
            return left.Kind switch
            {
                IntentKind.AttackTarget => left.TargetUnit == right.TargetUnit,
                IntentKind.CastAbility => left.AbilityId == right.AbilityId &&
                    left.AbilityVerb == right.AbilityVerb &&
                    left.AbilityAim == right.AbilityAim,
                IntentKind.MoveToPosition =>
                    left.TargetPosition.Equals(right.TargetPosition),
                _ => true,
            };
        }
    }
}
