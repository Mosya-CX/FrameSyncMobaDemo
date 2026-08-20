using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class DirectionalMultiZoneDamageStageDefAuthoring :
        StageDefAuthoring
    {
        [SerializeField] private DirectionalZoneShape shape;
        [SerializeField] private float forwardStart;
        [Min(0f)] [SerializeField] private float forwardLength;
        [Min(0f)] [SerializeField] private float nearHalfWidth;
        [Min(0f)] [SerializeField] private float farHalfWidth;
        [SerializeField] private float circleForwardOffset;
        [Min(0f)] [SerializeField] private float circleRadius;
        [SerializeField] private float sweetForwardStart;
        [SerializeField] private float sweetForwardEnd;
        [Min(0f)] [SerializeField] private float sweetCircleRadius;
        [SerializeField] private float[] baseDamageByAbilityLevel =
            Array.Empty<float>();
        [SerializeField] private float[] attackDamageRatioByAbilityLevel =
            Array.Empty<float>();
        [Min(0f)] [SerializeField] private float stageDamageMultiplier = 1f;
        [Min(0f)] [SerializeField] private float sweetSpotDamageMultiplier = 1f;
        [Min(0f)] [SerializeField] private float monsterBaseDamageBonus;
        [SerializeField] private float[] minionDamageMultiplierByUnitLevel =
            Array.Empty<float>();
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private UnitTargetFilter targetFilter =
            UnitTargetFilter.Default;
        [Min(0)] [SerializeField] private int sweetSpotControlId;
        [SerializeField] private DurationAuthoring sweetSpotControlDuration;
        [HideInInspector, Min(0)] [SerializeField] private int sweetSpotControlDurationTicks;
        [SerializeField] private DurationAuthoring fixedPassiveHitReduction;
        [HideInInspector, Min(0)] [SerializeField] private int fixedPassiveHitReductionTicks;
        [SerializeField] private DurationAuthoring fixedPassiveSweetHitReduction;
        [HideInInspector, Min(0)] [SerializeField] private int fixedPassiveSweetHitReductionTicks;
        [Min(1)] [SerializeField] private int recipeId = 1;
        [Min(1)] [SerializeField] private int sweetSpotRecipeId = 2;
        [Min(0)] [SerializeField] private int vfxDefId;
        [SerializeField] private DurationAuthoring impactDelay;
        [HideInInspector, Min(0)] [SerializeField] private int impactDelayTicks;

        public DirectionalZoneShape Shape => shape;
        public float ForwardStart => forwardStart;
        public float ForwardLength => forwardLength;
        public float NearHalfWidth => nearHalfWidth;
        public float FarHalfWidth => farHalfWidth;
        public float CircleForwardOffset => circleForwardOffset;
        public float CircleRadius => circleRadius;
        public float SweetForwardStart => sweetForwardStart;
        public float SweetForwardEnd => sweetForwardEnd;
        public float SweetCircleRadius => sweetCircleRadius;

        public override StageDef Bake(int tickRate = 30)
        {
            if (!Enum.IsDefined(typeof(DirectionalZoneShape), shape) ||
                forwardLength < 0f ||
                nearHalfWidth < 0f ||
                farHalfWidth < 0f ||
                circleRadius < 0f ||
                recipeId <= 0 ||
                sweetSpotRecipeId <= 0 ||
                targetFilter.UnitKindMask.IsEmpty ||
                targetFilter.LifeStateMask.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Directional stage '{DebugName}' has invalid geometry, recipes or target filter.");
            }

            return new DirectionalMultiZoneDamageStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                Shape = shape,
                ForwardStart = (fp)forwardStart,
                ForwardLength = (fp)forwardLength,
                NearHalfWidth = (fp)nearHalfWidth,
                FarHalfWidth = (fp)farHalfWidth,
                CircleForwardOffset = (fp)circleForwardOffset,
                CircleRadius = (fp)circleRadius,
                SweetForwardStart = (fp)sweetForwardStart,
                SweetForwardEnd = (fp)sweetForwardEnd,
                SweetCircleRadius = (fp)sweetCircleRadius,
                BaseDamageByAbilityLevel = BakeLevels(baseDamageByAbilityLevel),
                AttackDamageRatioByAbilityLevel =
                    BakeLevels(attackDamageRatioByAbilityLevel),
                StageDamageMultiplier = (fp)stageDamageMultiplier,
                SweetSpotDamageMultiplier = (fp)sweetSpotDamageMultiplier,
                MonsterBaseDamageBonus = (fp)monsterBaseDamageBonus,
                MinionDamageMultiplierByUnitLevel =
                    BakeArray(minionDamageMultiplierByUnitLevel),
                DamageType = damageType,
                TargetFilter = targetFilter,
                SweetSpotControlId = new CrowdControlId(
                    sweetSpotControlId),
                SweetSpotControlDurationTicks = BakeHelpers.BakeDuration(
                    sweetSpotControlDuration,
                    sweetSpotControlDurationTicks,
                    tickRate),
                FixedPassiveHitReductionTicks = BakeHelpers.BakeDuration(
                    fixedPassiveHitReduction,
                    fixedPassiveHitReductionTicks,
                    tickRate),
                FixedPassiveSweetHitReductionTicks =
                    BakeHelpers.BakeDuration(
                        fixedPassiveSweetHitReduction,
                        fixedPassiveSweetHitReductionTicks,
                        tickRate),
                RecipeId = recipeId,
                SweetSpotRecipeId = sweetSpotRecipeId,
                VfxDefId = vfxDefId,
                ImpactDelayTicks = BakeHelpers.BakeDuration(
                    impactDelay, impactDelayTicks, tickRate),
            };
        }

        private static AbilityLevelValue BakeLevels(float[] values)
        {
            return new AbilityLevelValue(BakeArray(values));
        }

        private static fp[] BakeArray(float[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<fp>();
            var result = new fp[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) ||
                    float.IsInfinity(values[i]) ||
                    values[i] < 0f)
                {
                    throw new InvalidOperationException(
                        $"Directional stage value {i} must be finite and nonnegative.");
                }
                result[i] = (fp)values[i];
            }
            return result;
        }
    }
}
