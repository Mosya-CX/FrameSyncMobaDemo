using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class CombatModifierRecord
    {
        public ulong Id;
        public CombatDomain Domain;
        public CombatModifierScope Scope;
        public CombatModifierMatch Match;
        public CombatFormulaPatch[] ValuePatches =
            Array.Empty<CombatFormulaPatch>();
        public CombatPolicyPatch[] PolicyPatches =
            Array.Empty<CombatPolicyPatch>();
    }

    public readonly struct CombatModifierMatch
    {
        public readonly SourceTypeMask SourceTypes;
        public readonly int SourceId;
        public readonly int RecipeId;
        public readonly DamageTypeMask DamageTypes;
        /// <summary>Bit mask over UnitKind (1 &lt;&lt; (int)UnitKind).
        /// Zero matches every target kind.</summary>
        public readonly ulong TargetKinds;

        public CombatModifierMatch(
            SourceTypeMask sourceTypes,
            int sourceId,
            int recipeId,
            DamageTypeMask damageTypes)
            : this(
                sourceTypes,
                sourceId,
                recipeId,
                damageTypes,
                targetKinds: 0UL)
        {
        }

        public CombatModifierMatch(
            SourceTypeMask sourceTypes,
            int sourceId,
            int recipeId,
            DamageTypeMask damageTypes,
            ulong targetKinds)
        {
            SourceTypes = sourceTypes;
            SourceId = sourceId;
            RecipeId = recipeId;
            DamageTypes = damageTypes;
            TargetKinds = targetKinds;
        }

        public bool Matches(
            in CombatRequestHeader header,
            DamageType damageType,
            UnitKind targetKind)
        {
            SourceTypeMask source =
                (SourceTypeMask)(1 <<
                    (int)header.SourceDescriptor.SourceType);
            DamageTypeMask damage =
                (DamageTypeMask)(1 << (int)damageType);
            return (SourceTypes == SourceTypeMask.None ||
                    (SourceTypes & source) != 0) &&
                   (SourceId == 0 ||
                    SourceId ==
                    header.SourceDescriptor.SourceId) &&
                   (RecipeId == 0 ||
                    RecipeId == header.RecipeId) &&
                   (DamageTypes == DamageTypeMask.None ||
                    (DamageTypes & damage) != 0) &&
                   (TargetKinds == 0UL ||
                    (TargetKinds &
                     (1UL << (int)targetKind)) != 0);
        }
    }

    public readonly struct CombatFormulaPatch
    {
        public readonly CombatFormulaSlot Slot;
        public readonly CombatModifierOperation Operation;
        public readonly CombatOperand Operand;

        public CombatFormulaPatch(
            CombatFormulaSlot slot,
            CombatModifierOperation operation,
            in CombatOperand operand)
        {
            Slot = slot;
            Operation = operation;
            Operand = operand;
        }
    }

    public readonly struct CombatOperand
    {
        public readonly fp Constant;
        public readonly CombatOperandTerm[] Terms;

        public CombatOperand(
            fp constant,
            CombatOperandTerm[] terms = null)
        {
            Constant = constant;
            Terms = terms == null
                ? Array.Empty<CombatOperandTerm>()
                : (CombatOperandTerm[])terms.Clone();
        }

        public fp Evaluate(
            fp baseValue,
            fp currentSlotValue,
            StatHandler sourceStats,
            StatHandler targetStats,
            fp targetBatchStartHealth)
        {
            fp value = Constant;
            CombatOperandTerm[] terms =
                Terms ?? Array.Empty<CombatOperandTerm>();
            for (int i = 0; i < terms.Length; i++)
            {
                CombatOperandTerm term = terms[i];
                fp referenced;
                switch (term.Value.Kind)
                {
                    case CombatValueRefKind.BaseValue:
                        referenced = baseValue;
                        break;
                    case CombatValueRefKind.CurrentSlotValue:
                        referenced = currentSlotValue;
                        break;
                    case CombatValueRefKind.SourceStat:
                        referenced = ReadStat(
                            sourceStats,
                            term.Value.ValueId);
                        break;
                    case CombatValueRefKind.TargetStat:
                        referenced = ReadStat(
                            targetStats,
                            term.Value.ValueId);
                        break;
                    case CombatValueRefKind.TargetCurrentHealth:
                        referenced = targetBatchStartHealth;
                        break;
                    default:
                        throw new DeterministicSimulationException(
                            $"Unknown Combat value reference {term.Value.Kind}.");
                }
                value += referenced * term.Coefficient;
            }
            return value;
        }

        private static fp ReadStat(
            StatHandler stats,
            ushort valueId)
        {
            if (stats == null ||
                !Enum.IsDefined(typeof(StatId), (int)valueId))
                throw new DeterministicSimulationException(
                    $"Combat operand references unavailable StatId {valueId}.");
            return stats.GetStat((StatId)valueId);
        }
    }

    public readonly struct CombatOperandTerm
    {
        public readonly CombatValueRef Value;
        public readonly fp Coefficient;

        public CombatOperandTerm(
            in CombatValueRef value,
            fp coefficient)
        {
            Value = value;
            Coefficient = coefficient;
        }
    }

    public readonly struct CombatValueRef
    {
        public readonly CombatValueRefKind Kind;
        public readonly ushort ValueId;

        public CombatValueRef(
            CombatValueRefKind kind,
            ushort valueId = 0)
        {
            Kind = kind;
            ValueId = valueId;
        }
    }

    public readonly struct CombatPolicyPatch
    {
        public readonly CombatPolicyKind Kind;

        public CombatPolicyPatch(CombatPolicyKind kind)
        {
            Kind = kind;
        }
    }
}
