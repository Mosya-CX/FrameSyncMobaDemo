using FrameSyncMoba.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Static utility that reads a CastModelDef and produces a
    /// BakedPlayerAbilityInputProfile describing the physical input mode.
    /// 
    /// Mapping:
    ///   HoldRelease → PressFocusReleaseOrPrimaryCommit
    ///   Commit with aim → LocalAimPrimaryCommit
    ///   Commit without aim → PressCommit
    ///   Channel → PressFocusReleaseOrPrimaryCommit (channel behaves like hold)
    ///   ActiveSignal → PressCommit
    /// </summary>
    public static class AbilityInputProfileBaker
    {
        /// <summary>
        /// Derive the player input profile from a CastModelDef.
        /// Returns the default PressCommit profile if def is null.
        /// </summary>
        public static BakedPlayerAbilityInputProfile Bake(CastModelDef def)
        {
            if (def == null)
                return new BakedPlayerAbilityInputProfile(BakedPlayerAbilityInputMode.PressCommit);

            switch (def.Kind)
            {
                case CastModelKind.HoldRelease:
                    return new BakedPlayerAbilityInputProfile(
                        BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit);

                case CastModelKind.Channel:
                    // Channel abilities behave like hold-release: press starts channel,
                    // release or recast commits/ends channel.
                    return new BakedPlayerAbilityInputProfile(
                        BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit);

                case CastModelKind.Commit:
                case CastModelKind.ActiveSignal:
                default:
                    // For Commit/ActiveSignal, check if the ability requires aim
                    // This is determined by the AimKind in AbilityDef
                    // If no AimKind info, default to PressCommit
                    return new BakedPlayerAbilityInputProfile(
                        BakedPlayerAbilityInputMode.PressCommit);
            }
        }

        /// <summary>
        /// Derive with explicit AimKind override for abilities that require aiming.
        /// If AimKind is anything other than None or Self, the ability needs
        /// local aim mode.
        /// </summary>
        public static BakedPlayerAbilityInputProfile Bake(CastModelDef def, AimKind aimKind)
        {
            if (def == null)
                return new BakedPlayerAbilityInputProfile(BakedPlayerAbilityInputMode.PressCommit);

            // If the ability requires targeting (point, unit, direction),
            // use local-aim mode regardless of cast model kind (unless hold-release)
            if (aimKind != AimKind.None && aimKind != AimKind.Self)
            {
                if (def.Kind == CastModelKind.HoldRelease)
                {
                    // Hold-release with aim: press→Focus, then aim, release→Commit with aim
                    return new BakedPlayerAbilityInputProfile(
                        BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit);
                }

                // Standard skillshot: press→aim, primary-click→Commit
                return new BakedPlayerAbilityInputProfile(
                    BakedPlayerAbilityInputMode.LocalAimPrimaryCommit);
            }

            return Bake(def);
        }
    }
}
