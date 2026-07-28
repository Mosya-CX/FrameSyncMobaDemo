using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §3.2 — the unit's current long-term goal.
    /// Intent persists across ticks until the goal is achieved, cleared, or replaced.
    /// It is NOT the current action, NOT the current Runtime, and NOT the current Handler state.
    /// </summary>
    public enum IntentKind : byte
    {
        /// <summary>No active intent; unit idles.</summary>
        None,

        /// <summary>Attack a specific target unit.</summary>
        AttackTarget,

        /// <summary>Move to a world position.</summary>
        MoveToPosition,

        /// <summary>Cast a specific ability (may include target).</summary>
        CastAbility,

        /// <summary>Minion lane advance behavior.</summary>
        LaneAdvance,

        /// <summary>Monster return-to-camp behavior.</summary>
        ReturnToCamp,
    }

    /// <summary>
    /// Unit Framework v27.3 §3.2 — the unit's current long-term goal.
    /// Does NOT store: whether the unit can currently attack/cast, resource
    /// reservations, remaining windup ticks, movement task handles, or
    /// crowd-control override state.
    /// </summary>
    public struct UnitIntent
    {
        /// <summary>The kind of long-term goal.</summary>
        public IntentKind Kind;

        /// <summary>
        /// When Kind is AttackTarget: the target unit's UID.
        /// When Kind is CastAbility with a unit target: the target unit.
        /// </summary>
        public UnitUid TargetUnit;

        /// <summary>
        /// When Kind is MoveToPosition or CastAbility with a ground target:
        /// the world-space target position.
        /// </summary>
        public fp2 TargetPosition;

        /// <summary>When Kind is CastAbility: the ability definition ID.</summary>
        public int AbilityId;

        /// <summary>When Kind is CastAbility: the existing Ability signal verb.</summary>
        public AbilitySignalVerb AbilityVerb;

        /// <summary>When Kind is CastAbility: the existing canonical Ability aim.</summary>
        public AimSnapshot AbilityAim;

        /// <summary>
        /// When true, the planner may generate chase MoveActionRequests
        /// if the unit is out of range for Attack/Cast.
        /// </summary>
        public bool AllowChase;

        /// <summary>
        /// When true, the planner may replan (switch to a different action)
        /// if conditions change. When false, the current action must complete
        /// or fail before the planner considers alternatives.
        /// </summary>
        public bool AllowReplan;

        public static readonly UnitIntent None = default;

        public bool IsActive => Kind != IntentKind.None;

        public void Clear()
        {
            this = None;
        }
    }
}
