using System;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum ProjectileTeamRule : byte
    {
        Enemy = 0,
        Ally = 1,
        Self = 2,
        All = 3,
    }

    [Flags]
    public enum ProjectileUnitKindMask : byte
    {
        None = 0,
        Hero = 1 << (int)UnitKind.Hero,
        Minion = 1 << (int)UnitKind.Minion,
        Monster = 1 << (int)UnitKind.Monster,
        Structure = 1 << (int)UnitKind.Structure,
        All = Hero | Minion | Monster | Structure,
    }

    [Flags]
    public enum ProjectileLifeStateMask : byte
    {
        None = 0,
        Alive = 1 << (int)LifeState.Alive,
        Dying = 1 << (int)LifeState.Dying,
        Dead = 1 << (int)LifeState.Dead,
        Respawning = 1 << (int)LifeState.Respawning,
        All = Alive | Dying | Dead | Respawning,
    }

    public enum HitSameTargetPolicy : byte
    {
        Once = 0,
        Cooldown = 1,
        Unrestricted = 2,
    }

    public enum ProjectileEndReason : byte
    {
        None = 0,
        LifetimeExpired = 1,
        HitPolicyExhausted = 2,
        ExplicitRequest = 3,
    }

    [Serializable]
    public struct ProjectileTargetFilter
    {
        public ProjectileTeamRule TeamRule;
        public ProjectileUnitKindMask UnitKindMask;
        public ushort[] IncludeSubKindIds;
        public ushort[] ExcludeSubKindIds;
        public int[] IncludePrototypeIds;
        public int[] ExcludePrototypeIds;
        public ProjectileLifeStateMask AllowedLifeStates;
        public bool RequireTargetable;

        public static ProjectileTargetFilter DefaultEnemy =>
            new ProjectileTargetFilter
            {
                TeamRule = ProjectileTeamRule.Enemy,
                UnitKindMask = ProjectileUnitKindMask.All,
                AllowedLifeStates = ProjectileLifeStateMask.Alive,
                RequireTargetable = true,
            };

        public bool Allows(
            Unit target,
            UnitUid ownerUid,
            TeamId projectileTeam)
        {
            if (target == null || !target.UnitUid.IsValid())
                return false;

            bool isSelf = target.UnitUid == ownerUid;
            bool sameTeam = target.TeamId == projectileTeam;
            switch (TeamRule)
            {
                case ProjectileTeamRule.Enemy:
                    if (isSelf || sameTeam ||
                        target.TeamId == TeamId.Neutral ||
                        projectileTeam == TeamId.Neutral)
                        return false;
                    break;
                case ProjectileTeamRule.Ally:
                    if (isSelf || !sameTeam) return false;
                    break;
                case ProjectileTeamRule.Self:
                    if (!isSelf) return false;
                    break;
                case ProjectileTeamRule.All:
                    break;
                default:
                    return false;
            }

            int kindBit = 1 << (int)target.UnitKind;
            if (((int)UnitKindMask & kindBit) == 0)
                return false;

            int lifeBit = 1 << (int)target.LifeState;
            if (((int)AllowedLifeStates & lifeBit) == 0)
                return false;
            if (RequireTargetable &&
                !target.CapabilityState.IsTargetable)
                return false;

            if (!AllowsValue(
                    target.UnitSubKindId,
                    IncludeSubKindIds,
                    ExcludeSubKindIds))
                return false;
            return AllowsValue(
                target.UnitPrototypeId,
                IncludePrototypeIds,
                ExcludePrototypeIds);
        }

        public void ValidateOrThrow()
        {
            if (UnitKindMask == ProjectileUnitKindMask.None)
                throw new InvalidOperationException(
                    "Projectile target UnitKindMask must not be empty.");
            if (AllowedLifeStates == ProjectileLifeStateMask.None)
                throw new InvalidOperationException(
                    "Projectile target AllowedLifeStates must not be empty.");
            ValidateUnique(IncludeSubKindIds, nameof(IncludeSubKindIds));
            ValidateUnique(ExcludeSubKindIds, nameof(ExcludeSubKindIds));
            ValidateUnique(IncludePrototypeIds, nameof(IncludePrototypeIds));
            ValidateUnique(ExcludePrototypeIds, nameof(ExcludePrototypeIds));
        }

        private static bool AllowsValue<T>(
            T value,
            T[] includes,
            T[] excludes)
            where T : IEquatable<T>
        {
            if (Contains(excludes, value)) return false;
            return includes == null ||
                includes.Length == 0 ||
                Contains(includes, value);
        }

        private static bool Contains<T>(T[] values, T value)
            where T : IEquatable<T>
        {
            if (values == null) return false;
            for (int i = 0; i < values.Length; i++)
                if (values[i].Equals(value)) return true;
            return false;
        }

        private static void ValidateUnique<T>(
            T[] values,
            string fieldName)
            where T : IEquatable<T>
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                for (int j = i + 1; j < values.Length; j++)
                    if (values[i].Equals(values[j]))
                        throw new InvalidOperationException(
                            $"{fieldName} contains duplicate value {values[i]}.");
        }
    }

    [Serializable]
    public struct ProjectileHitPolicy
    {
        public bool Enabled;
        public DurationAuthoring QueryInterval;
        [HideInInspector]
        public int QueryIntervalTicks;
        public HitSameTargetPolicy SameTargetPolicy;
        public DurationAuthoring SameTargetCooldown;
        [HideInInspector]
        public int SameTargetCooldownTicks;
        public int MaxTotalHitCount;
        public int InitialPierceCount;
        public int InitialBounceCount;
        /// <summary>
        /// When true and the projectile has a locked TargetUnitUid, only
        /// that tracked target can be hit, even if other valid targets
        /// overlap the swept path (single-target homing attacks). Area and
        /// pierce projectiles keep the flag false.
        /// </summary>
        public bool RestrictToTrackedTarget;
        public bool EndOnFirstValidHit;
        public bool StopResolvingAfterEndRequested;

        public static ProjectileHitPolicy DefaultSingleHit =>
            new ProjectileHitPolicy
            {
                Enabled = true,
                QueryIntervalTicks = 1,
                SameTargetPolicy = HitSameTargetPolicy.Once,
                MaxTotalHitCount = 1,
                EndOnFirstValidHit = true,
                StopResolvingAfterEndRequested = true,
            };

        public void ValidateOrThrow()
        {
            if (!Enabled) return;
            if (QueryIntervalTicks < 1)
                throw new InvalidOperationException(
                    "Projectile QueryIntervalTicks must be positive.");
            if (SameTargetPolicy == HitSameTargetPolicy.Cooldown &&
                SameTargetCooldownTicks < 1)
                throw new InvalidOperationException(
                    "Projectile same-target cooldown must be positive.");
            if (MaxTotalHitCount < 0 ||
                InitialPierceCount < 0 ||
                InitialBounceCount < 0)
                throw new InvalidOperationException(
                    "Projectile hit limits must not be negative.");
        }
    }
}
