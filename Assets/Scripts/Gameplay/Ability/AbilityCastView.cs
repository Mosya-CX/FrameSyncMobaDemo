using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Presentation v13.2 §3.1 — read-only view of the current ability cast state
    /// exposed by AbilityHandler for the UnitAnimationDriver.
    /// 
    /// The animation driver reads this instead of reaching into AbilityHandler
    /// internals. This is a projection, not Gameplay authority.
    /// </summary>
    public readonly struct AbilityCastView
    {
        /// <summary>Whether a cast is currently active.</summary>
        public readonly bool IsCasting;

        /// <summary>The ability definition ID being cast.</summary>
        public readonly int AbilityId;

        /// <summary>The current stage index (0-based).</summary>
        public readonly int StageIndex;

        /// <summary>Normalized progress through the current stage (0..1).</summary>
        public readonly fp StageProgress;

        /// <summary>
        /// The target position for ground/vector-targeted abilities.
        /// Default if unit-targeted.
        /// </summary>
        public readonly fp2 TargetPosition;

        /// <summary>
        /// The target unit UID for unit-targeted abilities.
        /// Invalid if ground-targeted.
        /// </summary>
        public readonly UnitUid TargetUnit;

        public AbilityCastView(
            bool isCasting,
            int abilityId,
            int stageIndex,
            fp stageProgress,
            fp2 targetPosition,
            UnitUid targetUnit)
        {
            IsCasting = isCasting;
            AbilityId = abilityId;
            StageIndex = stageIndex;
            StageProgress = stageProgress;
            TargetPosition = targetPosition;
            TargetUnit = targetUnit;
        }

        public static readonly AbilityCastView None = default;
    }
}
