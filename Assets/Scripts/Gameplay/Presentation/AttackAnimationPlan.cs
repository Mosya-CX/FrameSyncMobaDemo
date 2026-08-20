using System;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

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
        public DurationAuthoring StartDuration;
        public DurationAuthoring WindupDuration;
        public DurationAuthoring ImpactDuration;
        public DurationAuthoring RecoveryDuration;

        [HideInInspector]
        public int StartDurationTicks;
        [HideInInspector]
        public int WindupDurationTicks;
        [HideInInspector]
        public int ImpactDurationTicks;
        [HideInInspector]
        public int RecoveryDurationTicks;

        /// <summary>Total ticks for the full attack animation.</summary>
        public int TotalTicks => StartDurationTicks + WindupDurationTicks + ImpactDurationTicks + RecoveryDurationTicks;

        public AttackAnimationPlan Bake(int tickRate)
        {
            AttackAnimationPlan baked = this;
            baked.StartDurationTicks = BakeDuration(
                StartDuration,
                StartDurationTicks,
                tickRate);
            baked.WindupDurationTicks = BakeDuration(
                WindupDuration,
                WindupDurationTicks,
                tickRate);
            baked.ImpactDurationTicks = BakeDuration(
                ImpactDuration,
                ImpactDurationTicks,
                tickRate);
            baked.RecoveryDurationTicks = BakeDuration(
                RecoveryDuration,
                RecoveryDurationTicks,
                tickRate);
            return baked;
        }

        private static int BakeDuration(
            in DurationAuthoring authoring,
            int legacyTicks,
            int tickRate)
        {
            return authoring.IsAuthored
                ? authoring.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        legacyTicks,
                        tickRate);
        }

        public static readonly AttackAnimationPlan Default = new AttackAnimationPlan
        {
            StartDuration = DurationAuthoring.FromLegacyTicks(2),
            WindupDuration = DurationAuthoring.FromLegacyTicks(5),
            ImpactDuration = DurationAuthoring.FromLegacyTicks(2),
            RecoveryDuration = DurationAuthoring.FromLegacyTicks(8),
            StartDurationTicks = 2,
            WindupDurationTicks = 5,
            ImpactDurationTicks = 2,
            RecoveryDurationTicks = 8,
        };
    }
}
