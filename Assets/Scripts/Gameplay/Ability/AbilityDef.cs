using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityDef
    {
        public int AbilityId;
        public string Name;
        public int CooldownTicks;
        public AbilityCostPlan CostPlan;
        public CastModelDef CastModel;
        public fp CastRange;
        public AimKind AimKind;
        public AbilityCastConditionDef[] CastConditions =
            Array.Empty<AbilityCastConditionDef>();
        public ActiveAbilityPassiveEffectDef PassiveEffect;

        public bool IsValid =>
            AbilityId > 0 &&
            CooldownTicks >= 0 &&
            CastRange >= fp.zero &&
            CastModel != null &&
            CostPlan.IsValid;
    }

    public readonly struct AbilityLevelValue
    {
        private readonly fp[] values;

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
            if (abilityLevel <= 0 || abilityLevel > values.Length)
                throw new DeterministicSimulationException(
                    $"Ability level {abilityLevel} is outside configured range 1..{values.Length}.");
            return values[abilityLevel - 1];
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
