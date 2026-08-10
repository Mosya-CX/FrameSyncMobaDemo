using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable UID for an equipment tag asset (design v12 2.9).
    /// Zero is invalid.
    /// </summary>
    [Serializable]
    public struct EquipmentTagUid :
        IEquatable<EquipmentTagUid>,
        IComparable<EquipmentTagUid>
    {
        public int Value;

        public EquipmentTagUid(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public bool Equals(EquipmentTagUid other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is EquipmentTagUid other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public int CompareTo(EquipmentTagUid other)
        {
            return Value.CompareTo(other.Value);
        }

        public override string ToString()
        {
            return $"EquipmentTagUid({Value})";
        }

        public static bool operator ==(
            EquipmentTagUid left,
            EquipmentTagUid right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(
            EquipmentTagUid left,
            EquipmentTagUid right)
        {
            return left.Value != right.Value;
        }
    }

}
