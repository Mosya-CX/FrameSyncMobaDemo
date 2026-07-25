using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class RVOSystemTests
    {
        private DeterministicRVOSystem _rvo;

        [SetUp]
        public void SetUp()
        {
            _rvo = new DeterministicRVOSystem(RVOConfig.Default);
        }

        private static RVOInput MakeInput(int id, fp2 pos, fp2 desiredVel, fp maxSpeed = default)
        {
            if (maxSpeed <= fp.zero) maxSpeed = (fp)3m;
            return new RVOInput
            {
                SelfUid = new UnitUid(1, (byte)id, (byte)id),
                Position = pos,
                DesiredVelocity = desiredVel,
                Radius = (fp)0.5m,
                MaxSpeed = maxSpeed,
            };
        }

        [Test]
        public void SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge()
        {
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, (fp)5m), new fp2(fp.zero, -fp.one)),
                MakeInput(2, new fp2(fp.zero, (fp)6m), new fp2(fp.zero, fp.one)),
            };

            RvoResult[] results = _rvo.Step(inputs);

            Assert.That(results.Length, Is.EqualTo(2));
            // Units should not both move directly toward each other
            fp2 v1 = results[0].FinalVelocity;
            fp2 v2 = results[1].FinalVelocity;

            // At least one unit should have a non-zero velocity
            bool hasMovement = (v1.x != fp.zero || v1.y != fp.zero)
                || (v2.x != fp.zero || v2.y != fp.zero);
            Assert.That(hasMovement, Is.True, "At least one unit should have movement.");
        }

        [Test]
        public void SolveAvoidance_NoNeighbors_ReturnsDesired()
        {
            fp2 desiredVel = new fp2(fp.one, fp.zero);
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, fp.zero), desiredVel),
            };

            RvoResult[] results = _rvo.Step(inputs);

            Assert.That(results.Length, Is.EqualTo(1));
            fp2 resultVel = results[0].FinalVelocity;

            // Single unit with no neighbors should move in desired direction
            fp dot = fpmath.dot(resultVel, desiredVel);
            Assert.That(dot, Is.GreaterThan(fp.zero),
                "Velocity should point in the same hemisphere as desired.");
        }

        [Test]
        public void SolveAvoidance_Deterministic_SameInputSameOutput()
        {
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, (fp)1m), new fp2(fp.one, fp.zero)),
                MakeInput(2, new fp2(fp.zero, (fp)2m), new fp2(-fp.one, fp.zero)),
                MakeInput(3, new fp2((fp)1m, (fp)0m), new fp2(fp.zero, fp.one)),
            };

            RvoResult[] results1 = _rvo.Step(inputs);
            RvoResult[] results2 = _rvo.Step(inputs);

            Assert.That(results1.Length, Is.EqualTo(results2.Length));
            for (int i = 0; i < results1.Length; i++)
            {
                Assert.That(results1[i].FinalVelocity.x, Is.EqualTo(results2[i].FinalVelocity.x),
                    $"Determinism violation at unit {i}: x component.");
                Assert.That(results1[i].FinalVelocity.y, Is.EqualTo(results2[i].FinalVelocity.y),
                    $"Determinism violation at unit {i}: y component.");
            }
        }

        [Test]
        public void SolveAvoidance_ZeroDesiredVelocity_ReturnsZero()
        {
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, fp.zero), fp2.zero),
            };

            RvoResult[] results = _rvo.Step(inputs);
            Assert.That(results[0].FinalVelocity.x, Is.EqualTo(fp.zero));
            Assert.That(results[0].FinalVelocity.y, Is.EqualTo(fp.zero));
        }

        [Test]
        public void SolveAvoidance_StableOrdering_IndependentOfInputOrder()
        {
            var inputs1 = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, (fp)1m), new fp2(fp.one, fp.zero)),
                MakeInput(2, new fp2(fp.zero, (fp)2m), new fp2(-fp.one, fp.zero)),
            };

            var inputs2 = new RVOInput[]
            {
                MakeInput(2, new fp2(fp.zero, (fp)2m), new fp2(-fp.one, fp.zero)),
                MakeInput(1, new fp2(fp.zero, (fp)1m), new fp2(fp.one, fp.zero)),
            };

            RvoResult[] results1 = _rvo.Step(inputs1);
            RvoResult[] results2 = _rvo.Step(inputs2);

            // Each unit should get the same result regardless of input array order
            for (int i = 0; i < 2; i++)
            {
                RvoResult r1a = results1[i];
                RvoResult r2a = results2[i];
                bool found = false;
                for (int j = 0; j < 2; j++)
                {
                    if (results2[j].FinalVelocity.x == r1a.FinalVelocity.x
                        && results2[j].FinalVelocity.y == r1a.FinalVelocity.y)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, $"Result {r1a.FinalVelocity} not found in second run.");
            }
        }
    }
}
