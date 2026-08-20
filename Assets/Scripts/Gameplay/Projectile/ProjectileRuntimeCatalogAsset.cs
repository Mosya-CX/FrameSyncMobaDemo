using System;
using System.Collections.Generic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct ProjectileDamageAuthoring
    {
        [Min(0f)] public float BaseDamage;
        [Min(0f)] public float StatRatio;
        public DamageType DamageType;
        [Min(1)] public int RecipeId;
    }

    [Serializable]
    public struct ProjectileBuffAuthoring
    {
        [Min(1)] public int BuffConfigId;
        public DurationAuthoring Duration;
        [HideInInspector]
        [Min(1)] public int DurationTicks;
        public UnitKindMask TargetKinds;
    }

    [Serializable]
    public struct ProjectileCrowdControlAuthoring
    {
        [Min(1)] public int ControlId;
        public DurationAuthoring Duration;
        [HideInInspector]
        [Min(1)] public int DurationTicks;
    }

    [Serializable]
    public sealed class ProjectileDefinitionAuthoring
    {
        [Min(1)] public int DefId;
        [Min(1)] public int RuntimeEntityPrefabId;
        [Min(0f)] public float Speed;
        [Min(0f)] public float AccelerationPerSecond;
        [HideInInspector]
        public float Acceleration;
        public bool Homing;
        public DurationAuthoring MaxLifetime;
        [HideInInspector]
        [Min(1)] public int MaxLifetimeTicks = 30;
        [Min(0f)] public float HitRadius = 0.1f;
        public ProjectileTargetFilter TargetFilter =
            ProjectileTargetFilter.DefaultEnemy;
        public ProjectileHitPolicy HitPolicy =
            ProjectileHitPolicy.DefaultSingleHit;
        public ProjectileDamageAuthoring[] DamageEffects;
        public ProjectileBuffAuthoring[] BuffEffects;
        public ProjectileCrowdControlAuthoring[] CrowdControlEffects;

        public ProjectileDef BakeOrThrow(
            GlobalPrefabTable prefabTable,
            int tickRate = 30)
        {
            if (prefabTable == null)
                throw new InvalidOperationException(
                    "Projectile Bake requires GlobalPrefabTable.");
            GameObject prefab = prefabTable.GetRequiredPrefab(
                PrefabKind.Projectile,
                RuntimeEntityPrefabId);
            if (prefab.GetComponent<PhysicsEntity2D>() == null)
                throw new InvalidOperationException(
                    $"Projectile prefab {RuntimeEntityPrefabId} has no PhysicsEntity2D.");
            PhysicsEntity2DShapeAuthoring shapeAuthoring =
                prefab.GetComponent<PhysicsEntity2DShapeAuthoring>();
            if (shapeAuthoring == null)
                throw new InvalidOperationException(
                    $"Projectile prefab {RuntimeEntityPrefabId} has no PhysicsEntity2DShapeAuthoring.");
            shapeAuthoring.BakeOrThrow();
            ProjectileContainmentZoneAuthoring containmentAuthoring =
                prefab.GetComponent<ProjectileContainmentZoneAuthoring>();

            var definition = new ProjectileDef
            {
                DefId = DefId,
                RuntimeEntityPrefabId = RuntimeEntityPrefabId,
                Speed = (fp)Speed,
                Acceleration = (fp)(
                    AccelerationPerSecond != 0f
                        ? AccelerationPerSecond
                        : Acceleration * 30f),
                Homing = Homing,
                MaxLifetimeTicks = BakeDuration(
                    MaxLifetime,
                    MaxLifetimeTicks,
                    tickRate),
                HitRadius = (fp)HitRadius,
                TargetFilter = TargetFilter,
                HitPolicy = BakeHitPolicy(
                    HitPolicy,
                    tickRate),
                OnHitEffects = new ProjectileOnHitEffects
                {
                    DamageEffects = BakeDamageEffects(),
                    BuffEffects = BakeBuffEffects(tickRate),
                    CCEffects = BakeCrowdControlEffects(tickRate),
                },
                ContainmentZone = containmentAuthoring != null
                    ? containmentAuthoring.BakeOrThrow()
                    : default,
            };
            definition.ValidateOrThrow();
            return definition;
        }

        private ProjectileOnHitDamage[] BakeDamageEffects()
        {
            if (DamageEffects == null)
                return Array.Empty<ProjectileOnHitDamage>();
            var baked =
                new ProjectileOnHitDamage[DamageEffects.Length];
            for (int i = 0; i < baked.Length; i++)
            {
                baked[i] = new ProjectileOnHitDamage
                {
                    Amount = (fp)DamageEffects[i].BaseDamage,
                    DamageRatio = (fp)DamageEffects[i].StatRatio,
                    DamageType = DamageEffects[i].DamageType,
                    RecipeId = DamageEffects[i].RecipeId,
                };
            }
            return baked;
        }

        private ProjectileOnHitBuff[] BakeBuffEffects(int tickRate)
        {
            if (BuffEffects == null)
                return Array.Empty<ProjectileOnHitBuff>();
            var baked =
                new ProjectileOnHitBuff[BuffEffects.Length];
            for (int i = 0; i < baked.Length; i++)
            {
                baked[i] = new ProjectileOnHitBuff
                {
                    BuffId = new BuffConfigId(
                        BuffEffects[i].BuffConfigId),
                    DurationTicks =
                        BakeDuration(
                            BuffEffects[i].Duration,
                            BuffEffects[i].DurationTicks,
                            tickRate),
                    TargetKinds = BuffEffects[i].TargetKinds,
                };
            }
            return baked;
        }

        private ProjectileOnHitCC[]
            BakeCrowdControlEffects(int tickRate)
        {
            if (CrowdControlEffects == null)
                return Array.Empty<ProjectileOnHitCC>();
            var baked =
                new ProjectileOnHitCC[
                    CrowdControlEffects.Length];
            for (int i = 0; i < baked.Length; i++)
            {
                baked[i] = new ProjectileOnHitCC
                {
                    ControlId = new CrowdControlId(
                        CrowdControlEffects[i].ControlId),
                    DurationTicks =
                        BakeDuration(
                            CrowdControlEffects[i].Duration,
                            CrowdControlEffects[i].DurationTicks,
                            tickRate),
                };
            }
            return baked;
        }

        private static int BakeDuration(
            in DurationAuthoring duration,
            int legacyTicks,
            int tickRate)
        {
            return duration.IsAuthored
                ? duration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        legacyTicks,
                        tickRate);
        }

        private static ProjectileHitPolicy BakeHitPolicy(
            ProjectileHitPolicy policy,
            int tickRate)
        {
            policy.QueryIntervalTicks =
                policy.QueryInterval.IsAuthored
                    ? policy.QueryInterval.BakeTicks(tickRate)
                    : DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(
                            policy.QueryIntervalTicks,
                            tickRate);
            policy.SameTargetCooldownTicks =
                policy.SameTargetCooldown.IsAuthored
                    ? policy.SameTargetCooldown.BakeTicks(tickRate)
                    : DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(
                            policy.SameTargetCooldownTicks,
                            tickRate);
            return policy;
        }
    }

    [CreateAssetMenu(
        fileName = "ProjectileRuntimeCatalog",
        menuName =
            "FrameSyncMoba/Runtime/Projectile Runtime Catalog")]
    public sealed class ProjectileRuntimeCatalogAsset :
        ScriptableObject
    {
        [SerializeField]
        private List<ProjectileDefinitionAuthoring>
            definitions =
                new List<ProjectileDefinitionAuthoring>();

        public ProjectileDefRegistry BakeOrThrow(
            GlobalPrefabTable prefabTable,
            int tickRate = 30)
        {
            var registry = new ProjectileDefRegistry();
            for (int i = 0; i < definitions.Count; i++)
            {
                ProjectileDefinitionAuthoring authoring =
                    definitions[i];
                if (authoring == null)
                    throw new InvalidOperationException(
                        $"Projectile definition {i} is null.");
                registry.Register(
                    authoring.BakeOrThrow(
                        prefabTable,
                        tickRate));
            }
            return registry;
        }

        internal void ReplaceForTests(
            IEnumerable<ProjectileDefinitionAuthoring>
                values)
        {
            definitions.Clear();
            if (values != null)
                definitions.AddRange(values);
        }
    }
}
