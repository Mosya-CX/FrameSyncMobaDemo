using System;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Presentation v13.2 §3.4 — timing parameters for an attack animation clip.
    /// Used by UnitAnimationDriver to synchronize attack animations with
    /// deterministic Gameplay attack phases.
    /// </summary>
    [Serializable]
    public struct AttackAnimationPlan
    {
        /// <summary>Duration of the attack startup (before windup) in ticks.</summary>
        public int StartDurationTicks;

        /// <summary>Duration of the windup phase in ticks.</summary>
        public int WindupDurationTicks;

        /// <summary>Duration of the impact phase in ticks.</summary>
        public int ImpactDurationTicks;

        /// <summary>Duration of the recovery phase in ticks.</summary>
        public int RecoveryDurationTicks;

        /// <summary>Total ticks for the full attack animation.</summary>
        public int TotalTicks => StartDurationTicks + WindupDurationTicks + ImpactDurationTicks + RecoveryDurationTicks;

        public static readonly AttackAnimationPlan Default = new AttackAnimationPlan
        {
            StartDurationTicks = 2,
            WindupDurationTicks = 5,
            ImpactDurationTicks = 2,
            RecoveryDurationTicks = 8,
        };
    }
}
