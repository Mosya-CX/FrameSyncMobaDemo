namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §3.2 — tracks which action resources are currently
    /// occupied by active ActionRuntimes. Used by the ActionArbiter to determine
    /// whether a new action can start or an existing action must be interrupted.
    /// </summary>
    public struct ReservationState
    {
        /// <summary>Whether any Move-occupying action is active.</summary>
        public bool MoveReserved;

        /// <summary>Whether any Attack-occupying action is active.</summary>
        public bool AttackReserved;

        /// <summary>Whether any Cast-occupying action is active.</summary>
        public bool CastReserved;

        /// <summary>
        /// The kind of the highest-priority action currently occupying resources,
        /// or None if nothing is reserved.
        /// </summary>
        public ActionKind HighestReservedKind;

        public static readonly ReservationState Empty = default;

        public bool IsReserved(ActionKind kind)
        {
            return kind switch
            {
                ActionKind.Move => MoveReserved,
                ActionKind.Attack => AttackReserved,
                ActionKind.Cast => CastReserved,
                _ => false,
            };
        }
    }
}
