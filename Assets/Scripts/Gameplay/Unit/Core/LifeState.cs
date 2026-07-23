namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit lifecycle state frozen by Unit v27.3 section 1.8.
    /// Unit stores LifeState; UnitWorld is the sole writer and transition validator.
    /// </summary>
    public enum LifeState : byte
    {
        Alive = 0,
        Dying = 1,
        Dead = 2,
        Respawning = 3,
    }
}