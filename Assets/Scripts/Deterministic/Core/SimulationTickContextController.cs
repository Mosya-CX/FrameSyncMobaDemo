using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Owns the write boundary for the global Tick context.
    /// </summary>
    public sealed class SimulationTickContextController
    {
        public bool IsTickActive { get; private set; }

        public void BeginTick(int tick, ExecutionMode executionMode)
        {
            if (IsTickActive || SimulationTickContext.IsTickActive)
            {
                throw new InvalidOperationException(
                    "A Gameplay Tick is already active. End it before beginning another Tick.");
            }

            SimulationTickContext.SetCurrent(new SimulationTickContext(tick, executionMode));
            IsTickActive = true;
        }

        public void EndTick()
        {
            if (!IsTickActive)
            {
                throw new InvalidOperationException("This controller does not own an active Gameplay Tick.");
            }

            SimulationTickContext.CompleteCurrent();
            IsTickActive = false;
        }
    }
}
