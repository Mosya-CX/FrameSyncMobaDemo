namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Query identity metadata attached to a PhysicsEntity2D (Physics v13.1 section 2.3).
    /// Set at registration time and read-only thereafter. Used for grid candidate
    /// deduplication, stable sorting, collision PairKey, and logging.
    /// </summary>
    public readonly struct PhysicsEntityQueryInfo
    {
        public readonly RuntimeUidQueryValue UidSnapshot;
        public readonly PhysicsEntityKind Kind;
        public readonly byte TeamSnapshot;
        public readonly object Owner;

        public PhysicsEntityQueryInfo(
            RuntimeUidQueryValue uidSnapshot,
            PhysicsEntityKind kind,
            byte teamSnapshot,
            object owner)
        {
            UidSnapshot = uidSnapshot;
            Kind = kind;
            TeamSnapshot = teamSnapshot;
            Owner = owner;
        }

        /// <summary>
        /// Whether this query info has been initialized (non-default).
        /// </summary>
        public bool IsSet => Owner != null || UidSnapshot.SpawnLogicTick != 0;
    }
}