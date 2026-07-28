using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct DamageRequest
    {
        public CombatRequestHeader Header;

        public UnitUid SourceUnitUid
        {
            get => Header.SourceUnitUid;
            set => Header.SourceUnitUid = value;
        }

        public UnitUid TargetUnitUid
        {
            get => Header.TargetUnitUid;
            set => Header.TargetUnitUid = value;
        }

        public DamageType DamageType;
        public fp BaseDamage;
        public ProjectileUid? ProjectileSourceUid;

        public bool IsValid =>
            SourceUnitUid.IsValid() &&
            TargetUnitUid.IsValid() &&
            Header.SourceDescriptor.IsValid &&
            Header.RecipeId > 0 &&
            BaseDamage > fp.zero;

        public static readonly DamageRequest None = default;
    }
}
