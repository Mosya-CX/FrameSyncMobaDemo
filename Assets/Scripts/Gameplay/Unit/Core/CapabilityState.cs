namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Coarse-grained capability state frozen by Unit v27.3 section 1.9.
    /// Only answers whether the Unit may initiate basic active behaviors.
    /// Does not cover forced displacement (knockback, pull, knockup).
    /// </summary>
    public struct CapabilityState
    {
        /// <summary>Whether the Unit may initiate active movement.</summary>
        public bool CanMove;

        /// <summary>Whether the Unit may initiate a normal attack.</summary>
        public bool CanAttack;

        /// <summary>Whether the Unit may initiate ability casting.</summary>
        public bool CanCast;

        /// <summary>Whether the Unit may initiate active turning.</summary>
        public bool CanTurn;

        /// <summary>Whether the Unit can be targeted or hit.</summary>
        public bool IsTargetable;

        /// <summary>
        /// Alive default: all capabilities enabled (Unit v27.3 section 1.9).
        /// Handler-derived AbilityMask refinement is a future concern; this
        /// slice sets all-true because no Handler system exists yet.
        /// </summary>
        public static CapabilityState CreateAliveDefault()
        {
            return new CapabilityState
            {
                CanMove = true,
                CanAttack = true,
                CanCast = true,
                CanTurn = true,
                IsTargetable = true,
            };
        }

        /// <summary>
        /// Disables all active actions and targeting (Unit v27.3 section 1.9).
        /// Called by UnitWorld when formal death is confirmed.
        /// </summary>
        public void DisableAllActions()
        {
            CanMove = false;
            CanAttack = false;
            CanCast = false;
            CanTurn = false;
            IsTargetable = false;
        }

        /// <summary>
        /// Resets to Alive default (Unit v27.3 section 1.9).
        /// The full design uses ResetAliveDefault(AbilityMask); this slice
        /// uses the all-true default because no Handler/AbilityMask exists yet.
        /// </summary>
        public void ResetAliveDefault()
        {
            CanMove = true;
            CanAttack = true;
            CanCast = true;
            CanTurn = true;
            IsTargetable = true;
        }
    }
}