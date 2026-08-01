using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class RvoOrchestrator
    {
        private readonly List<RVOInput> _inputs =
            new List<RVOInput>(64);
        private readonly List<PhysicsEntity2D>
            _neighborEntities =
                new List<PhysicsEntity2D>(32);
        private readonly List<RVOInput> _neighbors =
            new List<RVOInput>(16);
        private readonly Dictionary<UnitUid, int>
            _inputIndexByUid =
                new Dictionary<UnitUid, int>(64);

        public void Step(
            DeterministicRVOSystem rvoSystem,
            PhysicsWorld physicsWorld,
            IReadOnlyList<Unit> units,
            IReadOnlyList<LocomotionResult>
                locomotionResults)
        {
            if (rvoSystem == null ||
                physicsWorld?.RvoGrid == null ||
                units == null ||
                locomotionResults == null)
            {
                return;
            }
            if (units.Count !=
                locomotionResults.Count)
            {
                throw new DeterministicSimulationException(
                    "RVO requires one locomotion result per stable Unit.");
            }

            GatherInputs(
                units,
                locomotionResults);
            for (int i = 0;
                 i < _inputs.Count;
                 i++)
            {
                LocomotionResult locomotion =
                    locomotionResults[i];
                if (!locomotion.HasMovement ||
                    !locomotion.AllowRVO)
                {
                    continue;
                }

                Unit unit = units[i];
                PhysicsEntity2D entity =
                    unit.PhysicsEntity;
                CollectNeighbors(
                    physicsWorld.RvoGrid,
                    entity,
                    rvoSystem);
                RvoResult result =
                    rvoSystem.Solve(
                        _inputs[i],
                        _neighbors);
                unit.MovementHandler
                    ?.ApplyRvoResult(result);
            }
        }

        private void GatherInputs(
            IReadOnlyList<Unit> units,
            IReadOnlyList<LocomotionResult>
                locomotionResults)
        {
            _inputs.Clear();
            _inputIndexByUid.Clear();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                Unit unit = units[i] ??
                    throw new DeterministicSimulationException(
                        "Stable Unit list contains a null entry.");
                LocomotionResult locomotion =
                    locomotionResults[i];
                if (locomotion.UnitUid !=
                    unit.UnitUid)
                {
                    throw new DeterministicSimulationException(
                        "Locomotion result order does not match stable Unit order.");
                }
                PhysicsEntity2D entity =
                    unit.PhysicsEntity ??
                    throw new DeterministicSimulationException(
                        $"Unit {unit.UnitUid} has no PhysicsEntity2D.");

                fp2 desiredVelocity =
                    locomotion.HasMovement
                        ? locomotion.DesiredDirection *
                          locomotion.DesiredSpeed
                        : fp2.zero;
                fp maxSpeed =
                    locomotion.HasMovement
                        ? locomotion.DesiredSpeed
                        : unit.MovementHandler.LogicMoveSpeed;
                var input = new RVOInput
                {
                    SelfUid = unit.UnitUid,
                    Position =
                        entity.Transform2D.Position,
                    DesiredVelocity =
                        desiredVelocity,
                    Radius = entity.Shape.Radius,
                    MaxSpeed = maxSpeed,
                };
                _inputIndexByUid.Add(
                    input.SelfUid,
                    _inputs.Count);
                _inputs.Add(input);
            }
        }

        private void CollectNeighbors(
            PhysicsSpatialGrid2D grid,
            PhysicsEntity2D entity,
            DeterministicRVOSystem rvoSystem)
        {
            fp expand =
                rvoSystem.NeighborSearchRadius;
            PhysicsBounds2D bounds =
                entity.Bounds;
            var queryBounds =
                new PhysicsBounds2D(
                    new fp2(
                        bounds.Min.x - expand,
                        bounds.Min.y - expand),
                    new fp2(
                        bounds.Max.x + expand,
                        bounds.Max.y + expand));

            grid.CollectCandidates(
                queryBounds,
                _neighborEntities);
            _neighbors.Clear();
            for (int i = 0;
                 i < _neighborEntities.Count;
                 i++)
            {
                PhysicsEntity2D candidate =
                    _neighborEntities[i];
                if (candidate == entity ||
                    !(candidate.QueryInfo.Owner is
                        Unit other) ||
                    !_inputIndexByUid.TryGetValue(
                        other.UnitUid,
                        out int inputIndex))
                {
                    continue;
                }

                fp2 delta =
                    _inputs[inputIndex].Position -
                    entity.Transform2D.Position;
                fp searchRadius =
                    rvoSystem.NeighborSearchRadius;
                if (fpmath.lengthsq(delta) >
                    searchRadius * searchRadius)
                {
                    continue;
                }
                _neighbors.Add(
                    _inputs[inputIndex]);
                if (_neighbors.Count >=
                    rvoSystem.MaxNeighbors)
                {
                    break;
                }
            }
        }
    }
}
