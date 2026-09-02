using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Applies a Buff to the caster and one crowd-control definition to
    /// deterministic enemies in a circular area.
    /// </summary>
    public sealed class SelfBuffAreaControlStageDef : StageDef
    {
        public BuffConfigId SelfBuffConfigId;
        public fp Radius;
        public UnitTargetFilter TargetFilter;
        public CrowdControlId ControlId;
        public int ControlDurationTicks;
        public int ApplyDelayTicks;

        private readonly List<Unit> _results = new List<Unit>();
        private readonly List<Physics.PhysicsEntity2D> _grid =
            new List<Physics.PhysicsEntity2D>();

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            return IsConfigured(runtime)
                ? StageResult.Running
                : StageResult.Failed;
        }

        public override StageResult OnTick(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (session.StageElapsedTicks < ApplyDelayTicks)
                return StageResult.Running;
            if (!SelfBuffConfigId.IsValid ||
                runtime.World?.BuffDefinitions == null ||
                runtime.World.RangeQuery == null ||
                !runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) ||
                !runtime.World.BuffDefinitions.TryGet(
                    SelfBuffConfigId,
                    out BuffDefinition buff))
            {
                return StageResult.Failed;
            }

            caster.BuffHandler.Apply(
                SelfBuffConfigId,
                buff,
                BuffSource.Create(
                    caster.UnitUid,
                    BuffSourceType.Ability,
                    runtime.Definition.AbilityId));

            fp2 position = caster.PhysicsEntity.Transform2D.Position;
            var query = new RangeQueryDesc
            {
                Shape = Physics.PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    Radius),
                Transform = new Physics.PhysicsTransform2D(
                    position,
                    position,
                    caster.PhysicsEntity.Transform2D.Forward,
                    caster.PhysicsEntity.Transform2D.Right),
                TargetFilter = TargetFilter,
                SortMode = RangeQuerySortMode.Uid,
                MaxResult = 0,
            };
            runtime.World.RangeQuery.Query(
                query,
                caster.UnitUid,
                caster.TeamId,
                _results,
                _grid);
            _results.Sort(
                (left, right) => left.UnitUid.CompareTo(right.UnitUid));
            for (int i = 0; i < _results.Count; i++)
            {
                Unit target = _results[i];
                if (target == null ||
                    (target.CrowdControl == null &&
                     target.UnitKind != UnitKind.Structure))
                    continue;
                StructureEffectPolicy.TryApplyCrowdControl(
                    target,
                    caster.UnitUid,
                    ControlId,
                    ControlDurationTicks,
                    default);
            }
            return StageResult.Completed;
        }

        private bool IsConfigured(AbilityRuntime runtime)
        {
            return SelfBuffConfigId.IsValid &&
                runtime?.World?.BuffDefinitions != null &&
                runtime.World.RangeQuery != null;
        }
    }
}
