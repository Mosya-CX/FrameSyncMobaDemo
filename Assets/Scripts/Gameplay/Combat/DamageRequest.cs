using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct DamageRequest
    {
        public CombatRequestHeader Header;
        public UnitUid SourceUnitUid;
        public UnitUid TargetUnitUid;
        public DamageType DamageType;
        public fp BaseDamage;
        public byte AttackSequenceIndex;
        public ProjectileUid? ProjectileSourceUid;

        public bool IsValid =>
            SourceUnitUid.IsValid() && TargetUnitUid.IsValid() && BaseDamage > fp.zero;

        public static readonly DamageRequest None = default;
    }
}
