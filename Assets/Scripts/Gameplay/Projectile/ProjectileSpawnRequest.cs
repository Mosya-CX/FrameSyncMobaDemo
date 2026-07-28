using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public readonly struct ProjectileSpawnRequest
    {
        public readonly int ProjectileDefId;
        public readonly UnitUid OwnerUnitUid;
        public readonly TeamId TeamSnapshot;
        public readonly SourceDescriptor Source;
        public readonly fp2 StartPosition;
        public readonly fp2 Direction;

        public ProjectileSpawnRequest(
            int projectileDefId,
            UnitUid ownerUnitUid,
            TeamId teamSnapshot,
            SourceDescriptor source,
            fp2 startPosition,
            fp2 direction)
        {
            ProjectileDefId = projectileDefId;
            OwnerUnitUid = ownerUnitUid;
            TeamSnapshot = teamSnapshot;
            Source = source;
            StartPosition = startPosition;
            Direction = direction;
        }
    }
}
