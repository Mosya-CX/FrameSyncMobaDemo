namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Unified rollback contract for authoritative state owners (Unit v27.3 §7.15).
    /// Only systems that own state affecting future simulation need to implement this.
    /// </summary>
    /// <typeparam name="TState">The snapshot state type owned by the implementor.</typeparam>
    public interface IRollback<TState>
    {
        void Capture(ref TState state);
        void Restore(in TState state);
        void Resolve(in RollbackContext context);
        void Rebuild(in RollbackContext context);
    }
}