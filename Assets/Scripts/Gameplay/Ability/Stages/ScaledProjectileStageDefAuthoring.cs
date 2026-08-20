using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ScaledProjectileStageDefAuthoring :
        StageDefAuthoring
    {
        [Min(1)] [SerializeField] private int projectileDefId = 1;
        [Min(0f)] [SerializeField] private float spawnOffsetDistance;
        [SerializeField] private float[] baseDamageByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] attackDamageRatioByLevel =
            Array.Empty<float>();
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [Min(0f)] [SerializeField] private float minionDamageMultiplier;
        [Min(1)] [SerializeField] private int recipeId = 1;
        [SerializeField] private DurationAuthoring spawnDelay;
        [HideInInspector, Min(0)] [SerializeField] private int spawnDelayTicks;

        public override StageDef Bake(int tickRate = 30)
        {
            if (projectileDefId <= 0 || recipeId <= 0)
                throw new InvalidOperationException(
                    $"Scaled projectile stage '{DebugName}' has invalid IDs.");
            return new ScaledProjectileStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                ProjectileDefId = projectileDefId,
                SpawnOffsetDistance = (fp)spawnOffsetDistance,
                BaseDamageByLevel = BakeLevels(baseDamageByLevel),
                AttackDamageRatioByLevel =
                    BakeLevels(attackDamageRatioByLevel),
                DamageType = damageType,
                MinionDamageMultiplier = (fp)minionDamageMultiplier,
                RecipeId = recipeId,
                SpawnDelayTicks = BakeHelpers.BakeDuration(
                    spawnDelay, spawnDelayTicks, tickRate),
            };
        }

        private static AbilityLevelValue BakeLevels(float[] values)
        {
            if (values == null || values.Length == 0)
                return default;
            var result = new fp[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) ||
                    float.IsInfinity(values[i]) ||
                    values[i] < 0f)
                {
                    throw new InvalidOperationException(
                        $"Scaled projectile value {i} must be finite and nonnegative.");
                }
                result[i] = (fp)values[i];
            }
            return new AbilityLevelValue(result);
        }
    }
}
