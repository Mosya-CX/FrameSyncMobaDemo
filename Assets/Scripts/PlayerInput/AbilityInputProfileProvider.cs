using System;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Runtime implementation of IPlayerAbilityInputProfileProvider.
    /// Holds a pre-baked array of BakedPlayerAbilityInputProfile indexed by slot,
    /// plus an optional AimKind array for aim-aware Bake.
    /// 
    /// This is created once during bootstrap from AbilityDefinitionRegistry data
    /// and does not change during a match.
    /// </summary>
    public sealed class AbilityInputProfileProvider :
        IPlayerAbilityInputProfileProvider,
        IPlayerAbilityAimProfileProvider
    {
        private const int MaxSlots = 4;

        private readonly BakedPlayerAbilityInputProfile[] _profiles;
        private readonly AimKind[] _aimKinds;
        private readonly Unity.Mathematics.FixedPoint.fp[]
            _castRanges;

        public AbilityInputProfileProvider(
            BakedPlayerAbilityInputProfile[] profiles,
            AimKind[] aimKinds = null,
            Unity.Mathematics.FixedPoint.fp[] castRanges = null)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            if (_profiles.Length > MaxSlots)
            {
                throw new ArgumentException(
                    $"Profile array length {_profiles.Length} exceeds max slots {MaxSlots}.",
                    nameof(profiles));
            }

            _aimKinds = aimKinds ?? new AimKind[MaxSlots];
            _castRanges = castRanges ??
                new Unity.Mathematics.FixedPoint.fp[MaxSlots];
            if (_aimKinds.Length < _profiles.Length ||
                _castRanges.Length < _profiles.Length)
            {
                throw new ArgumentException(
                    "Aim and range arrays must cover every profile slot.");
            }
        }

        /// <summary>
        /// Create a provider by baking from an AbilityHandler's definitions.
        /// Reads each slot's AbilityDef.CastModel and bakes the profile.
        /// </summary>
        public static AbilityInputProfileProvider CreateFromAbilityHandler(
            AbilityHandler handler)
        {
            if (handler == null)
                return CreateEmpty();

            var profiles = new BakedPlayerAbilityInputProfile[MaxSlots];
            var aimKinds = new AimKind[MaxSlots];
            var castRanges =
                new Unity.Mathematics.FixedPoint.fp[MaxSlots];

            for (byte slot = 0; slot < MaxSlots; slot++)
            {
                AbilityDef def = handler.GetAbilityDef(slot);
                if (def != null && def.IsValid && def.CastModel != null)
                {
                    AimKind aimKind = def.AimKind;
                    aimKinds[slot] = aimKind;
                    castRanges[slot] = def.CastRange;
                    profiles[slot] = AbilityInputProfileBaker.Bake(def.CastModel, aimKind);
                }
                else
                {
                    profiles[slot] = new BakedPlayerAbilityInputProfile(
                        BakedPlayerAbilityInputMode.PressCommit);
                    aimKinds[slot] = AimKind.None;
                }
            }

            return new AbilityInputProfileProvider(
                profiles,
                aimKinds,
                castRanges);
        }

        /// <summary>
        /// Create an empty provider that returns PressCommit for all slots.
        /// </summary>
        public static AbilityInputProfileProvider CreateEmpty()
        {
            var profiles = new BakedPlayerAbilityInputProfile[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                profiles[i] = new BakedPlayerAbilityInputProfile(
                    BakedPlayerAbilityInputMode.PressCommit);
            }

            var aimKinds = new AimKind[MaxSlots];
            return new AbilityInputProfileProvider(profiles, aimKinds);
        }

        public bool TryGetProfile(byte slot, out BakedPlayerAbilityInputProfile profile)
        {
            if (slot >= _profiles.Length)
            {
                profile = default;
                return false;
            }

            profile = _profiles[slot];
            return true;
        }

        public bool TryGetAimKind(byte slot, out AimKind aimKind)
        {
            if (_aimKinds == null || slot >= _aimKinds.Length)
            {
                aimKind = AimKind.None;
                return false;
            }

            aimKind = _aimKinds[slot];
            return aimKind != AimKind.None;
        }

        public bool TryGetAimConfiguration(
            byte slot,
            out AimKind aimKind,
            out Unity.Mathematics.FixedPoint.fp castRange)
        {
            if (slot >= _profiles.Length)
            {
                aimKind = AimKind.None;
                castRange =
                    Unity.Mathematics.FixedPoint.fp.zero;
                return false;
            }
            aimKind = _aimKinds[slot];
            castRange = _castRanges[slot];
            return aimKind != AimKind.None;
        }
    }
}
