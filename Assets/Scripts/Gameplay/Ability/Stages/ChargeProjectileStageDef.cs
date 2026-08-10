using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Release stage for charged directional projectiles. Reads the charge
    /// ratio (and an optional empowered flag) from the ability Blackboard,
    /// linearly interpolates base damage, attack-damage ratio, range and
    /// missing-health damage, then spawns a projectile with a per-instance
    /// on-hit damage override and lifetime (cast range).
    /// </summary>
    public sealed class ChargeProjectileStageDef : StageDef
    {
        public int ProjectileDefId;
        public fp SpawnOffsetDistance;
        public int ChargeRatioBlackboardKeyId;
        public int EmpoweredBlackboardKeyId;
        public AbilityLevelValue MinBaseDamageByLevel;
        public AbilityLevelValue MaxBaseDamageByLevel;
        public AbilityLevelValue MinAttackDamageRatioByLevel;
        public AbilityLevelValue MaxAttackDamageRatioByLevel;
        public AbilityLevelValue MinMissingHpRatioByLevel;
        public AbilityLevelValue MaxMissingHpRatioByLevel;
        public fp MinRange;
        public fp MaxRange;
        public fp FalloffPerHitPercent;
        public fp MinDamageRatio;
        public int RecipeId;

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (ProjectileDefId <= 0 ||
                ChargeRatioBlackboardKeyId <= 0 ||
                RecipeId <= 0 ||
                runtime.World?.ProjectileWorld == null)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster))
                return StageResult.Failed;
            if (!session.Blackboard.TryGet(
                    new AbilityBlackboardKey<fp>(
                        ChargeRatioBlackboardKeyId),
                    out fp chargeRatio))
                return StageResult.Failed;

            bool empowered = false;
            if (EmpoweredBlackboardKeyId > 0 &&
                session.Blackboard.TryGet(
                    new AbilityBlackboardKey<fp>(
                        EmpoweredBlackboardKeyId),
                    out fp empoweredValue) &&
                empoweredValue > fp.zero)
            {
                empowered = true;
            }

            int level = runtime.Level;
            fp baseDamage = Lerp(
                MinBaseDamageByLevel,
                MaxBaseDamageByLevel,
                chargeRatio,
                level);
            fp adRatio = Lerp(
                MinAttackDamageRatioByLevel,
                MaxAttackDamageRatioByLevel,
                chargeRatio,
                level);
            fp missingHpRatio = empowered
                ? Lerp(
                    MinMissingHpRatioByLevel,
                    MaxMissingHpRatioByLevel,
                    chargeRatio,
                    level)
                : fp.zero;

            fp range = MinRange +
                (MaxRange - MinRange) * chargeRatio;
            ProjectileDef def =
                runtime.World.ProjectileWorld.DefRegistry
                    ?.FindById(ProjectileDefId);
            if (def == null || def.Speed <= fp.zero)
                return StageResult.Failed;
            // Def.Speed is logic units per second; convert the flight time
            // (range / speed) into logic ticks using the world's seconds
            // per Tick (e.g. 30tps -> 1/30).
            fp logicSecondsPerTick =
                runtime.World.ProjectileWorld
                    .LogicSecondsPerTick > fp.zero
                    ? runtime.World.ProjectileWorld
                        .LogicSecondsPerTick
                    : fp.one;
            fp lifetimeTicks =
                range / (def.Speed * logicSecondsPerTick);
            int lifetime =
                (int)fpmath.ceil(lifetimeTicks);
            if (lifetime <= 0)
                lifetime = 1;

            fp2 direction = session.Aim.Direction;
            fp2 casterPos =
                caster.MovementHandler?.Position ??
                fp2.zero;
            fp2 spawnPos = casterPos +
                direction * SpawnOffsetDistance;

            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Ability,
                SourceId = runtime.Definition.AbilityId,
                OwnerUnitUid = runtime.CasterUnitUid,
                EmitterUnitUid = runtime.CasterUnitUid,
            };

            var physical = new ProjectileOnHitDamage
            {
                Amount = baseDamage,
                DamageType = DamageType.Physical,
                DamageRatio = adRatio,
                FalloffPerHitPercent = FalloffPerHitPercent,
                MinDamageRatio = MinDamageRatio,
                RecipeId = RecipeId,
            };
            var empoweredDamage = empowered
                ? new ProjectileOnHitDamage
                {
                    Amount = fp.zero,
                    DamageType = DamageType.Magic,
                    MissingHpRatio = missingHpRatio,
                    RecipeId = RecipeId,
                }
                : default;

            var request = new ProjectileSpawnRequest(
                ProjectileDefId,
                runtime.CasterUnitUid,
                caster.TeamId,
                source,
                spawnPos,
                direction,
                empowered
                    ? new[]
                    {
                        physical,
                        empoweredDamage,
                    }
                    : new[]
                    {
                        physical,
                    },
                lifetime);

            runtime.World.ProjectileWorld.RequestSpawn(
                request);
            return StageResult.Completed;
        }

        private static fp Lerp(
            AbilityLevelValue min,
            AbilityLevelValue max,
            fp ratio,
            int level)
        {
            return min.Resolve(level) +
                (max.Resolve(level) -
                 min.Resolve(level)) * ratio;
        }
    }
}
