using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Cross-Tick state required to restore the deterministic random stream.
    /// </summary>
    public readonly struct DeterministicRandomSnapshot : IEquatable<DeterministicRandomSnapshot>
    {
        public DeterministicRandomSnapshot(uint state)
        {
            State = state;
        }

        public uint State { get; }

        public bool Equals(DeterministicRandomSnapshot other)
        {
            return State == other.State;
        }

        public override bool Equals(object obj)
        {
            return obj is DeterministicRandomSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return State.GetHashCode();
        }

        public static bool operator ==(DeterministicRandomSnapshot left, DeterministicRandomSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeterministicRandomSnapshot left, DeterministicRandomSnapshot right)
        {
            return !left.Equals(right);
        }
    }
}
