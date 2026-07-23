namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global ordering header for Combat requests within a single LogicTick
    /// (Combat v13.2 section 2.2).
    ///
    /// All active requests (Shield/Damage/Heal) share a unified
    /// SequenceInTick: the order in which requests were accepted by CombatSystem.
    /// This guarantees deterministic execution regardless of submission order
    /// from different Gameplay modules.
    /// </summary>
    public struct CombatRequestHeader
    {
        /// <summary>
        /// Stable ordering identity within the current LogicTick.
        /// Assigned by CombatSystem when the request enters an active queue.
        /// Smaller values execute first.
        /// </summary>
        public ushort SequenceInTick;

        /// <summary>The LogicTick when this request was created.</summary>
        public int SourceLogicTick;

        public static readonly CombatRequestHeader None = default;
    }
}
