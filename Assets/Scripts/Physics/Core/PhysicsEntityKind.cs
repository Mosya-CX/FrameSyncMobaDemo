namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Business source category of a physics entity (Physics v13.1 section 2.3).
    /// Does not determine hit, movement, damage or lifecycle rules.
    /// </summary>
    public enum PhysicsEntityKind : byte
    {
        Unit = 0,
        Projectile = 1,
    }
}