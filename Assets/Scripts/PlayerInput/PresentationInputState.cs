namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Presentation-only input state shared with the UI bridge. Written by
    /// PlayerInputController from Gameplay-map actions (e.g. hold C to expand
    /// stats); it never enters the local event buffer, a GameplayCommand or
    /// any deterministic state.
    /// </summary>
    public static class PresentationInputState
    {
        public static bool ExpandStatsHeld { get; set; }
    }
}
