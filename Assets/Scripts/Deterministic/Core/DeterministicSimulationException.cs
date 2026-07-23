using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Thrown when a deterministic simulation invariant is violated.
    /// Must never be caught and ignored in authoritative Gameplay paths.
    /// Referenced by Unit v27.3 section 1.3 for spawn-sequence overflow.
    /// </summary>
    public sealed class DeterministicSimulationException : Exception
    {
        public DeterministicSimulationException(string message)
            : base(message)
        {
        }

        public DeterministicSimulationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}