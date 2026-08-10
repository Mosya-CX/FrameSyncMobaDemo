using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct BuffConfigId : IEquatable<BuffConfigId>, IComparable<BuffConfigId>
    {
        public int Value;

        public BuffConfigId(int value)
        {
            Value = value;
        }

        public bool Equals(BuffConfigId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BuffConfigId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(BuffConfigId other) => Value.CompareTo(other.Value);

        public static bool operator ==(BuffConfigId left, BuffConfigId right) => left.Equals(right);
        public static bool operator !=(BuffConfigId left, BuffConfigId right) => !left.Equals(right);

        public static readonly BuffConfigId Invalid = new BuffConfigId(0);
        public bool IsValid => Value != 0;

        public override string ToString() => $"BuffConfigId({Value})";
    }
}
