using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public enum AoETrigger
    {
        OnDestroy,
        OnImpact,
        OnExpire,
    }

    public struct ProjectileAoEConfig
    {
        public bool HasAoE;
        public fp AoERadius;
        public int MaxAoETargets;
        public AoETrigger Trigger;
        public static readonly ProjectileAoEConfig None = default;
    }
}
