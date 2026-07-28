namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Presentation v13.2 §3.1 — high-level action kind for animation state selection.
    /// Maps coarse Unit behavior state to animation categories.
    /// </summary>
    public enum ActionMainKind : byte
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Cast = 3,
        Control = 4,
        Dash = 5,
        Dead = 6,
    }

    /// <summary>
    /// Presentation v13.2 §3.1 — base action kind for lower-body animation layers.
    /// </summary>
    public enum ActionBaseKind : byte
    {
        Idle = 0,
        Move = 1,
        Dash = 2,
        ForcedMove = 3,
    }

    /// <summary>
    /// Presentation v13.2 §3.1 — read-only snapshot of a unit's action state,
    /// consumed by UnitAnimationDriver to select and drive animation clips.
    /// Does NOT contain Gameplay-authoritative data; it is a projection.
    /// </summary>
    public readonly struct UnitActionStateView
    {
        /// <summary>Primary action category (controls upper-body animation).</summary>
        public readonly ActionMainKind MainKind;

        /// <summary>Base action category (controls lower-body animation).</summary>
        public readonly ActionBaseKind BaseKind;

        /// <summary>Whether the unit is currently performing an action that prevents idle reset.</summary>
        public readonly bool IsActing;

        public UnitActionStateView(ActionMainKind mainKind, ActionBaseKind baseKind, bool isActing)
        {
            MainKind = mainKind;
            BaseKind = baseKind;
            IsActing = isActing;
        }

        public static readonly UnitActionStateView Idle = new UnitActionStateView(
            ActionMainKind.Idle, ActionBaseKind.Idle, false);

        public static readonly UnitActionStateView Dead = new UnitActionStateView(
            ActionMainKind.Dead, ActionBaseKind.Idle, false);
    }
}
