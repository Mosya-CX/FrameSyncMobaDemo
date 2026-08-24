using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: spawns a projectile from the caster's position toward
    /// the aim direction, using ProjectileWorld.RequestSpawn.
    /// </summary>
    public sealed class SpawnProjectileStageDef : StageDef
    {
        public int ProjectileDefId;
        public fp SpawnOffsetDistance;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World?.ProjectileWorld == null || ProjectileDefId <= 0)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            fp2 casterPos = caster.MovementHandler?.Position ?? fp2.zero;
            fp2 direction = session.Aim.Direction;
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(direction, out fp2 facing, out _))
                return StageResult.Failed;

            fp2 spawnPos = casterPos + facing * SpawnOffsetDistance;

            var request = new ProjectileSpawnRequest(
                ProjectileDefId,
                runtime.CasterUnitUid,
                caster.TeamId,
                new SourceDescriptor
                {
                    SourceType = CombatSourceType.Ability,
                    SourceId = runtime.Definition.AbilityId,
                    OwnerUnitUid = runtime.CasterUnitUid,
                    EmitterUnitUid = runtime.CasterUnitUid,
                },
                BuildOriginActionId(
                    session,
                    runtime,
                    caster),
                spawnPos,
                facing);

            if (!runtime.World.ProjectileWorld
                    .RequestSpawn(request)
                    .IsValid)
            {
                return StageResult.Failed;
            }
            return StageResult.Completed;
        }
    }
}
