using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Kinds of deferred combat requests stored for next-Tick execution
    /// (Combat v13.2 section 12.1).
    /// </summary>
    public enum CombatRequestKind : byte
    {
        None = 0,
        Shield = 1,
        Damage = 2,
        Heal = 3,
    }
}
