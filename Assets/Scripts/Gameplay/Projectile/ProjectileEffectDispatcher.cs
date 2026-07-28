using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public static class ProjectileEffectDispatcher
    {
        private static readonly List<Unit> sortedTargets =
            new List<Unit>();
        private static readonly List<PhysicsEntity2D> candidates =
            new List<PhysicsEntity2D>();

        public static void DispatchOnHit(
            ProjectileRuntime projectile,
            UnitUid targetUid,
            UnitWorld unitWorld)
        {
            if (projectile == null)
                throw new System.ArgumentNullException(
                    nameof(projectile));
            if (unitWorld == null)
                throw new System.ArgumentNullException(
                    nameof(unitWorld));
            if (!unitWorld.TryGetUnit(
                    targetUid,
                    out Unit target))
                throw new DeterministicSimulationException(
                    $"Projectile hit target {targetUid} is missing.");

            ProjectileOnHitEffects effects =
                projectile.Def.OnHitEffects;
            if (!effects.HasAnyEffect) return;

            SubmitDamageEffects(
                projectile,
                target,
                unitWorld,
                effects.DamageEffects);
            ApplyBuffEffects(
                projectile,
                target,
                unitWorld,
                effects.BuffEffects);
            ApplyCrowdControlEffects(
                projectile,
                target,
                effects.CCEffects);
        }

        public static void DispatchAoE(
            ProjectileRuntime projectile,
            fp2 center,
            fp radius,
            UnitWorld unitWorld,
            PhysicsWorld physicsWorld)
        {
            ProjectileAoEConfig config =
                projectile.Def.AoE;
            if (!config.HasAoE || radius <= fp.zero)
                return;

            PhysicsSpatialGrid2D grid =
                physicsWorld?.UnitFinalGrid;
            if (grid == null) return;

            fp2 extent = new fp2(radius, radius);
            candidates.Clear();
            grid.CollectCandidates(
                new PhysicsBounds2D(
                    center - extent,
                    center + extent),
                candidates);

            sortedTargets.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                PhysicsEntity2D entity =
                    candidates[i];
                if (!(entity.QueryInfo.Owner is Unit target))
                    continue;
                if (!projectile.Def.TargetFilter.Allows(
                        target,
                        projectile.OwnerUnitUid,
                        projectile.TeamSnapshot))
                    continue;
                if (!CircleOverlapsBounds(
                        center,
                        radius,
                        entity.Bounds))
                    continue;
                sortedTargets.Add(target);
            }

            sortedTargets.Sort((a, b) =>
            {
                fp2 aPosition =
                    a.PhysicsEntity.Transform2D.Position;
                fp2 bPosition =
                    b.PhysicsEntity.Transform2D.Position;
                fp aDistance = fpmath.lengthsq(
                    aPosition - center);
                fp bDistance = fpmath.lengthsq(
                    bPosition - center);
                int comparison =
                    aDistance.CompareTo(bDistance);
                return comparison != 0
                    ? comparison
                    : a.UnitUid.CompareTo(b.UnitUid);
            });

            int maxTargets = config.MaxAoETargets > 0
                ? config.MaxAoETargets
                : sortedTargets.Count;
            for (int i = 0;
                 i < sortedTargets.Count &&
                 i < maxTargets;
                 i++)
            {
                DispatchOnHit(
                    projectile,
                    sortedTargets[i].UnitUid,
                    unitWorld);
            }
        }

        private static void SubmitDamageEffects(
            ProjectileRuntime projectile,
            Unit target,
            UnitWorld world,
            ProjectileOnHitDamage[] effects)
        {
            if (effects == null) return;
            CombatSystem combat = world.CombatSystem;
            if (combat == null)
                throw new DeterministicSimulationException(
                    "Projectile damage has no CombatSystem.");

            if (!world.TryGetUnit(
                    projectile.OwnerUnitUid,
                    out Unit source))
                throw new DeterministicSimulationException(
                    $"Projectile owner {projectile.OwnerUnitUid} is missing.");

            for (int i = 0; i < effects.Length; i++)
            {
                ProjectileOnHitDamage effect =
                    effects[i];
                if (!effect.IsValid)
                    throw new DeterministicSimulationException(
                        $"Projectile damage effect {i} is invalid.");

                fp amount = effect.Amount;
                if (effect.DamageRatio > fp.zero)
                {
                    StatId statId =
                        effect.DamageType ==
                        DamageType.Physical
                            ? StatId.AttackDamage
                            : StatId.AbilityPower;
                    amount +=
                        source.StatHandler.GetStat(statId) *
                        effect.DamageRatio;
                }

                if (amount <= fp.zero) continue;
                var request = new DamageRequest
                {
                    Header = new CombatRequestHeader
                    {
                        SourceUnitUid =
                            projectile.OwnerUnitUid,
                        TargetUnitUid =
                            target.UnitUid,
                        SourceDescriptor =
                            projectile.Source,
                        RecipeId = effect.RecipeId,
                    },
                    DamageType = effect.DamageType,
                    BaseDamage = amount,
                };
                if (!combat.SubmitDamage(request))
                    throw new DeterministicSimulationException(
                        $"Combat rejected projectile damage from {projectile.Uid}.");
            }
        }

        private static void ApplyBuffEffects(
            ProjectileRuntime projectile,
            Unit target,
            UnitWorld world,
            ProjectileOnHitBuff[] effects)
        {
            if (effects == null) return;
            if (target.BuffHandler == null)
                throw new DeterministicSimulationException(
                    $"Projectile target {target.UnitUid} has no BuffHandler.");
            if (world.BuffDefinitions == null)
                throw new DeterministicSimulationException(
                    "Projectile Buff effect has no BuffDefinitionRegistry.");

            for (int i = 0; i < effects.Length; i++)
            {
                ProjectileOnHitBuff effect = effects[i];
                if (!effect.IsValid)
                    throw new DeterministicSimulationException(
                        $"Projectile Buff effect {i} is invalid.");
                if (!world.BuffDefinitions.TryGet(
                        effect.BuffId,
                        out BuffDef definition))
                    throw new DeterministicSimulationException(
                        $"Projectile Buff effect references missing BuffConfigId {effect.BuffId.Value}.");

                target.BuffHandler.Apply(
                    effect.BuffId,
                    definition,
                    projectile.OwnerUnitUid);
            }
        }

        private static void ApplyCrowdControlEffects(
            ProjectileRuntime projectile,
            Unit target,
            ProjectileOnHitCC[] effects)
        {
            if (effects == null) return;
            if (target.CrowdControl == null)
                throw new DeterministicSimulationException(
                    $"Projectile target {target.UnitUid} has no CrowdControlHandler.");

            for (int i = 0; i < effects.Length; i++)
            {
                ProjectileOnHitCC effect = effects[i];
                if (!effect.IsValid)
                    throw new DeterministicSimulationException(
                        $"Projectile CC effect {i} is invalid.");
                target.CrowdControl.SubmitConstraint(
                    new CrowdControlConstraint
                    {
                        Type = effect.CCType,
                        RemainingTicks =
                            effect.DurationTicks,
                        Priority = 1,
                        SourceUnitUid =
                            projectile.OwnerUnitUid,
                    });
            }
        }

        private static bool CircleOverlapsBounds(
            fp2 center,
            fp radius,
            in PhysicsBounds2D bounds)
        {
            fp x = fpmath.clamp(
                center.x,
                bounds.Min.x,
                bounds.Max.x);
            fp y = fpmath.clamp(
                center.y,
                bounds.Min.y,
                bounds.Max.y);
            fp2 delta = center - new fp2(x, y);
            return fpmath.lengthsq(delta) <=
                radius * radius;
        }
    }
}
