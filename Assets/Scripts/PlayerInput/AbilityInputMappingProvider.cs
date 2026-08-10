using System;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Runtime provider of per-slot InputMappingTemplate plus aim metadata.
    /// Created once at bootstrap from AbilityHandler definitions; templates
    /// are offline-validated authored data (no Bake).
    /// </summary>
    public sealed class AbilityInputMappingProvider :
        IPlayerAbilityInputProfileProvider,
        IPlayerAbilityAimProfileProvider
    {
        private const int MaxSlots = 4;

        private readonly InputMappingTemplate[] _templates;
        private readonly AimKind[] _aimKinds;
        private readonly fp[] _castRanges;

        public AbilityInputMappingProvider(
            InputMappingTemplate[] templates,
            AimKind[] aimKinds = null,
            fp[] castRanges = null)
        {
            _templates = templates ??
                throw new ArgumentNullException(nameof(templates));
            if (_templates.Length > MaxSlots)
            {
                throw new ArgumentException(
                    $"Template array length {_templates.Length} exceeds max slots {MaxSlots}.",
                    nameof(templates));
            }
            _aimKinds = aimKinds ?? new AimKind[MaxSlots];
            _castRanges = castRanges ??
                new fp[MaxSlots];
            if (_aimKinds.Length < _templates.Length ||
                _castRanges.Length < _templates.Length)
            {
                throw new ArgumentException(
                    "Aim and range arrays must cover every template slot.");
            }
        }

        /// <summary>
        /// Build the provider from an AbilityHandler's definitions using the
        /// default templates. Slots without a valid definition use the
        /// PressCommit default.
        /// </summary>
        public static AbilityInputMappingProvider
            CreateFromAbilityHandler(
                AbilityHandler handler)
        {
            var templates =
                new InputMappingTemplate[MaxSlots];
            var aimKinds = new AimKind[MaxSlots];
            var castRanges = new fp[MaxSlots];

            for (byte slot = 0;
                 slot < MaxSlots;
                 slot++)
            {
                AbilityDef def =
                    handler?.GetAbilityDef(slot);
                if (def != null &&
                    def.IsValid &&
                    def.CastModel != null)
                {
                    AimKind aimKind = def.AimKind;
                    aimKinds[slot] = aimKind;
                    castRanges[slot] = def.CastRange;
                    templates[slot] =
                        AbilityInputMapping.BuildDefault(
                            def.CastModel,
                            aimKind);
                }
                else
                {
                    templates[slot] =
                        AbilityInputMapping
                            .DefaultPressCommit;
                    aimKinds[slot] = AimKind.None;
                }
            }

            return new AbilityInputMappingProvider(
                templates,
                aimKinds,
                castRanges);
        }

        public static AbilityInputMappingProvider
            CreateEmpty()
        {
            var templates =
                new InputMappingTemplate[MaxSlots];
            for (int i = 0;
                 i < MaxSlots;
                 i++)
                templates[i] =
                    AbilityInputMapping
                        .DefaultPressCommit;
            return new AbilityInputMappingProvider(
                templates,
                new AimKind[MaxSlots]);
        }

        public bool TryGetTemplate(
            byte slot,
            out InputMappingTemplate template)
        {
            if (slot >= _templates.Length)
            {
                template = null;
                return false;
            }
            template = _templates[slot];
            return true;
        }

        public bool TryGetAimKind(
            byte slot,
            out AimKind aimKind)
        {
            if (_aimKinds == null ||
                slot >= _aimKinds.Length)
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
            out fp castRange)
        {
            if (slot >= _templates.Length)
            {
                aimKind = AimKind.None;
                castRange = fp.zero;
                return false;
            }
            aimKind = _aimKinds[slot];
            castRange = _castRanges[slot];
            return aimKind != AimKind.None;
        }
    }
}
