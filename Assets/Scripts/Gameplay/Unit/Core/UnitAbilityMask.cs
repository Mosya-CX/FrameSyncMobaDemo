namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §1.7 — derived from HandlerLoadout at spawn time.
    /// Expresses which active-behavior capabilities the unit possesses, based
    /// solely on whether the corresponding Handler is configured.
    /// 
    /// Does NOT express: Buff, Control, Equipment, Targetable status,
    /// PhysicsEntity2D presence, or Locomotion.
    /// </summary>
    public readonly struct UnitAbilityMask
    {
        /// <summary>Whether the unit has a MovementHandler and may initiate active movement.</summary>
        public readonly bool HasMovement;

        /// <summary>Whether the unit has an AttackHandler and may initiate normal attacks.</summary>
        public readonly bool HasAttack;

        /// <summary>Whether the unit has an AbilityHandler and may initiate ability casting.</summary>
        public readonly bool HasAbility;

        public UnitAbilityMask(bool hasMovement, bool hasAttack, bool hasAbility)
        {
            HasMovement = hasMovement;
            HasAttack = hasAttack;
            HasAbility = hasAbility;
        }

        /// <summary>
        /// Derives the ability mask from the Handler references already assigned
        /// to a Unit. Called during InitializeForNewRuntime after Handler binding.
        /// </summary>
        public static UnitAbilityMask BuildFromUnit(Unit unit)
        {
            return new UnitAbilityMask(
                hasMovement: unit.MovementHandler != null,
                hasAttack: unit.AttackHandler != null,
                hasAbility: unit.AbilityHandler != null);
        }

        public static readonly UnitAbilityMask None = default;
    }
}
