using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Tower-specific AttackHandler (NonHero v5 搂9). Keeps the base attack
    /// cycle and adds:
    ///   - hero damage ramp: first hit = base attack damage (180), every
    ///     following hit on the same hero x1.5, capped at 600;
    ///   - in-flight projectile locking: while a tower shot is unresolved the
    ///     AI keeps the same target (HasUnresolvedProjectile).
    /// Deterministic: ramp counters and the pending projectile are part of
    /// AttackSnapshot (rollback-safe).
    /// </summary>
    public class TowerAttackHandler : AttackHandler
    {
        private static readonly fp RampMultiplier =
            (fp)1.5m;
        private static readonly fp MaxRampDamage =
            (fp)600m;

        private UnitUid rampTarget;
        private int rampHits;
        private ProjectileUid pendingProjectile;
        private UnitUid lockedTarget;

        public bool HasUnresolvedProjectile =>
            pendingProjectile.IsValid &&
            Owner?.World?.ProjectileWorld != null &&
            Owner.World.ProjectileWorld.TryGet(
                pendingProjectile,
                out _);

        public UnitUid LockedTarget => lockedTarget;

        protected override ProjectileSpawnRequest
            BuildProjectileSpawnRequest(Unit target)
        {
            ProjectileSpawnRequest baseRequest =
                base.BuildProjectileSpawnRequest(
                    target);
            fp damage = ResolveTowerDamage(
                target);
            return new ProjectileSpawnRequest(
                baseRequest.ProjectileDefId,
                baseRequest.OwnerUnitUid,
                baseRequest.TeamSnapshot,
                baseRequest.Source,
                baseRequest.StartPosition,
                baseRequest.Direction,
                onHitDamageOverride: new[]
                {
                    new ProjectileOnHitDamage
                    {
                        Amount = damage,
                        DamageType =
                            DamageType.Physical,
                        RecipeId =
                            CombatBuiltinRecipeId
                                .BasicAttackDamage,
                    },
                },
                maxLifetimeTicksOverride:
                    baseRequest
                        .MaxLifetimeTicksOverride,
                targetUnitUid:
                    baseRequest.TargetUnitUid);
        }

        protected override void OnProjectileCommitted(
            ProjectileUid uid)
        {
            pendingProjectile = uid;
            lockedTarget = CurrentTargetUid;
            if (lockedTarget.IsValid() &&
                IsHero(lockedTarget))
            {
                if (lockedTarget != rampTarget)
                {
                    rampTarget = lockedTarget;
                    rampHits = 0;
                }
                rampHits++;
            }
        }

        private fp ResolveTowerDamage(
            Unit target)
        {
            fp baseDamage = GetAttackDamage();
            if (target == null ||
                target.UnitKind != UnitKind.Hero)
            {
                // Minions and other non-hero targets: flat base damage.
                return baseDamage;
            }
            return ResolveRampDamage(
                baseDamage,
                rampHits);
        }

        /// <summary>
        /// Hero ramp formula: base * 1.5 ^ hits, capped at 600
        /// (hits = number of already-fired hits on the same hero).
        /// </summary>
        internal static fp ResolveRampDamage(
            fp baseDamage,
            int hits)
        {
            fp damage = baseDamage;
            for (int i = 0; i < hits; i++)
            {
                damage *= RampMultiplier;
                if (damage > MaxRampDamage)
                {
                    damage = MaxRampDamage;
                    break;
                }
            }
            return damage;
        }

        private bool IsHero(UnitUid uid)
        {
            return Owner?.World != null &&
                Owner.World.TryGetUnit(
                    uid,
                    out Unit target) &&
                target.UnitKind == UnitKind.Hero;
        }

        public override void Capture(
            ref AttackSnapshot state)
        {
            base.Capture(ref state);
            state.RampTargetUnitUid = rampTarget;
            state.RampHitCount = rampHits;
            state.PendingProjectileUid =
                pendingProjectile;
            state.LockedTargetUnitUid =
                lockedTarget;
        }

        public override void Restore(
            in AttackSnapshot state)
        {
            base.Restore(in state);
            rampTarget = state.RampTargetUnitUid;
            rampHits = state.RampHitCount;
            pendingProjectile =
                state.PendingProjectileUid;
            lockedTarget =
                state.LockedTargetUnitUid;
        }
    }
}
