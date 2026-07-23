using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public static class ProjectileEffectDispatcher
    {
        private static readonly System.Collections.Generic.List<Unit> SortedTargets
            = new System.Collections.Generic.List<Unit>();

        public static void DispatchOnHit(
            ProjectileRuntime proj,
            UnitUid targetUid,
            UnitWorld unitWorld)
        {
            if (!unitWorld.TryGetUnit(targetUid, out Unit target)) return;
            var effects = proj.Def.OnHitEffects;
            if (!effects.HasAnyEffect) return;

            // Damage effects
            if (effects.DamageEffects != null)
            {
                for (int i = 0; i < effects.DamageEffects.Length; i++)
                {
                    var dmg = effects.DamageEffects[i];
                    int amount = dmg.Amount;
                    if (dmg.DamageRatio > fp.zero && unitWorld.TryGetUnit(proj.OwnerUnitUid, out Unit source))
                    {
                        var stat = dmg.DamageType == DamageType.Physical
                            ? source.StatHandler?.GetStat(StatId.AttackDamage) ?? fp.zero
                            : source.StatHandler?.GetStat(StatId.AbilityPower) ?? fp.zero;
                        amount += (int)((stat * dmg.DamageRatio).RawValue >> 16);
                    }
                    if (amount <= 0) continue;
                    ApplyDirectDamage(unitWorld, targetUid, new fp(amount));
                }
            }

            // Buff effects
            if (effects.BuffEffects != null)
            {
                var buffHandler = target.BuffHandler;
                if (buffHandler != null)
                {
                    for (int i = 0; i < effects.BuffEffects.Length; i++)
                    {
                        var b = effects.BuffEffects[i];
                        if (!b.IsValid) continue;
                        var def = ResolveBuffDef(b.BuffId, target);
                        if (def != null)
                            buffHandler.Apply(b.BuffId, def, proj.OwnerUnitUid);
                    }
                }
            }

            // CC effects
            if (effects.CCEffects != null)
            {
                var ccHandler = target.CrowdControl;
                if (ccHandler != null)
                {
                    for (int i = 0; i < effects.CCEffects.Length; i++)
                    {
                        var c = effects.CCEffects[i];
                        if (!c.IsValid) continue;
                        var constraint = new CrowdControlConstraint
                        {
                            Type = c.CCType,
                            RemainingTicks = c.DurationTicks,
                            Priority = 1,
                            SourceUnitUid = proj.OwnerUnitUid,
                        };
                        ccHandler.SubmitConstraint(constraint);
                    }
                }
            }
        }

        public static void DispatchAoE(
            ProjectileRuntime proj,
            fp2 center,
            fp radius,
            UnitWorld unitWorld,
            PhysicsWorld physicsWorld)
        {
            var config = proj.Def.AoE;
            if (!config.HasAoE || radius <= fp.zero) return;

            var grid = physicsWorld?.UnitFinalGrid;
            if (grid == null) return;

            fp2 min = center - new fp2(radius, radius);
            fp2 max = center + new fp2(radius, radius);
            var queryBounds = new PhysicsBounds2D(min, max);

            var candidates = new System.Collections.Generic.List<Physics.PhysicsEntity2D>();
            grid.CollectCandidates(queryBounds, candidates);

            SortedTargets.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                var entity = candidates[i];
                if (!(entity.QueryInfo.Owner is Unit targetUnit)) continue;
                if (!targetUnit.UnitUid.IsValid()) continue;
                if (targetUnit.UnitUid == proj.OwnerUnitUid) continue;
                if (targetUnit.LifeState != LifeState.Alive && targetUnit.LifeState != LifeState.Dying) continue;

                fp2 targetPos = entity.Transform2D.Position;
                if (PhysicsGeometry2D.PointOverlapsCircle(targetPos, center, radius))
                    SortedTargets.Add(targetUnit);
            }

            SortedTargets.Sort((a, b) =>
            {
                fp2 pa = a.MovementHandler?.Snapshot.Position ?? fp2.zero;
                fp2 pb = b.MovementHandler?.Snapshot.Position ?? fp2.zero;
                fp dA = fpmath.dot(pa - center, pa - center);
                fp dB = fpmath.dot(pb - center, pb - center);
                int cmp = dA.CompareTo(dB);
                if (cmp != 0) return cmp;
                return a.UnitUid.CompareTo(b.UnitUid);
            });

            int maxTargets = config.MaxAoETargets > 0 ? config.MaxAoETargets : SortedTargets.Count;
            int hitCount = 0;
            for (int i = 0; i < SortedTargets.Count && hitCount < maxTargets; i++)
            {
                DispatchOnHit(proj, SortedTargets[i].UnitUid, unitWorld);
                hitCount++;
            }
        }

        private static BuffDef ResolveBuffDef(BuffConfigId configId, Unit target)
        {
            return null; // BuffDef registry deferred to later plan
        }

        private static void ApplyDirectDamage(UnitWorld world, UnitUid targetUid, fp amount)
        {
            if (!world.TryGetUnit(targetUid, out Unit target)) return;
            if (target.LifeState != LifeState.Alive && target.LifeState != LifeState.Dying) return;
            var stats = target.StatHandler;
            if (stats == null) return;
            fp curHp = stats.CurrentHealth;
            fp newHp = curHp - amount;
            if (newHp < fp.zero) newHp = fp.zero;
            stats.SetCurrentHealth(newHp);
        }
    }
}
