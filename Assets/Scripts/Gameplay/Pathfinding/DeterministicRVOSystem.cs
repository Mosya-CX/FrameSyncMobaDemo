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
            _config = config;
            _sampleDirections = PrecomputeSampleDirections(config.SampleCount);
        }

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
                        if (neighborLists[i].Count >= _config.MaxNeighbors)
                            break;
                    }
                }
            }

            for (int i = 0; i < count; i++)
                results[i] = SolveAvoidance(inputs[i], neighborLists[i]);

            return results;
        }

        private RvoResult SolveAvoidance(in RVOInput input, List<RVOInput> neighbors)
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
                return new RvoResult { UnitUid = input.SelfUid, FinalVelocity = fp2.zero };

            return new RvoResult { UnitUid = input.SelfUid, FinalVelocity = best };
        }

        private fp EvaluateVelocity(in RVOInput input, fp2 candidate, List<RVOInput> neighbors)
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

        private static fp VelocityTieBreaker(fp2 candidate, fp2 current, fp2 desired)
        {
            fp candDot = fpmath.dot(candidate, desired);
            fp currDot = fpmath.dot(current, desired);
            return currDot - candDot;
        }

        private static fp2[] PrecomputeSampleDirections(int count)
        {
            var dirs = new fp2[]
            {
                new fp2(fp.one, fp.zero), new fp2(-fp.one, fp.zero),
                new fp2(fp.zero, fp.one), new fp2(fp.zero, -fp.one),
                new fp2((fp)0.7071m, (fp)0.7071m), new fp2(-(fp)0.7071m, (fp)0.7071m),
                new fp2((fp)0.7071m, -(fp)0.7071m), new fp2(-(fp)0.7071m, -(fp)0.7071m),
                new fp2((fp)0.5m, fp.zero), new fp2(-(fp)0.5m, fp.zero),
                new fp2(fp.zero, (fp)0.5m), new fp2(fp.zero, -(fp)0.5m),
                new fp2((fp)0.3536m, (fp)0.3536m), new fp2(-(fp)0.3536m, (fp)0.3536m),
                new fp2((fp)0.3536m, -(fp)0.3536m), new fp2(-(fp)0.3536m, -(fp)0.3536m),
            };

            int dirCount = System.Math.Min(count, dirs.Length);
            var result = new fp2[dirCount];
            Array.Copy(dirs, result, dirCount);
            return result;
        }
    }
}
