namespace FrameSyncMoba.Unit
{
    public enum ArbitrationResult : byte
    {
        Accepted,
        Rejected,
        Interrupt,
    }

    public sealed class ActionArbiter
    {
        private readonly Unit _owner;

        public ActionArbiter(Unit owner) { _owner = owner; }

        public ArbitrationResult Evaluate(ActionRequest request)
        {
            if (request == null) return ArbitrationResult.Rejected;
            if (!HasCapability(request.Kind)) return ArbitrationResult.Rejected;

            if (HasBehaviorOverride(out ActionKind overrideKind))
            {
                if (request.Kind != overrideKind && overrideKind != ActionKind.None)
                    return ArbitrationResult.Rejected;
                return ArbitrationResult.Accepted;
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

        private bool HasBehaviorOverride(out ActionKind kind)
        {
            kind = ActionKind.None;
            if (_owner.CrowdControl == null) return false;
            var cc = _owner.CrowdControl.ActiveConstraint;
            if (!cc.IsActive) return false;
            switch (cc.Type)
            {
                case CrowdControlType.Stun:
                case CrowdControlType.Suppression:
                    kind = ActionKind.None;
                    return true;
                case CrowdControlType.Disarm:
                    kind = ActionKind.Move;
                    return true;
                case CrowdControlType.Silence:
                    return false;
                default:
                    return false;
            }
        }
    }
}
