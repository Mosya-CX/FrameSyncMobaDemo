namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Controls what happens when an existing Buff receives a new Apply
    /// (BuffSystem v14.2 section 1.6).
    /// </summary>
    public enum BuffStackRule
    {
        /// <summary>Refresh the remaining duration to the full configured value; stack count unchanged.</summary>
        RefreshDuration,

        /// <summary>Add one additional stack (up to MaxStacks); duration handled independently.</summary>
        Independent,

        /// <summary>All stacks share a single duration; new application refreshes that duration.</summary>
        Dependent,
    }
}
