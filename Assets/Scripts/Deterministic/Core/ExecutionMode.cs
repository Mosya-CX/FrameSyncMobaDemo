namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Identifies why the current deterministic Gameplay Tick is executing.
    /// </summary>
    public enum ExecutionMode
    {
        ServerAuthority = 0,
        ClientPrediction = 1,
        ClientReplay = 2,
    }
}
