using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Single immutable simulation-time view published by the FrameSync pipeline.
    /// </summary>
    public readonly struct SimulationTickContext : IEquatable<SimulationTickContext>
    {
        private static SimulationTickContext current =
            new SimulationTickContext(
                0,
                ExecutionMode.ServerAuthority);
        private static bool isTickActive;

        internal SimulationTickContext(int tick, ExecutionMode executionMode)
        {
            Tick = tick;
            DeltaTick = 1;
            ExecutionMode = executionMode;
        }

        public static SimulationTickContext Current => current;

        public int Tick { get; }

        public int DeltaTick { get; }

        public ExecutionMode ExecutionMode { get; }

        internal static bool IsTickActive => isTickActive;

        internal static void SetCurrent(SimulationTickContext value)
        {
            if (isTickActive)
            {
                throw new InvalidOperationException(
                    "A Gameplay Tick is already active. Nested Tick execution is not allowed.");
            }

            current = value;
            isTickActive = true;
        }

        internal static void CompleteCurrent()
        {
            if (!isTickActive)
            {
                throw new InvalidOperationException("No Gameplay Tick is active.");
            }

            isTickActive = false;
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
