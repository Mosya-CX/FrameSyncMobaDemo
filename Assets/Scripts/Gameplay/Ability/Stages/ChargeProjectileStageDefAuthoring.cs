using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ChargeProjectileStageDefAuthoring :
        StageDefAuthoring
    {
        [Min(1)]
        [SerializeField] private int projectileDefId = 1;
        [Min(0f)]
        [SerializeField] private float spawnOffsetDistance = 1f;
        [Min(1)]
        [SerializeField] private int chargeRatioBlackboardKeyId = 1;
        [SerializeField] private int empoweredBlackboardKeyId;
        [SerializeField] private float[] minBaseDamageByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] maxBaseDamageByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] minAttackDamageRatioByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] maxAttackDamageRatioByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] minMissingHpRatioByLevel =
            Array.Empty<float>();
        [SerializeField] private float[] maxMissingHpRatioByLevel =
            Array.Empty<float>();
        [Min(0f)]
        [SerializeField] private float minRange;
        [Min(0f)]
        [SerializeField] private float maxRange;
        [Min(0f)]
        [SerializeField] private float falloffPerHitPercent;
        [Min(0f)]
        [SerializeField] private float minDamageRatio;
        [Min(1)]
        [SerializeField] private int recipeId = 100;

        public int ProjectileDefId => projectileDefId;
        public float SpawnOffsetDistance =>
            spawnOffsetDistance;
        public float MaxRange => maxRange;
        public float[] MinBaseDamageByLevel =>
            minBaseDamageByLevel;
        public float[] MaxBaseDamageByLevel =>
            maxBaseDamageByLevel;
        public float[] MinAttackDamageRatioByLevel =>
            minAttackDamageRatioByLevel;
        public float[] MaxAttackDamageRatioByLevel =>
            maxAttackDamageRatioByLevel;
        public float[] MinMissingHpRatioByLevel =>
            minMissingHpRatioByLevel;
        public float[] MaxMissingHpRatioByLevel =>
            maxMissingHpRatioByLevel;

        public override StageDef Bake(int tickRate = 30)
        {
            return new ChargeProjectileStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                ProjectileDefId = projectileDefId,
                SpawnOffsetDistance =
                    (fp)spawnOffsetDistance,
                ChargeRatioBlackboardKeyId =
                    chargeRatioBlackboardKeyId,
                EmpoweredBlackboardKeyId =
                    empoweredBlackboardKeyId,
                MinBaseDamageByLevel = BakeLevels(
                    minBaseDamageByLevel),
                MaxBaseDamageByLevel = BakeLevels(
                    maxBaseDamageByLevel),
                MinAttackDamageRatioByLevel = BakeLevels(
                    minAttackDamageRatioByLevel),
                MaxAttackDamageRatioByLevel = BakeLevels(
                    maxAttackDamageRatioByLevel),
                MinMissingHpRatioByLevel = BakeLevels(
                    minMissingHpRatioByLevel),
                MaxMissingHpRatioByLevel = BakeLevels(
                    maxMissingHpRatioByLevel),
                MinRange = (fp)minRange,
                MaxRange = (fp)maxRange,
                FalloffPerHitPercent =
                    (fp)falloffPerHitPercent,
                MinDamageRatio = (fp)minDamageRatio,
                RecipeId = recipeId,
            };
        }

        private static AbilityLevelValue BakeLevels(
            float[] values)
        {
            if (values == null ||
                values.Length == 0)
                return default;
            var converted =
                new Unity.Mathematics.FixedPoint.fp[
                    values.Length];
            for (int i = 0;
                 i < values.Length;
                 i++)
            {
                converted[i] =
                    (Unity.Mathematics.FixedPoint.fp)
                        values[i];
            }
            return new AbilityLevelValue(converted);
        }
    }
}
