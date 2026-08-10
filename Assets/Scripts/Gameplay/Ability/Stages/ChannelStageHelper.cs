using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime helper for channel ability stages.
    /// Provides deterministic per-tick channel evaluation with CC interruption
    /// and progress tracking.
    /// (Ability Design v15.2 sections 5.3)
    /// </summary>
    public static class ChannelStageHelper
    {
        /// <summary>
        /// Evaluate one tick of channeling.
        /// Returns (isActive, progress) where progress is 0.0 to 1.0.
        /// Channel breaks if the caster is stunned, silenced, rooted, or suppressed.
        /// </summary>
        public static (bool isActive, fp progress) EvaluateChannel(
            Unit caster,
            int currentTick,
            int startTick,
            int durationTicks)
        {
            if (caster == null || durationTicks <= 0)
                return (false, fp.zero);

            // Interruption check: CC that prevents ability usage breaks channel
            if (caster.CrowdControl != null &&
                caster.CrowdControl.IsBlocked(
                    UnitActionBlockMask.AbilityCast))
                return (false, fp.zero);

            // Hit reaction that interrupts abilities breaks channel
            if (caster.HitReaction.InterruptsAbility)
                return (false, fp.zero);

            int elapsed = currentTick - startTick;
            if (elapsed >= durationTicks)
                return (false, fp.one);

            fp progress = (fp)elapsed / (fp)durationTicks;
            return (true, progress);
        }

        /// <summary>
        /// Check whether a channel should be interrupted due to external conditions.
        /// </summary>
        public static bool ShouldInterrupt(Unit caster)
        {
            if (caster == null) return true;
            if (caster.CrowdControl != null &&
                caster.CrowdControl.IsBlocked(
                    UnitActionBlockMask.AbilityCast))
                return true;
            if (caster.HitReaction.InterruptsAbility)
                return true;
            return false;
        }
    }
}
