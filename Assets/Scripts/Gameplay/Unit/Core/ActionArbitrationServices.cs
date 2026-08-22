using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Converts typed requests and authored ability stages into structural
    /// arbitration specs. It owns no lifecycle state.
    /// </summary>
    internal sealed class ActionStartSpecResolver
    {
        private readonly Unit _owner;

        public ActionStartSpecResolver(Unit owner) { _owner = owner; }

        public bool TryResolve(
            ActionRequest request,
            out ActionStartSpec spec,
            out ActionRejectReason reason)
        {
            spec = default;
            reason = ActionRejectReason.None;
            switch (request)
            {
                case MoveActionRequest:
                    bool controlMove =
                        ActionRequestClassifier.IsControl(_owner, request);
                    if (!controlMove &&
                        _owner.AbilityHandler != null &&
                        _owner.AbilityHandler.IsCastMovementLocked())
                    {
                        reason = ActionRejectReason.BlockedByActiveCast;
                        return false;
                    }
                    spec = new ActionStartSpec(
                        ActionSlot.Base,
                        ActionResource.BaseAction |
                            ActionResource.Movement |
                            ActionResource.Facing,
                        ActionResource.BaseAction |
                            ActionResource.Movement |
                            ActionResource.Facing,
                        controlMove
                            ? ActionInterruptLevel.Forced
                            : ActionInterruptLevel.Ordinary,
                        true,
                        false);
                    return true;

                case AttackActionRequest:
                    bool controlAttack =
                        ActionRequestClassifier.IsControl(_owner, request);
                    if (!controlAttack &&
                        _owner.AbilityHandler != null &&
                        _owner.AbilityHandler.IsCastMovementLocked())
                    {
                        reason = ActionRejectReason.BlockedByActiveCast;
                        return false;
                    }
                    spec = new ActionStartSpec(
                        ActionSlot.Main,
                        ActionResource.MainAction |
                            ActionResource.Attack |
                            ActionResource.Facing,
                        ActionResource.MainAction |
                            ActionResource.Attack |
                            ActionResource.Facing,
                        controlAttack
                            ? ActionInterruptLevel.Forced
                            : ActionInterruptLevel.Ordinary,
                        true,
                        false);
                    return true;

                case CastActionRequest cast:
                    if (cast.AbilityId < byte.MinValue ||
                        cast.AbilityId > byte.MaxValue ||
                        _owner.AbilityHandler == null ||
                         !_owner.AbilityHandler
                            .TryDescribeRequestedStageForArbitration(
                            (byte)cast.AbilityId,
                            cast.Verb,
                             cast.Aim,
                             out CastStage stage,
                             out bool isDash,
                             out bool ownsActionRuntime))
                    {
                        reason = ActionRejectReason.InvalidAbilityStage;
                        return false;
                    }
                    spec = BuildCastSpec(
                        in stage,
                        isDash,
                        ownsActionRuntime);
                    return true;

                default:
                    reason = ActionRejectReason.InvalidRequest;
                    return false;
            }
        }

        public static ActionStartSpec BuildCastSpec(
            in CastStage stage,
            bool isDash,
            bool ownsActionRuntime = true)
        {
            if (!ownsActionRuntime)
                return new ActionStartSpec(
                    ActionSlot.None,
                    ActionResource.None,
                    ActionResource.None,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);

            if (isDash)
                return new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement,
                    ActionResource.BaseAction |
                        ActionResource.Movement,
                    ActionInterruptLevel.Ordinary,
                    stage.Interruptible,
                    false);

            ActionResource facing = stage.LockMovement
                ? ActionResource.Facing
                : ActionResource.None;
            return new ActionStartSpec(
                ActionSlot.Main,
                ActionResource.MainAction |
                    ActionResource.Ability |
                    facing,
                ActionResource.MainAction |
                    ActionResource.Ability |
                    facing,
                ActionInterruptLevel.Ordinary,
                stage.Interruptible,
                stage.LockMovement);
        }
    }

    /// <summary>
    /// Narrow adapter that starts the existing mechanism authorities after an
    /// Arbiter grant. Route/attack/ability rules remain Handler-owned.
    /// </summary>
    internal sealed class ActionHandlerStarter
    {
        private readonly Unit _owner;

        public ActionHandlerStarter(Unit owner) { _owner = owner; }

        public bool TryStart(ActionRequest request, bool isControlAction)
        {
            switch (request)
            {
                case MoveActionRequest move:
                    if (_owner.Locomotion == null)
                    {
                        if (_owner.MovementHandler == null) return false;
                        _owner.MovementHandler.ApplyMoveInput(
                            new MoveIntent(
                                move.TargetPosition -
                                _owner.MovementHandler.Position));
                        return true;
                    }
                    RouteMoveRequest route = move.ChaseTarget.IsValid()
                        ? RouteMoveRequest.FollowUnit(
                            move.ChaseTarget,
                            move.StopRange,
                            move.Purpose)
                        : RouteMoveRequest.ToPosition(
                            move.TargetPosition,
                            move.StopRange);
                    route.Purpose = move.Purpose;
                    route.AllowRVO = true;
                    MoveAcceptResult result =
                        _owner.Locomotion.AcceptRouteRequest(route);
                    if (result != MoveAcceptResult.Accepted &&
                        result != MoveAcceptResult.Rejected_AlreadyActive)
                        return false;
                    if (_owner.AttackHandler != null &&
                        _owner.AttackHandler.IsAttackCycleActive &&
                        _owner.AttackHandler.ImpactCommitted)
                        _owner.AttackHandler.ResetAttackTimer(
                            AttackTimerResetReason.MoveCancelRecovery);
                    return true;

                case AttackActionRequest attack:
                    if (_owner.AttackHandler == null) return false;
                    _owner.AttackHandler.ApplyAttackInput(
                        attack.TargetUnit,
                        isControlAction);
                    return _owner.AttackHandler.IsAttackCycleActive &&
                        !_owner.AttackHandler.ImpactCommitted &&
                        _owner.AttackHandler.CurrentTargetUid ==
                            attack.TargetUnit;

                case CastActionRequest cast:
                    return _owner.AbilityHandler != null &&
                        _owner.AbilityHandler.HandleSignal(new AbilitySignal
                        {
                            Slot = (byte)cast.AbilityId,
                            Verb = cast.Verb,
                            Aim = cast.Aim,
                        });
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Synchronizes fixed Runtime reservations with Handler-owned automatic
    /// stage transitions. It contains no request eligibility policy.
    /// </summary>
    internal sealed class ActionRuntimeReconciler
    {
        private readonly Unit _owner;

        public ActionRuntimeReconciler(Unit owner) { _owner = owner; }

        public void Refresh()
        {
            ActionRuntimeSet runtimes = _owner.ActionRuntimes;
            if (runtimes == null) return;

            runtimes.RefreshFromHandlers();
            int mainAbility = runtimes.Main.IsOccupied &&
                runtimes.Main.Kind == ActionKind.Cast
                    ? runtimes.Main.AbilitySlot
                    : -1;
            int baseAbility = runtimes.Base.IsOccupied &&
                runtimes.Base.Kind == ActionKind.Cast
                    ? runtimes.Base.AbilitySlot
                    : -1;
            if (mainAbility >= 0)
                RefreshCast(ActionSlot.Main, (byte)mainAbility);
            if (baseAbility >= 0)
                RefreshCast(ActionSlot.Base, (byte)baseAbility);
        }

        private void RefreshCast(ActionSlot currentSlot, byte abilitySlot)
        {
            ActionRuntimeSet runtimes = _owner.ActionRuntimes;
            if (!runtimes.TryGet(
                    currentSlot,
                    out ActionRuntimeSlotSnapshot current) ||
                current.Kind != ActionKind.Cast ||
                current.AbilitySlot != abilitySlot)
                return;
            if (_owner.AbilityHandler == null ||
                !_owner.AbilityHandler.TryDescribeActiveStage(
                    abilitySlot,
                    out CastStage stage,
                    out bool isDash))
                throw new DeterministicSimulationException(
                    $"Active Cast runtime for ability slot {abilitySlot} " +
                    "has no describable authored stage.");

            ActionStartSpec next =
                ActionStartSpecResolver.BuildCastSpec(in stage, isDash);
            ActionSlot conflictSlot = currentSlot == next.Slot
                ? (next.Slot == ActionSlot.Main
                    ? ActionSlot.Base
                    : ActionSlot.Main)
                : next.Slot;
            if (runtimes.TryGet(
                    conflictSlot,
                    out ActionRuntimeSlotSnapshot conflict) &&
                (currentSlot != next.Slot ||
                 (conflict.OccupiedResources & next.RequiredFreeResources) != 0))
            {
                if (!conflict.Interruptible)
                    throw new DeterministicSimulationException(
                        $"Ability slot {abilitySlot} stage transition to " +
                        $"{next.Slot} conflicts with uninterruptible " +
                        $"{conflict.Kind} runtime in {conflictSlot}.");
                runtimes.CancelSlot(
                    conflictSlot,
                    MoveCancelReason.AbilityCastStarted);
            }

            if (currentSlot != next.Slot)
                runtimes.ReleaseSlotWithoutCancel(currentSlot);
            runtimes.Start(
                ActionKind.Cast,
                next,
                abilitySlot: abilitySlot);
        }
    }

    internal static class ActionRequestClassifier
    {
        public static bool IsControl(Unit owner, ActionRequest request)
        {
            if (request is MoveActionRequest move)
                return move.Purpose == MovePurpose.ControlMove;
            return request is AttackActionRequest attack &&
                owner.CrowdControl != null &&
                owner.CrowdControl.TryGetBehaviorOverride(
                    out CrowdControlBehaviorOverride behavior) &&
                behavior.Kind == CrowdControlBehaviorKind.AttackTarget &&
                behavior.TargetUnitUid == attack.TargetUnit;
        }
    }
}
