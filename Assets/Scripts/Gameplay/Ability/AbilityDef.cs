using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityDef
    {
        public int AbilityId;
        public string Name;
        /// <summary>
        /// Whether this ability is the unit's ultimate (usually the R slot).
        /// Used for skill-point UI (LockMask) and slot-learning rules; the
        /// actual level gates/max ranks live on the AbilitySlotDef.
        /// </summary>
        public bool IsUltimate;
        /// <summary>Stable client Addressables address for the default UI
        /// icon. Gameplay never loads or reads the Sprite asset.</summary>
        public string IconAddress;
        /// <summary>Base cooldown by ability level (design v15.2 5.5).</summary>
        public AbilityLevelValue CooldownByLevel;
        public AbilityCostPlan CostPlan;
        public CastModelDef CastModel;
        public fp CastRange;
        public AimKind AimKind;
        public AbilityCastConditionDef[] CastConditions =
            Array.Empty<AbilityCastConditionDef>();
        public ActiveAbilityPassiveEffectDef PassiveEffect;

        public bool IsValid =>
            AbilityId > 0 &&
            CastRange >= fp.zero &&
            CastModel != null &&
            CostPlan.IsValid;

        public int GetCooldownTicks(int abilityLevel)
        {
            // Unlearned abilities (level 0) have no cooldown to display;
            // the HUD's LockMask covers them.
            if (abilityLevel <= 0 ||
                !CooldownByLevel.HasValue)
                return 0;
            return (int)CooldownByLevel.Resolve(
                abilityLevel);
        }
    }

    [Serializable]
    public struct AbilityLevelValue
    {
        [SerializeField] private fp[] values;

        public AbilityLevelValue(fp[] values)
        {
            this.values = values == null
                ? Array.Empty<fp>()
                : (fp[])values.Clone();
            for (int i = 0; i < this.values.Length; i++)
            {
                if (this.values[i] < fp.zero)
                    throw new ArgumentOutOfRangeException(
                        nameof(values),
                        "Ability level values must be nonnegative.");
            }
        }

        public bool HasValue => values != null && values.Length > 0;
        public int Count => values?.Length ?? 0;

        public fp Resolve(int abilityLevel)
        {
            if (!HasValue) return fp.zero;
            // Skills now start at level 0 (unlearned). Level-0 lookups
            // resolve to the rank-1 value instead of throwing, and anything
            // above the configured ranks clamps to the last rank.
            int index = abilityLevel <= 0
                ? 0
                : abilityLevel - 1;
            if (index >= values.Length)
            {
                index = values.Length - 1;
            }
            return values[index];
        }

        public fp[] CopyValues() =>
            values == null ? Array.Empty<fp>() : (fp[])values.Clone();
    }

    public enum AbilityCostTiming : byte
    {
        OnSessionStart = 0,
        OnFirstCommit = 1,
    }

    public readonly struct AbilityCostPlan
    {
        public readonly AbilityLevelValue CastResourceCost;
        public readonly AbilityLevelValue HealthCost;
        public readonly AbilityCostTiming Timing;

        public AbilityCostPlan(
            AbilityLevelValue castResourceCost,
            AbilityLevelValue healthCost,
            AbilityCostTiming timing)
        {
            CastResourceCost = castResourceCost;
            HealthCost = healthCost;
            Timing = timing;
        }

        public bool HasCost =>
            CastResourceCost.HasValue || HealthCost.HasValue;
        public bool IsValid =>
            Enum.IsDefined(typeof(AbilityCostTiming), Timing);

        public void Resolve(
            int abilityLevel,
            out fp resourceCost,
            out fp healthCost)
        {
            resourceCost = CastResourceCost.Resolve(abilityLevel);
            healthCost = HealthCost.Resolve(abilityLevel);
        }
    }

    public readonly struct AbilityCastContext
    {
        public readonly Unit Caster;
        public readonly AbilityRuntime Runtime;
        public readonly AbilitySignal Signal;

        public AbilityCastContext(
            Unit caster,
            AbilityRuntime runtime,
            in AbilitySignal signal)
        {
            Caster = caster;
            Runtime = runtime;
            Signal = signal;
        }
    }

    public abstract class AbilityCastConditionDef
    {
        public abstract bool CanCast(in AbilityCastContext context);
    }
}
