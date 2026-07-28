using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: queries a circular area around the aim point and submits
    /// damage to all matching units via CombatSystem.
    /// </summary>
    public sealed class AreaDamageStageDef : StageDef
    {
        public fp Radius;
        public fp BaseDamage;
        public DamageType DamageType;
        public UnitTargetFilter TargetFilter;

        private readonly List<Unit> _resultScratch = new List<Unit>();
        private readonly List<Physics.PhysicsEntity2D> _gridScratch = new List<Physics.PhysicsEntity2D>();

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World == null || runtime.World.RangeQuery == null ||
                runtime.World.CombatSystem == null)
                return StageResult.Failed;

            fp2 center = session.Aim.TargetPoint;
            var desc = new RangeQueryDesc
            {
                Shape = Physics.PhysicsShape2D.CreateCircle(fp2.zero, Radius),
                Transform = new Physics.PhysicsTransform2D(center, center, fp2.zero, fp2.zero),
                TargetFilter = TargetFilter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
                MaxResult = 0,
            };

            runtime.World.RangeQuery.Query(
                desc,
                runtime.CasterUnitUid,
                default,
                _resultScratch,
                _gridScratch);

            for (int i = 0; i < _resultScratch.Count; i++)
            {
                Unit target = _resultScratch[i];
                if (target == null || target.LifeState != LifeState.Alive)
                    continue;

                var damageReq = new DamageRequest
                {
                    Header = CombatRequestHeader.Create(
                        runtime.CasterUnitUid,
                        target.UnitUid,
                        CombatSourceType.Ability,
                        runtime.Definition?.AbilityId ?? 0,
                        runtime.Definition?.AbilityId ?? 0),
                    BaseDamage = BaseDamage,
                    DamageType = DamageType,
                };
                runtime.World.CombatSystem.SubmitDamage(damageReq);
            }

            return StageResult.Completed;
        }
    }
}
