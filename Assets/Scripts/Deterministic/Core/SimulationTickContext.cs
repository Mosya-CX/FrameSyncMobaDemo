using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Immutable data shared by every deterministic system during one Gameplay Tick.
    /// </summary>
    public readonly struct SimulationTickContext : IEquatable<SimulationTickContext>
    {
        private static SimulationTickContext current;
        private static bool hasCurrent;

        internal SimulationTickContext(int tick, ExecutionMode executionMode)
        {
            Tick = tick;
            DeltaTick = 1;
            ExecutionMode = executionMode;
        }

        public static SimulationTickContext Current
        {
            get
            {
                if (!hasCurrent)
                {
                    throw new InvalidOperationException(
                        "SimulationTickContext.Current is only available while a Gameplay Tick is active.");
                }

                return current;
            }
        }

        public int Tick { get; }

        public int DeltaTick { get; }

        public ExecutionMode ExecutionMode { get; }

        internal static bool HasCurrent => hasCurrent;

        internal static void SetCurrent(SimulationTickContext value)
        {
            if (hasCurrent)
            {
                throw new InvalidOperationException(
                    "A Gameplay Tick is already active. Nested Tick execution is not allowed.");
            }

            current = value;
            hasCurrent = true;
        }

        internal static void ClearCurrent()
        {
            if (!hasCurrent)
            {
                throw new InvalidOperationException("No Gameplay Tick is active.");
            }

            current = default;
            hasCurrent = false;
        }

        public bool Equals(SimulationTickContext other)
        {
            return Tick == other.Tick
                && DeltaTick == other.DeltaTick
                && ExecutionMode == other.ExecutionMode;
        }

        public override bool Equals(object obj)
        {
            return obj is SimulationTickContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Tick;
                hashCode = (hashCode * 397) ^ DeltaTick;
                hashCode = (hashCode * 397) ^ (int)ExecutionMode;
                return hashCode;
            }
        }

        public static bool operator ==(SimulationTickContext left, SimulationTickContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SimulationTickContext left, SimulationTickContext right)
        {
            return !left.Equals(right);
        }
    }
}
