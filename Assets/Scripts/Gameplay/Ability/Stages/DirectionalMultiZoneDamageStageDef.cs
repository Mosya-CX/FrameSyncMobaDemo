using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public enum DirectionalZoneShape : byte
    {
        Rectangle = 0,
        Trapezoid = 1,
        OffsetCircle = 2,
    }

    /// <summary>
    /// Deterministic directional impact with a primary and an optional
    /// sweet-spot zone. The supported public Physics shapes provide the
    /// broad phase; exact rectangle/trapezoid/offset-circle checks happen in
    /// this stage in stable UnitUid order.
    /// </summary>
    public sealed class DirectionalMultiZoneDamageStageDef : StageDef
    {
        public DirectionalZoneShape Shape;
        public fp ForwardStart;
        public fp ForwardLength;
        public fp NearHalfWidth;
        public fp FarHalfWidth;
        public fp CircleForwardOffset;
        public fp CircleRadius;
        public fp SweetForwardStart;
        public fp SweetForwardEnd;
        public fp SweetCircleRadius;
        public AbilityLevelValue BaseDamageByAbilityLevel;
        public AbilityLevelValue AttackDamageRatioByAbilityLevel;
        public fp StageDamageMultiplier = fp.one;
        public fp SweetSpotDamageMultiplier = fp.one;
        public fp MonsterBaseDamageBonus;
        public fp[] MinionDamageMultiplierByUnitLevel;
        public DamageType DamageType;
        public UnitTargetFilter TargetFilter;
        public CrowdControlId SweetSpotControlId;
        public int SweetSpotControlDurationTicks;
        public int FixedPassiveHitReductionTicks;
        public int FixedPassiveSweetHitReductionTicks;
        public int RecipeId;
        public int SweetSpotRecipeId;
        public int VfxDefId;
        public int ImpactDelayTicks;

        private readonly List<Unit> _resultScratch = new List<Unit>();
        private readonly List<Physics.PhysicsEntity2D> _gridScratch =
            new List<Physics.PhysicsEntity2D>();

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (VfxDefId <= 0)
                return StageResult.Running;
            if (runtime.World == null ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    session.Aim.Direction,
                    out fp2 forward,
                    out _))
            {
                return StageResult.Failed;
            }
            SubmitVfx(
                runtime,
                caster,
                caster.PhysicsEntity.Transform2D.Position,
                forward);
            return StageResult.Running;
        }

        public override StageResult OnTick(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (session.StageElapsedTicks < ImpactDelayTicks)
                return StageResult.Running;
            return ExecuteImpact(session, runtime)
                ? StageResult.Completed
                : StageResult.Failed;
        }

        private bool ExecuteImpact(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (runtime.World?.RangeQuery == null ||
                runtime.World.CombatSystem == null ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                caster.PhysicsEntity == null ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    session.Aim.Direction,
                    out fp2 forward,
                    out fp2 right))
            {
                return false;
            }

            fp2 origin = caster.PhysicsEntity.Transform2D.Position;
            fp broadRadius = ResolveBroadRadius();
            var desc = new RangeQueryDesc
            {
                Shape = Physics.PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    broadRadius),
                Transform = new Physics.PhysicsTransform2D(
                    origin,
                    origin,
                    forward,
                    right),
                TargetFilter = TargetFilter,
                SortMode = RangeQuerySortMode.Uid,
                MaxResult = 0,
            };
            runtime.World.RangeQuery.Query(
                desc,
                runtime.CasterUnitUid,
                caster.TeamId,
                _resultScratch,
                _gridScratch);
            _resultScratch.Sort(
                (left, rightUnit) =>
                    left.UnitUid.CompareTo(rightUnit.UnitUid));

            fp attackDamage = caster.StatHandler.GetStat(
                StatId.AttackDamage);
            fp baseDamage = BaseDamageByAbilityLevel.Resolve(
                runtime.Level);
            fp attackRatio = AttackDamageRatioByAbilityLevel.Resolve(
                runtime.Level);

            for (int i = 0; i < _resultScratch.Count; i++)
            {
                Unit target = _resultScratch[i];
                if (target == null ||
                    target.LifeState != LifeState.Alive ||
                    target.UnitKind == UnitKind.Structure)
                {
                    continue;
                }

                fp targetRadius = target.PhysicsEntity.Shape.Kind ==
                    Physics.PhysicsShapeKind.Circle
                        ? target.PhysicsEntity.Shape.Radius
                        : fp.zero;
                fp2 delta =
                    target.PhysicsEntity.Transform2D.Position - origin;
                fp longitudinal = fpmath.dot(delta, forward);
                fp lateral = fpmath.abs(fpmath.dot(delta, right));
                if (!ContainsPrimary(
                        longitudinal,
                        lateral,
                        targetRadius))
                {
                    continue;
                }

                bool sweet = ContainsSweetSpot(
                    longitudinal,
                    lateral,
                    targetRadius);
                fp targetBaseDamage = baseDamage;
                if (target.UnitKind == UnitKind.Monster)
                    targetBaseDamage += MonsterBaseDamageBonus;
                fp damage =
                    (targetBaseDamage + attackDamage * attackRatio) *
                    StageDamageMultiplier;
                if (sweet)
                    damage *= SweetSpotDamageMultiplier;
                if (target.UnitKind == UnitKind.Minion)
                    damage *= ResolveMinionMultiplier(caster.Level);

                var request = new DamageRequest
                {
                    Header = CombatRequestHeader.Create(
                        caster.UnitUid,
                        target.UnitUid,
                        CombatSourceType.Ability,
                        runtime.Definition.AbilityId,
                        sweet ? SweetSpotRecipeId : RecipeId,
                        originActionId: BuildOriginActionId(
                            session,
                            runtime,
                            caster),
                        effectOrdinal:
                            CombatFairnessKey.ComposeEffectOrdinal(
                                StageDefId,
                                sweet ? 1 : 0)),
                    BaseDamage = damage,
                    DamageType = DamageType,
                };
                if (!runtime.World.CombatSystem.SubmitDamage(request))
                {
                    throw new DeterministicSimulationException(
                        $"Directional stage {StageDefId} damage was rejected.");
                }

                if (sweet && SweetSpotControlId.IsValid &&
                    SweetSpotControlDurationTicks > 0)
                {
                    target.CrowdControl?.Add(
                        SweetSpotControlId,
                        SweetSpotControlDurationTicks,
                        default);
                }

                if (target.UnitKind == UnitKind.Hero)
                {
                    caster.AbilityHandler?.ReduceFixedPassiveCooldown(
                        sweet
                            ? FixedPassiveSweetHitReductionTicks
                            : FixedPassiveHitReductionTicks);
                }
            }

            return true;
        }

        private fp ResolveBroadRadius()
        {
            if (Shape == DirectionalZoneShape.OffsetCircle)
                return fpmath.abs(CircleForwardOffset) + CircleRadius;
            fp far = fpmath.max(
                fpmath.abs(ForwardStart),
                fpmath.abs(ForwardStart + ForwardLength));
            fp halfWidth = fpmath.max(NearHalfWidth, FarHalfWidth);
            return fpmath.sqrt(far * far + halfWidth * halfWidth);
        }

        private bool ContainsPrimary(
            fp longitudinal,
            fp lateral,
            fp targetRadius)
        {
            if (Shape == DirectionalZoneShape.OffsetCircle)
            {
                fp x = longitudinal - CircleForwardOffset;
                fp radius = CircleRadius + targetRadius;
                return x * x + lateral * lateral <= radius * radius;
            }

            fp end = ForwardStart + ForwardLength;
            if (longitudinal < ForwardStart - targetRadius ||
                longitudinal > end + targetRadius)
            {
                return false;
            }
            fp progress = ForwardLength > fp.zero
                ? fpmath.clamp(
                    (longitudinal - ForwardStart) / ForwardLength,
                    fp.zero,
                    fp.one)
                : fp.zero;
            fp halfWidth = Shape == DirectionalZoneShape.Trapezoid
                ? NearHalfWidth + (FarHalfWidth - NearHalfWidth) * progress
                : NearHalfWidth;
            return lateral <= halfWidth + targetRadius;
        }

        private bool ContainsSweetSpot(
            fp longitudinal,
            fp lateral,
            fp targetRadius)
        {
            if (Shape == DirectionalZoneShape.OffsetCircle)
            {
                fp x = longitudinal - CircleForwardOffset;
                fp radius = SweetCircleRadius + targetRadius;
                return radius > fp.zero &&
                    x * x + lateral * lateral <= radius * radius;
            }
            if (SweetForwardEnd <= SweetForwardStart)
                return false;
            if (longitudinal < SweetForwardStart - targetRadius ||
                longitudinal > SweetForwardEnd + targetRadius)
            {
                return false;
            }
            return ContainsPrimary(longitudinal, lateral, targetRadius);
        }

        private fp ResolveMinionMultiplier(int unitLevel)
        {
            if (MinionDamageMultiplierByUnitLevel == null ||
                MinionDamageMultiplierByUnitLevel.Length == 0)
            {
                return fp.one;
            }
            int index = unitLevel <= 1 ? 0 : unitLevel - 1;
            if (index >= MinionDamageMultiplierByUnitLevel.Length)
                index = MinionDamageMultiplierByUnitLevel.Length - 1;
            return MinionDamageMultiplierByUnitLevel[index];
        }

        private void SubmitVfx(
            AbilityRuntime runtime,
            Unit caster,
            fp2 origin,
            fp2 forward)
        {
            if (VfxDefId <= 0)
                return;
            int tick = SimulationTickContext.Current.Tick;
            VisualEventOutput.SubmitVfx(
                new VfxEvent
                {
                    Id = new PresentationEventId
                    {
                        SourceLogicTick = tick,
                        SourceKind = PresentationSourceKind.Unit,
                        SourceRuntimeUid = caster.UnitUid,
                        EventSequence = (ushort)StageDefId,
                        EventKey = PresentationEventKeys.AbilityCast,
                    },
                    VfxDefId = VfxDefId,
                    WorldPosition = origin,
                    WorldDirection = forward,
                    AttachToUnit = caster.UnitUid,
                    DurationScale = ImpactDelayTicks > 0
                        ? (fp)ImpactDelayTicks /
                          (fp)runtime.World.TickRate
                        : fp.one,
                });
        }
    }
}
