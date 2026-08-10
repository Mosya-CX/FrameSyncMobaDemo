using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable identifier for a BuffBlackboard slot (design v14.2 5.2).
    /// Zero is invalid.
    /// </summary>
    [Serializable]
    public struct BuffStateSlotId :
        IEquatable<BuffStateSlotId>,
        IComparable<BuffStateSlotId>
    {
        public int Value;

        public BuffStateSlotId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public bool Equals(BuffStateSlotId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is BuffStateSlotId other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public int CompareTo(BuffStateSlotId other)
        {
            return Value.CompareTo(other.Value);
        }

        public override string ToString()
        {
            return $"BuffStateSlotId({Value})";
        }

        public static bool operator ==(
            BuffStateSlotId left,
            BuffStateSlotId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(
            BuffStateSlotId left,
            BuffStateSlotId right)
        {
            return left.Value != right.Value;
        }
    }

    /// <summary>
    /// Allowed deterministic value kinds in a BuffBlackboard
    /// (design v14.2 5.3).
    /// </summary>
    public enum BuffValueKind : byte
    {
        Invalid = 0,
        Int = 1,
        Bool = 2,
        Fp = 3,
        Fp2 = 4,
        UnitUid = 5,
        StableConfigId = 6,
        StatModifierHandle = 7,
        CombatModifierHandle = 8,
    }

    /// <summary>
    /// Discriminated deterministic value stored in a BuffBlackboard slot.
    /// No Unity object, CLR object or delegate is allowed.
    /// </summary>
    public struct BuffValue :
        IEquatable<BuffValue>
    {
        public BuffValueKind Kind;
        public int IntValue;
        public bool BoolValue;
        public fp FpValue;
        public fp2 Fp2Value;
        public UnitUid UnitUidValue;
        public int ConfigIdValue;
        public StatModifierHandle StatHandle;
        public CombatModifierHandle CombatHandle;

        public static BuffValue FromInt(int value) =>
            new BuffValue { Kind = BuffValueKind.Int, IntValue = value };
        public static BuffValue FromBool(bool value) =>
            new BuffValue { Kind = BuffValueKind.Bool, BoolValue = value };
        public static BuffValue FromFp(fp value) =>
            new BuffValue { Kind = BuffValueKind.Fp, FpValue = value };
        public static BuffValue FromFp2(fp2 value) =>
            new BuffValue { Kind = BuffValueKind.Fp2, Fp2Value = value };
        public static BuffValue FromUnitUid(UnitUid value) =>
            new BuffValue { Kind = BuffValueKind.UnitUid, UnitUidValue = value };
        public static BuffValue FromConfigId(int value) =>
            new BuffValue { Kind = BuffValueKind.StableConfigId, ConfigIdValue = value };
        public static BuffValue FromStatHandle(StatModifierHandle value) =>
            new BuffValue { Kind = BuffValueKind.StatModifierHandle, StatHandle = value };
        public static BuffValue FromCombatHandle(CombatModifierHandle value) =>
            new BuffValue { Kind = BuffValueKind.CombatModifierHandle, CombatHandle = value };

        public bool Equals(BuffValue other)
        {
            if (Kind != other.Kind)
                return false;
            switch (Kind)
            {
                case BuffValueKind.Int:
                    return IntValue == other.IntValue;
                case BuffValueKind.Bool:
                    return BoolValue == other.BoolValue;
                case BuffValueKind.Fp:
                    return FpValue == other.FpValue;
                case BuffValueKind.Fp2:
                    return Fp2Value.Equals(other.Fp2Value);
                case BuffValueKind.UnitUid:
                    return UnitUidValue == other.UnitUidValue;
                case BuffValueKind.StableConfigId:
                    return ConfigIdValue == other.ConfigIdValue;
                case BuffValueKind.StatModifierHandle:
                    return StatHandle.Equals(
                        other.StatHandle);
                case BuffValueKind.CombatModifierHandle:
                    return CombatHandle.Equals(
                        other.CombatHandle);
                default:
                    return true;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is BuffValue other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ IntValue;
                hash = (hash * 397) ^ BoolValue.GetHashCode();
                hash = (hash * 397) ^ FpValue.GetHashCode();
                hash = (hash * 397) ^ Fp2Value.GetHashCode();
                hash = (hash * 397) ^ UnitUidValue.GetHashCode();
                hash = (hash * 397) ^ ConfigIdValue;
                hash = (hash * 397) ^ StatHandle.GetHashCode();
                hash = (hash * 397) ^ CombatHandle.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// One declared slot in a BuffBlackboardLayout (design v14.2 5.2).
    /// </summary>
    [Serializable]
    public sealed class BuffStateSlotDefinition
    {
        public BuffStateSlotId SlotId;
        public BuffValueKind Kind;
        public BuffValue DefaultValue;
    }

    /// <summary>
    /// Static slot layout owned by BuffDefinition (design v14.2 5.2).
    /// </summary>
    [Serializable]
    public sealed class BuffBlackboardLayout
    {
        public BuffStateSlotDefinition[] Slots;
    }
}
