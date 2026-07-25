using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Cross-Tick attack state captured at Tick end for snapshot/rollback
    /// (Attack v6.2 section 2.3; Snapshot Appendix v7.2).
    /// </summary>
    public struct AttackSnapshot
    {
        /// <summary>Target identity, or default when idle.</summary>
        public UnitUid CurrentTargetUid;

        /// <summary>Logic Tick when the current attack cycle started.</summary>
        public int AttackStartLogicTick;

        /// <summary>Logic Tick when Impact (damage/projectile) fires.</summary>
        public int ImpactLogicTick;

        /// <summary>Logic Tick when the next attack may begin.</summary>
        public int NextAttackReadyLogicTick;

        /// <summary>True if Impact has already fired this cycle.</summary>
        public bool ImpactCommitted;

        /// <summary>
        /// Monotonically increasing attack animation sequence index
        /// (Attack v6.2 section 2.3, adjust #14).
        /// </summary>
        public byte AttackSequenceIndex;

        public static readonly AttackSnapshot Default = new AttackSnapshot
        {
            CurrentTargetUid = default,
            AttackStartLogicTick = 0,
            ImpactLogicTick = 0,
            NextAttackReadyLogicTick = 0,
            ImpactCommitted = false,
            AttackSequenceIndex = 0,
        };
    }

    /// <summary>
    /// Animation-facing snapshot of the current attack state for presentation.
    /// Computed each LateUpdate by reading deterministic AttackHandler state.
    /// </summary>
    public struct AttackAnimationSnapshot
    {
        public bool IsAttacking;
        public byte SequenceIndex;
        public bool ImpactCommitted;
        public float WindupProgress;
        public float RecoveryProgress;
    }

    /// <summary>
    /// Animation-facing snapshot of the current hit reaction state.
    /// </summary>
    public struct HitReactionAnimationInfo
    {
        public bool IsActive;
        public HitReactionKind ActiveReaction;
        public float RemainingRatio;
    }
}
