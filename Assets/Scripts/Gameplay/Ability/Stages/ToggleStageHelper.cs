using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime helper for toggle ability stages.
    /// Provides deterministic per-tick toggle evaluation with resource drain
    /// and auto-off logic.
    /// (Ability Design v15.2 sections 5.4)
    /// </summary>
    public static class ToggleStageHelper
    {
        /// <summary>
        /// Evaluate one tick of a toggle stage.
        /// Returns (canContinue, resourceDrained).
        /// 
        /// When the caster toggles off or runs out of resource, canContinue becomes false.
        /// The caller should end the stage when canContinue is false.
        /// </summary>
        public static (bool canContinue, fp resourceDrained) EvaluateToggle(
            Unit caster,
            bool isToggledOn,
            fp resourcePerTick,
            ref fp currentResource)
        {
            if (caster == null)
                return (false, fp.zero);

            // Toggle off: stop draining, allow re-toggle
            if (!isToggledOn)
                return (false, fp.zero);

            // Check if caster can continue (CC restrictions)
            if (caster.CrowdControl != null &&
                caster.CrowdControl.IsBlocked(
                    UnitActionBlockMask.AbilityCast))
                return (false, fp.zero);

            if (caster.HitReaction.InterruptsAbility)
                return (false, fp.zero);

            // Drain resource
            if (resourcePerTick <= fp.zero)
                return (true, fp.zero);

            fp drained = resourcePerTick;
            if (currentResource < drained)
                drained = currentResource;

            currentResource -= drained;

            // Auto-off when resource depleted
            if (currentResource <= fp.zero)
            {
                currentResource = fp.zero;
                return (false, drained);
            }

            return (true, drained);
        }

        /// <summary>
        /// Check whether a toggle should be force-disabled due to external conditions.
        /// </summary>
        public static bool ShouldForceDisable(Unit caster)
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
