namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Team filtering rule for RangeQueryService (Physics v13.1 section 9.3).
    /// </summary>
    public enum TeamQueryRule
    {
        /// <summary>No team restriction — all units pass this filter.</summary>
        Any,

        /// <summary>Only enemy units (different team, not Neutral).</summary>
        EnemyOnly,

        /// <summary>Only allied units (same team, excluding self).</summary>
        AllyOnly,

        /// <summary>Allied units plus self.</summary>
        AllyOrSelf,

        /// <summary>Only the querying unit itself.</summary>
        SelfOnly,
    }
}
