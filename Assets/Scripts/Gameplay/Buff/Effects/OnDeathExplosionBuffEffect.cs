using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class OnDeathExplosionBuffEffect : BuffEffect
    {
        public fp ExplosionRadius = (fp)4;
        public fp ExplosionDamage = (fp)100;
        public DamageType DamageType = DamageType.Magic;
        public UnitTargetFilter TargetFilter = UnitTargetFilter.Default;

        public BuffStateSlotId TriggerSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = TriggerSlot,
                        Kind = BuffValueKind.Bool,
                    },
                };

        private readonly List<Unit> _resultScratch = new List<Unit>();
        private readonly List<Physics.PhysicsEntity2D> _gridScratch = new List<Physics.PhysicsEntity2D>();

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            runtime.Blackboard.WriteBool(
                TriggerSlot,
                false);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner) { }

        public override void OnUnitDeath(BuffRuntime runtime, Unit owner)
        {
            if (owner?.World?.CombatSystem == null || owner.World.RangeQuery == null)
                return;
            if (ExplosionDamage <= fp.zero || ExplosionRadius <= fp.zero)
                return;

            if (runtime.Blackboard.ReadBoolOrDefault(
                    TriggerSlot))
                return;
            runtime.Blackboard.WriteBool(
                TriggerSlot,
                true);

            fp2 center = owner.MovementHandler?.Position ?? fp2.zero;
            var desc = new RangeQueryDesc
            {
                Shape = Physics.PhysicsShape2D.CreateCircle(fp2.zero, ExplosionRadius),
                Transform = new Physics.PhysicsTransform2D(center, center, fp2.zero, fp2.zero),
                TargetFilter = TargetFilter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
                MaxResult = 0,
            };

            owner.World.RangeQuery.Query(
                desc,
                owner.UnitUid,
                owner.TeamId,
                _resultScratch,
                _gridScratch);

            for (int i = 0; i < _resultScratch.Count; i++)
            {
                Unit target = _resultScratch[i];
                if (target == null || target.LifeState != LifeState.Alive)
                    continue;

                var request = new DamageRequest
                {
                    Header = CombatRequestHeader.Create(
                        runtime.SourceUnitUid,
                        target.UnitUid,
                    CombatSourceType.Buff,
                    runtime.ConfigId.Value,
                    runtime.ConfigId.Value,
                    originActionId:
                        CombatActionIdentityFactory.CreateFromSource(
                            owner.World,
                            runtime.SourceUnitUid,
                            CombatSourceType.Buff,
                            runtime.ConfigId.Value,
                            SimulationTickContext.Current.Tick -
                                runtime.ElapsedTicks,
                            owner.GameplayParticipantId,
                            runtime.ConfigId.Value),
                    effectOrdinal:
                        CombatFairnessKey.ComposeEffectOrdinal(
                            runtime.ConfigId.Value,
                            0)),
                    BaseDamage = ExplosionDamage,
                    DamageType = DamageType,
                };
                owner.World.CombatSystem.SubmitDamage(request);
            }
        }
    }
}
