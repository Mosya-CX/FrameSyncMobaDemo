namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Context passed to Resolve and Rebuild phases (Unit v27.3 §7.15).
    /// </summary>
    public readonly struct RollbackContext
    {
        public readonly int TargetTick;
        public readonly ExecutionMode ExecutionMode;

        public RollbackContext(int targetTick, ExecutionMode executionMode)
        {
            TargetTick = targetTick;
            ExecutionMode = executionMode;
        }
    }
}