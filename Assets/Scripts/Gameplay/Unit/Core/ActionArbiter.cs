namespace FrameSyncMoba.Unit
{
    public enum ArbitrationResult : byte
    {
        Accepted,
        Rejected,
        Interrupt,
    }

    /// <summary>
    /// Unit-internal gate for ordinary behavior requests (Unit Framework
    /// v27.3 3.4). The control system influences arbitration only through
    /// CrowdControlHandler.State; no control-specific branches live here.
    /// </summary>
    public sealed class ActionArbiter
    {
        private readonly Unit _owner;

        public ActionArbiter(Unit owner) { _owner = owner; }

        public ArbitrationResult Evaluate(ActionRequest request)
        {
            if (request == null) return ArbitrationResult.Rejected;
            if (!HasCapability(request.Kind)) return ArbitrationResult.Rejected;
            if (IsActionBlockedByControl(request)) return ArbitrationResult.Rejected;
            if ((request.Kind == ActionKind.Move ||
                 request.Kind == ActionKind.Attack) &&
                _owner.AbilityHandler != null &&
                _owner.AbilityHandler.IsCastMovementLocked())
            {
                return ArbitrationResult.Rejected;
            }

            ReservationState reservation = _owner.ActionRuntimes.BuildReservation();

            switch (request.Kind)
            {
                case ActionKind.Move:
                    if (reservation.MoveReserved) return ArbitrationResult.Rejected;
                    break;
                case ActionKind.Attack:
                    if (reservation.AttackReserved) return ArbitrationResult.Rejected;
                    break;
                case ActionKind.Cast:
                    if (reservation.CastReserved) return ArbitrationResult.Rejected;
                    if (reservation.HighestReservedKind != ActionKind.None
                        && (int)request.Kind > (int)reservation.HighestReservedKind)
                        return ArbitrationResult.Interrupt;
                    break;
            }

            return ArbitrationResult.Accepted;
        }

        /// <summary>
        /// Fixed-phase check that interrupts current runtimes whose action is
        /// no longer allowed by the latest control state (Unit Framework
        /// v27.3 3.4 EvaluateCurrentRuntimes).
        /// </summary>
        public void EvaluateCurrentRuntimes()
        {
            if (_owner.ActionRuntimes == null) return;
            if (_owner.CrowdControl == null) return;
            CrowdControlStateView controlState =
                _owner.CrowdControl.State;

            if ((controlState.BlockedActions &
                 UnitActionBlockMask.VoluntaryMove) != 0)
            {
                _owner.ActionRuntimes.CancelByKind(
                    ActionKind.Move);
            }
            if ((controlState.BlockedActions &
                 UnitActionBlockMask.VoluntaryAttack) != 0)
            {
                _owner.ActionRuntimes.CancelByKind(
                    ActionKind.Attack);
            }
            if ((controlState.BlockedActions &
                 UnitActionBlockMask.AbilityCast) != 0)
            {
                _owner.ActionRuntimes.CancelByKind(
                    ActionKind.Cast);
            }
        }

        private bool HasCapability(ActionKind kind)
        {
            ref readonly CapabilityState cap = ref _owner.CapabilityState;
            return kind switch
            {
                ActionKind.Move => cap.CanMove,
                ActionKind.Attack => cap.CanAttack,
                ActionKind.Cast => cap.CanCast,
                _ => false,
            };
        }

        /// <summary>
        /// Map an ordinary request kind onto the aggregated control block mask
        /// (Unit Framework v27.3 3.4 step 4: directly read CrowdControlStateView).
        /// Control-driven Move requests carry ControlMove purpose; control-driven
        /// attacks are identified by the active behavior override.
        /// </summary>
        private bool IsActionBlockedByControl(
            ActionRequest request)
        {
            if (_owner.CrowdControl == null)
            {
                return false;
            }
            CrowdControlStateView controlState =
                _owner.CrowdControl.State;

            switch (request.Kind)
            {
                case ActionKind.Move:
                    bool isControlMove =
                        request is MoveActionRequest move &&
                        move.Purpose ==
                            MovePurpose.ControlMove;
                    return isControlMove
                        ? (controlState.BlockedActions &
                           UnitActionBlockMask.ControlMove) != 0
                        : (controlState.BlockedActions &
                           UnitActionBlockMask.VoluntaryMove) != 0;

                case ActionKind.Attack:
                    bool isControlAttack =
                        _owner.CrowdControl
                            .TryGetBehaviorOverride(
                                out CrowdControlBehaviorOverride behavior) &&
                        behavior.Kind ==
                            CrowdControlBehaviorKind.AttackTarget &&
                        request is AttackActionRequest attack &&
                        attack.TargetUnit ==
                            behavior.TargetUnitUid;
                    return isControlAttack
                        ? (controlState.BlockedActions &
                           UnitActionBlockMask.ControlAttack) != 0
                        : (controlState.BlockedActions &
                           UnitActionBlockMask.VoluntaryAttack) != 0;

                case ActionKind.Cast:
                    return (controlState.BlockedActions &
                           UnitActionBlockMask.AbilityCast) != 0;

                default:
                    return false;
            }
        }
    }
}
