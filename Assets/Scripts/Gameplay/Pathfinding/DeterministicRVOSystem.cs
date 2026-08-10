using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class DeterministicRVOSystem
    {
        private readonly RVOConfig _config;
        private readonly fp2[] _sampleDirections;
        private static readonly fp Epsilon = (fp)0.0001m;

        public DeterministicRVOSystem(RVOConfig config)
        {
            if (config.NeighborSearchRadius <= fp.zero ||
                config.MaxNeighbors <= 0 ||
                config.TimeHorizon <= fp.zero ||
                config.SampleCount <= 0)
            {
                throw new ArgumentException(
                    "RVO configuration values must be positive.",
                    nameof(config));
            }
            _config = config;
            _sampleDirections = PrecomputeSampleDirections(config.SampleCount);
        }

        public fp NeighborSearchRadius =>
            _config.NeighborSearchRadius;

        public int MaxNeighbors =>
            _config.MaxNeighbors;

        public RvoResult[] Step(RVOInput[] inputs)
        {
            int count = inputs.Length;
            var results = new RvoResult[count];
            var neighborLists = new List<RVOInput>[count];

            fp searchRadiusSq = _config.NeighborSearchRadius * _config.NeighborSearchRadius;
            for (int i = 0; i < count; i++)
            {
                neighborLists[i] = new List<RVOInput>();
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    fp2 delta = inputs[i].Position - inputs[j].Position;
                    if (fpmath.dot(delta, delta) < searchRadiusSq)
                    {
                        neighborLists[i].Add(inputs[j]);
                    }
                }
                neighborLists[i].Sort(
                    CompareInputsByUid);
                if (neighborLists[i].Count >
                    _config.MaxNeighbors)
                {
                    neighborLists[i].RemoveRange(
                        _config.MaxNeighbors,
                        neighborLists[i].Count -
                        _config.MaxNeighbors);
                }
            }

            for (int i = 0; i < count; i++)
                results[i] = SolveAvoidance(inputs[i], neighborLists[i]);

            return results;
        }

        public RvoResult Solve(
            in RVOInput input,
            IReadOnlyList<RVOInput> neighbors)
        {
            if (neighbors == null)
                throw new ArgumentNullException(
                    nameof(neighbors));
            return SolveAvoidance(input, neighbors);
        }

        private RvoResult SolveAvoidance(
            in RVOInput input,
            IReadOnlyList<RVOInput> neighbors)
        {
            if (input.DesiredVelocity.x == fp.zero && input.DesiredVelocity.y == fp.zero)
                return new RvoResult { UnitUid = input.SelfUid, FinalVelocity = fp2.zero };

            fp2 best = input.DesiredVelocity;
            fp bestPenalty = EvaluateVelocity(input, best, neighbors);

            for (int i = 0; i < _sampleDirections.Length; i++)
            {
                fp2 candidate = _sampleDirections[i] * input.MaxSpeed;
                fp penalty = EvaluateVelocity(input, candidate, neighbors);

                if (penalty < bestPenalty)
                {
                    best = candidate;
                    bestPenalty = penalty;
                }
                else if (penalty == bestPenalty)
                {
                    if (VelocityTieBreaker(candidate, best, input.DesiredVelocity) < fp.zero)
                        best = candidate;
                }
            }

            fp zeroPenalty = EvaluateVelocity(input, fp2.zero, neighbors);
            if (zeroPenalty < bestPenalty)
            {
                // RVO deadlock escape: a fully surrounded unit would
                // otherwise freeze at zero velocity forever (e.g. a minion
                // boxed in at the lane meeting point, unable to close the
                // last gap into attack range). Keep crawling in the desired
                // direction at reduced speed instead of stopping completely;
                // unit-vs-unit overlap remains soft and wall penetration is
                // corrected separately by the grid/wall resolver.
                fp2 crawl =
                    input.DesiredVelocity *
                    (fp)0.25m;
                return new RvoResult
                {
                    UnitUid = input.SelfUid,
                    FinalVelocity = crawl,
                };
            }

            return new RvoResult { UnitUid = input.SelfUid, FinalVelocity = best };
        }

        private fp EvaluateVelocity(
            in RVOInput input,
            fp2 candidate,
            IReadOnlyList<RVOInput> neighbors)
        {
            fp2 diff = candidate - input.DesiredVelocity;
            fp penalty = fpmath.dot(diff, diff);

            for (int i = 0; i < neighbors.Count; i++)
            {
                var nb = neighbors[i];
                fp2 relPos = input.Position - nb.Position;
                fp distSq = fpmath.dot(relPos, relPos);
                fp combinedR = input.Radius + nb.Radius;
                fp minDistSq = combinedR * combinedR;

                if (distSq < minDistSq)
                {
                    penalty += (fp)1000m;
                    continue;
                }

                fp2 relVel = candidate - nb.DesiredVelocity;
                fp relSpeedSq = fpmath.dot(relVel, relVel);
                if (relSpeedSq < Epsilon) continue;

                fp t = -fpmath.dot(relPos, relVel) / relSpeedSq;
                if (t <= fp.zero || t > _config.TimeHorizon) continue;

                fp2 closest = relPos + relVel * t;
                fp approachSq = fpmath.dot(closest, closest);
                if (approachSq < minDistSq)
                    penalty += (fp)100m / (t + Epsilon);
            }

            return penalty;
        }

        private static int CompareInputsByUid(
            RVOInput left,
            RVOInput right) =>
            left.SelfUid.CompareTo(right.SelfUid);

        private static fp VelocityTieBreaker(fp2 candidate, fp2 current, fp2 desired)
        {
            fp candDot = fpmath.dot(candidate, desired);
            fp currDot = fpmath.dot(current, desired);
            return currDot - candDot;
        }

        private static fp2[] PrecomputeSampleDirections(int count)
        {
            if (count <= 0)
            {
                count = 1;
            }
            var result = new fp2[count];
            fp twoPi =
                (fp)6.2831853071795864769m;
            int fullSpeedCount =
                count / 2;
            for (int i = 0; i < count; i++)
            {
                fp angle =
                    twoPi * (fp)i /
                    (fp)count;
                fp scale =
                    i < fullSpeedCount
                        ? fp.one
                        : (fp)0.5m;
                result[i] = new fp2(
                    fpmath.cos(angle) *
                        scale,
                    fpmath.sin(angle) *
                        scale);
            }
            return result;
        }
    }
}
