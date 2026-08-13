using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Spawns a directional projectile whose damage payload is resolved from
    /// the active ability rank at cast time.
    /// </summary>
    public sealed class ScaledProjectileStageDef : StageDef
    {
        public int ProjectileDefId;
        public fp SpawnOffsetDistance;
        public AbilityLevelValue BaseDamageByLevel;
        public AbilityLevelValue AttackDamageRatioByLevel;
        public DamageType DamageType;
        public fp MinionDamageMultiplier;
        public int RecipeId;
        public int SpawnDelayTicks;

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            return runtime?.World?.ProjectileWorld != null
                ? StageResult.Running
                : StageResult.Failed;
        }

        public override StageResult OnTick(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (session.StageElapsedTicks < SpawnDelayTicks)
                return StageResult.Running;
            if (runtime.World?.ProjectileWorld == null ||
                ProjectileDefId <= 0 ||
                RecipeId <= 0 ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    session.Aim.Direction,
                    out fp2 facing,
                    out _))
            {
                return StageResult.Failed;
            }

            fp2 origin = caster.PhysicsEntity.Transform2D.Position +
                facing * SpawnOffsetDistance;
            var damage = new ProjectileOnHitDamage
            {
                Amount = BaseDamageByLevel.Resolve(runtime.Level),
                DamageType = DamageType,
                DamageRatio = AttackDamageRatioByLevel.Resolve(runtime.Level),
                MinionDamageMultiplier = MinionDamageMultiplier,
                RecipeId = RecipeId,
            };
            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Ability,
                SourceId = runtime.Definition.AbilityId,
                OwnerUnitUid = caster.UnitUid,
                EmitterUnitUid = caster.UnitUid,
            };
            ProjectileUid uid = runtime.World.ProjectileWorld.RequestSpawn(
                new ProjectileSpawnRequest(
                    ProjectileDefId,
                    caster.UnitUid,
                    caster.TeamId,
                    source,
                    origin,
                    facing,
                    new[] { damage }));
            return uid.IsValid
                ? StageResult.Completed
                : StageResult.Failed;
        }
    }
}
