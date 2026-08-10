using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Physical input events that can be translated by a skill mapping template
    /// (design v1.1 4.2).
    /// </summary>
    public enum InputTrigger : byte
    {
        AbilityKeyPressed = 0,
        AbilityKeyReleased = 1,
        PrimaryClick = 2,
        SecondaryClick = 3,
        Cancel = 4,
    }

    /// <summary>
    /// What a physical event translates to. Only Focus / Commit / Cancel are
    /// real AbilitySignal verbs; LocalAim* are presentation-only; None means
    /// "no action" (the template does not specify this event).
    /// </summary>
    public enum InputTranslation : byte
    {
        None = 0,
        LocalAimOnly = 1,
        CancelLocalAim = 2,
        Focus = 3,
        Commit = 4,
        Cancel = 5,
    }

    public readonly struct InputBinding
    {
        public readonly InputTrigger Trigger;
        public readonly InputTranslation Translation;
        public readonly bool CaptureAim;

        public InputBinding(
            InputTrigger trigger,
            InputTranslation translation,
            bool captureAim = false)
        {
            Trigger = trigger;
            Translation = translation;
            CaptureAim = captureAim;
        }
    }

    /// <summary>
    /// Per-skill authored mapping template. Events absent from the template
    /// mean "no action". Lookup is an O(1) fixed array; never mutated at
    /// runtime. Validated offline (editor) before use; no Bake.
    /// </summary>
    public sealed class InputMappingTemplate
    {
        private const int TriggerCount = 5;

        private readonly InputBinding?[] _byTrigger =
            new InputBinding?[TriggerCount];
        private readonly InputBinding[] _bindings;

        public IReadOnlyList<InputBinding> Bindings => _bindings;

        public InputMappingTemplate(InputBinding[] bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));
            var list = new List<InputBinding>(bindings.Length);
            for (int i = 0; i < bindings.Length; i++)
            {
                InputBinding binding = bindings[i];
                int index = (int)binding.Trigger;
                if (index < 0 || index >= TriggerCount)
                    throw new ArgumentOutOfRangeException(
                        nameof(binding),
                        "InputBinding Trigger is out of range.");
                if (_byTrigger[index].HasValue)
                    throw new ArgumentException(
                        $"Duplicate InputTrigger '{binding.Trigger}' in mapping template.",
                        nameof(bindings));
                _byTrigger[index] = binding;
                list.Add(binding);
            }
            _bindings = list.ToArray();
        }

        public bool TryGet(
            InputTrigger trigger,
            out InputBinding binding)
        {
            int index = (int)trigger;
            if (index < 0 ||
                index >= _byTrigger.Length)
            {
                binding = default;
                return false;
            }
            InputBinding? candidate =
                _byTrigger[index];
            if (candidate.HasValue)
            {
                binding = candidate.Value;
                return true;
            }
            binding = default;
            return false;
        }
    }

    /// <summary>
    /// Offline default-template generation and legality validation for the
    /// composable input translation layer. No Bake: templates are authored
    /// data, defaulted from CastModelDef, and validated in the editor.
    /// </summary>
    public static class AbilityInputMapping
    {
        public static InputMappingTemplate DefaultPressCommit { get; } =
            new InputMappingTemplate(new[]
            {
                new InputBinding(
                    InputTrigger.AbilityKeyPressed,
                    InputTranslation.Commit),
            });

        public static InputMappingTemplate BuildDefault(
            CastModelDef def,
            AimKind aimKind)
        {
            if (def == null)
                return DefaultPressCommit;

            switch (def.Kind)
            {
                case CastModelKind.HoldRelease:
                case CastModelKind.Channel:
                    return BuildHoldReleaseDefault();
                case CastModelKind.Commit:
                case CastModelKind.ActiveSignal:
                default:
                    if (aimKind != AimKind.None &&
                        aimKind != AimKind.Self)
                        return BuildLocalAimDefault();
                    return DefaultPressCommit;
            }
        }

        /// <summary>
        /// 本版本默认：按下 Focus、左键 Commit、松键 None（不 Commit/不
        /// Cancel）、右键 None（继续 Move / Attack）、Escape None。
        /// </summary>
        public static InputMappingTemplate BuildHoldReleaseDefault()
        {
            return new InputMappingTemplate(new[]
            {
                new InputBinding(
                    InputTrigger.AbilityKeyPressed,
                    InputTranslation.Focus),
                new InputBinding(
                    InputTrigger.PrimaryClick,
                    InputTranslation.Commit,
                    captureAim: true),
                new InputBinding(
                    InputTrigger.AbilityKeyReleased,
                    InputTranslation.None),
                new InputBinding(
                    InputTrigger.SecondaryClick,
                    InputTranslation.None),
                new InputBinding(
                    InputTrigger.Cancel,
                    InputTranslation.None),
            });
        }

        /// <summary>
        /// 本地瞄准：按下只开瞄准圈，左键 Commit，右键/Escape 只关瞄准圈。
        /// </summary>
        public static InputMappingTemplate BuildLocalAimDefault()
        {
            return new InputMappingTemplate(new[]
            {
                new InputBinding(
                    InputTrigger.AbilityKeyPressed,
                    InputTranslation.LocalAimOnly),
                new InputBinding(
                    InputTrigger.PrimaryClick,
                    InputTranslation.Commit,
                    captureAim: true),
                new InputBinding(
                    InputTrigger.SecondaryClick,
                    InputTranslation.CancelLocalAim),
                new InputBinding(
                    InputTrigger.Cancel,
                    InputTranslation.CancelLocalAim),
            });
        }

        /// <summary>
        /// Offline legality validation (design v1.1 18.2). Returns a list of
        /// problems; empty means the template is legal.
        /// </summary>
        public static IReadOnlyList<string> Validate(
            InputMappingTemplate template,
            AimKind aimKind,
            bool requiresCommitSource)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var errors = new List<string>();
            bool hasCommit = false;
            for (int i = 0;
                 i < template.Bindings.Count;
                 i++)
            {
                InputBinding binding = template.Bindings[i];
                if (binding.Translation == InputTranslation.Commit)
                {
                    hasCommit = true;
                    if (binding.CaptureAim &&
                        aimKind != AimKind.Point &&
                        aimKind != AimKind.Unit &&
                        aimKind != AimKind.Direction)
                    {
                        errors.Add(
                            $"Commit binding on '{binding.Trigger}' captures Aim but AimKind '{aimKind}' is not aimable.");
                    }
                }
                if (binding.Translation == InputTranslation.Cancel &&
                    binding.Trigger != InputTrigger.Cancel &&
                    binding.Trigger != InputTrigger.AbilityKeyReleased &&
                    binding.Trigger != InputTrigger.SecondaryClick)
                {
                    errors.Add(
                        $"Cancel translation is only meaningful on Cancel/Release/SecondaryClick triggers, got '{binding.Trigger}'.");
                }
            }
            if (requiresCommitSource && !hasCommit)
            {
                errors.Add(
                    "Hold/guide templates must contain at least one Commit source.");
            }
            return errors;
        }
    }
}
