namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Reason for a non-death Unit removal via UnitWorld.DespawnUnit.
    /// Design: Unit Framework v27.3 §7.12, §9.6.1
    /// </summary>
    public enum UnitDespawnReason : byte
    {
        /// <summary>Summon duration expired naturally.</summary>
        SummonExpired = 0,

        /// <summary>Owner (hero, tower) was removed or the link was severed.</summary>
        OwnerRemoved = 1,

        /// <summary>Scripted map cleanup, phase transition, or designer-driven removal.</summary>
        ScriptedCleanup = 2,

        /// <summary>Match-cleanup, room reset, or end-of-game removal.</summary>
        MatchCleanup = 3,
    }

    /// <summary>
    /// How the Unit object should be disposed after a non-death despawn.
    /// Design: Unit Framework v27.3 §7.12
    /// </summary>
    public enum UnitDespawnMode : byte
    {
        /// <summary>Return the GameObject to its object pool for reuse.</summary>
        Pool = 0,

        /// <summary>Destroy the GameObject immediately.</summary>
        Destroy = 1,
    }

    /// <summary>
    /// A request to remove a Unit from the simulation without triggering
    /// death events, death rewards, or kill statistics.
    ///
    /// Used for summon expiration, owner removal, scripted cleanup,
    /// and match cleanup. Must NOT be used for rollback restore —
    /// that uses RemoveUnitForRollbackRestore instead.
    ///
    /// Design: Unit Framework v27.3 §7.12, §9.6.1
    /// </summary>
    public readonly struct UnitDespawnRequest
    {
        /// <summary>The unit to remove from simulation.</summary>
        public readonly UnitUid UnitUid;

        /// <summary>Why this unit is being despawned.</summary>
        public readonly UnitDespawnReason Reason;

        /// <summary>How to dispose the GameObject (pool or destroy).</summary>
        public readonly UnitDespawnMode Mode;

        public UnitDespawnRequest(
            UnitUid unitUid,
            UnitDespawnReason reason,
            UnitDespawnMode mode)
        {
            UnitUid = unitUid;
            Reason = reason;
            Mode = mode;
        }
    }
}
