using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Deterministic.Tests
{
    public sealed class DeterministicRandomServiceTests
    {
        [Test]
        public void SameSeedAndCalls_ProduceIdenticalSequence()
        {
            var first = new DeterministicRandomService(0x12345678u);
            var second = new DeterministicRandomService(0x12345678u);

            for (int index = 0; index < 64; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        [Test]
        public void SameSeedAndMixedPrimitiveCalls_ProduceIdenticalResults()
        {
            var first = new DeterministicRandomService(0x31415926u);
            var second = new DeterministicRandomService(0x31415926u);

            Assert.That(first.NextInt(), Is.EqualTo(second.NextInt()));
            Assert.That(first.NextInt(-17, 29), Is.EqualTo(second.NextInt(-17, 29)));
            Assert.That(first.NextBool(), Is.EqualTo(second.NextBool()));
            Assert.That(
                first.Chance01(fp.FromRaw(1L << 31)),
                Is.EqualTo(second.Chance01(fp.FromRaw(1L << 31))));
            Assert.That(
                first.ChancePercent(fp.FromRaw(37L << 32)),
                Is.EqualTo(second.ChancePercent(fp.FromRaw(37L << 32))));
        }

        [Test]
        public void CaptureRestore_ReplaysSubsequentSequence()
        {
            var service = new DeterministicRandomService(0xCAFEBABEu);
            service.NextUInt();
            DeterministicRandomSnapshot snapshot = service.Capture();
            var expected = new uint[16];

            for (int index = 0; index < expected.Length; index++)
            {
                expected[index] = service.NextUInt();
            }

            service.Restore(snapshot);

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(service.NextUInt(), Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void CaptureRestore_ReplaysMixedPrimitiveSequence()
        {
            var service = new DeterministicRandomService(0x0BADF00Du);
            service.NextUInt();
            DeterministicRandomSnapshot snapshot = service.Capture();

            int expectedInt = service.NextInt();
            int expectedRangedInt = service.NextInt(-100, 250);
            bool expectedBool = service.NextBool();
            long expectedFpRaw = service.NextFp(fp.FromRaw(-2L << 32), fp.FromRaw(3L << 32)).RawValue;
            bool expectedChance = service.Chance01(fp.FromRaw(3L << 30));

            service.Restore(snapshot);

            Assert.That(service.NextInt(), Is.EqualTo(expectedInt));
            Assert.That(service.NextInt(-100, 250), Is.EqualTo(expectedRangedInt));
            Assert.That(service.NextBool(), Is.EqualTo(expectedBool));
            Assert.That(
                service.NextFp(fp.FromRaw(-2L << 32), fp.FromRaw(3L << 32)).RawValue,
                Is.EqualTo(expectedFpRaw));
            Assert.That(service.Chance01(fp.FromRaw(3L << 30)), Is.EqualTo(expectedChance));
        }

        [Test]
        public void ZeroSeed_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRandomService(0u));
        }

        [Test]
        public void ZeroRestoredState_IsRejectedWithoutChangingSequence()
        {
            var service = new DeterministicRandomService(123u);
            DeterministicRandomSnapshot before = service.Capture();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.Restore(new DeterministicRandomSnapshot(0u)));

            Assert.That(service.Capture(), Is.EqualTo(before));
        }

        [Test]
        public void NextFp01_UsesOneUnsignedFractionalDrawWithinUnitRange()
        {
            var service = new DeterministicRandomService(98765u);

            for (int index = 0; index < 64; index++)
            {
                fp value = service.NextFp01();
                Assert.That(value, Is.GreaterThanOrEqualTo(fp.zero));
                Assert.That(value, Is.LessThan(fp.one));
                Assert.That(value.RawValue, Is.InRange(0L, (long)uint.MaxValue));
            }
        }

        [Test]
        public void NextFp_StaysWithinRequestedRange()
        {
            var service = new DeterministicRandomService(112233u);
            fp minimum = fp.FromRaw(-3L << 32);
            fp maximum = fp.FromRaw(9L << 32);

            for (int index = 0; index < 64; index++)
            {
                fp value = service.NextFp(minimum, maximum);
                Assert.That(value, Is.GreaterThanOrEqualTo(minimum));
                Assert.That(value, Is.LessThan(maximum));
            }
        }

        [Test]
        public void NextInt_StaysWithinRequestedExclusiveRange()
        {
            var service = new DeterministicRandomService(998877u);

            for (int index = 0; index < 128; index++)
            {
                int value = service.NextInt(-31, 47);
                Assert.That(value, Is.GreaterThanOrEqualTo(-31));
                Assert.That(value, Is.LessThan(47));
            }
        }

        [Test]
        public void RangedNextInt_UsesOneUnsignedDrawAndCanonicalModuloMapping()
        {
            const uint Seed = 0x10203040u;
            const int Minimum = -17;
            const int Maximum = 29;
            var service = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);
            uint draw = baseline.NextUInt();
            uint range = (uint)((long)Maximum - Minimum);
            int expected = (int)((long)Minimum + (draw % range));

            Assert.That(service.NextInt(Minimum, Maximum), Is.EqualTo(expected));
            Assert.That(service.NextUInt(), Is.EqualTo(baseline.NextUInt()));
        }

        [Test]
        public void ClampedChance01_ConsumesExactlyOneDraw()
        {
            AssertChanceConsumesOneDraw(fp.zero);
            AssertChanceConsumesOneDraw(fp.one);
            AssertChanceConsumesOneDraw(fp.FromRaw(-1L));
            AssertChanceConsumesOneDraw(fp.FromRaw((1L << 32) + 1L));
        }

        [Test]
        public void ChancePercent_UsesTheSameOneDrawRule()
        {
            var chanceService = new DeterministicRandomService(4567u);
            var baselineService = new DeterministicRandomService(4567u);

            Assert.That(chanceService.ChancePercent(fp.FromRaw(100L << 32)), Is.True);
            baselineService.NextUInt();

            Assert.That(chanceService.NextUInt(), Is.EqualTo(baselineService.NextUInt()));
        }

        [Test]
        public void MidRangeChance_UsesTheDrawAndPercentUsesTheSameScale()
        {
            const uint Seed = 0x76543210u;
            fp probability = fp.FromRaw((37L << 32) / 100L);
            var chanceService = new DeterministicRandomService(Seed);
            var rollService = new DeterministicRandomService(Seed);
            var percentService = new DeterministicRandomService(Seed);

            bool expected = rollService.NextFp01() < probability;
            uint expectedNext = rollService.NextUInt();

            Assert.That(chanceService.Chance01(probability), Is.EqualTo(expected));
            Assert.That(
                percentService.ChancePercent(fp.FromRaw(37L << 32)),
                Is.EqualTo(expected));
            Assert.That(chanceService.NextUInt(), Is.EqualTo(expectedNext));
            Assert.That(percentService.NextUInt(), Is.EqualTo(expectedNext));
        }

        [Test]
        public void PickIndexAndPickOne_UseOneCanonicalRangedDraw()
        {
            const uint Seed = 0x55667788u;
            var indexService = new DeterministicRandomService(Seed);
            var indexBaseline = new DeterministicRandomService(Seed);
            int expectedIndex = indexBaseline.NextInt(0, 5);

            Assert.That(indexService.PickIndex(5), Is.EqualTo(expectedIndex));
            Assert.That(indexService.NextUInt(), Is.EqualTo(indexBaseline.NextUInt()));

            string[] values = { "zero", "one", "two", "three", "four" };
            var valueService = new DeterministicRandomService(Seed);
            var valueBaseline = new DeterministicRandomService(Seed);
            int expectedValueIndex = valueBaseline.NextInt(0, values.Length);

            Assert.That(valueService.PickOne(values), Is.EqualTo(values[expectedValueIndex]));
            Assert.That(valueService.NextUInt(), Is.EqualTo(valueBaseline.NextUInt()));
        }

        [Test]
        public void InvalidCollectionInputs_AreRejectedWithoutChangingState()
        {
            var service = new DeterministicRandomService(0x11224488u);
            DeterministicRandomSnapshot before = service.Capture();

            Assert.Throws<ArgumentOutOfRangeException>(() => service.PickIndex(0));
            Assert.Throws<ArgumentNullException>(() => service.PickOne<int>(null));
            Assert.Throws<ArgumentException>(() => service.PickOne(Array.Empty<int>()));
            Assert.Throws<ArgumentNullException>(() => service.ShuffleInPlace<int>(null));

            Assert.That(service.Capture(), Is.EqualTo(before));
        }

        [Test]
        public void ShuffleInPlace_IsDeterministicAndConsumesLengthMinusOneDraws()
        {
            const uint Seed = 0x88776655u;
            int[] first = { 0, 1, 2, 3, 4, 5 };
            int[] second = { 0, 1, 2, 3, 4, 5 };
            var firstService = new DeterministicRandomService(Seed);
            var secondService = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);

            firstService.ShuffleInPlace(first);
            secondService.ShuffleInPlace(second);

            for (int index = first.Length - 1; index > 0; index--)
            {
                baseline.NextInt(0, index + 1);
            }

            int[] sorted = (int[])first.Clone();
            Array.Sort(sorted);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(sorted, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(firstService.NextUInt(), Is.EqualTo(baseline.NextUInt()));
        }

        [Test]
        public void EmptyAndSingleValueShuffle_ConsumeNoDraw()
        {
            const uint Seed = 0x13572468u;
            var emptyService = new DeterministicRandomService(Seed);
            var singleService = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);
            int[] single = { 42 };

            emptyService.ShuffleInPlace(Array.Empty<int>());
            singleService.ShuffleInPlace(single);

            uint expectedNext = baseline.NextUInt();
            Assert.That(emptyService.NextUInt(), Is.EqualTo(expectedNext));
            Assert.That(singleService.NextUInt(), Is.EqualTo(expectedNext));
            Assert.That(single, Is.EqualTo(new[] { 42 }));
        }

        [Test]
        public void CaptureRestore_ReplaysCollectionOperations()
        {
            var service = new DeterministicRandomService(0xA1B2C3D4u);
            DeterministicRandomSnapshot snapshot = service.Capture();
            int[] expectedShuffle = { 10, 20, 30, 40, 50 };
            int expectedPick = service.PickOne(expectedShuffle);
            service.ShuffleInPlace(expectedShuffle);

            service.Restore(snapshot);
            int[] replayedShuffle = { 10, 20, 30, 40, 50 };
            int replayedPick = service.PickOne(replayedShuffle);
            service.ShuffleInPlace(replayedShuffle);

            Assert.That(replayedPick, Is.EqualTo(expectedPick));
            Assert.That(replayedShuffle, Is.EqualTo(expectedShuffle));
        }

        [Test]
        public void RandomDirection2D_UsesCanonicalAngleMappingAndOneDraw()
        {
            const uint Seed = 0xAABBCCDDu;
            var service = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);
            fp angleDraw = baseline.NextFp01();
            fp angle = angleDraw * fpmath.PI_TIMES_2;
            var expected = new fp2(fpmath.cos(angle), fpmath.sin(angle));

            fp2 actual = service.RandomDirection2D();

            AssertFp2(actual, expected);
            Assert.That(service.NextUInt(), Is.EqualTo(baseline.NextUInt()));
        }

        [Test]
        public void RandomPointOnCircle_UsesCanonicalMappingAndOneDraw()
        {
            const uint Seed = 0x10293847u;
            fp radius = Whole(7);
            var service = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);
            fp angleDraw = baseline.NextFp01();
            fp angle = angleDraw * fpmath.PI_TIMES_2;
            var expected = new fp2(fpmath.cos(angle), fpmath.sin(angle)) * radius;

            fp2 actual = service.RandomPointOnCircle(radius);

            AssertFp2(actual, expected);
            Assert.That(service.NextUInt(), Is.EqualTo(baseline.NextUInt()));
        }

        [Test]
        public void RandomPointInsideCircle_UsesAngleThenAreaUniformRadialDraw()
        {
            const uint Seed = 0x91827364u;
            fp radius = Whole(9);
            var service = new DeterministicRandomService(Seed);
            var baseline = new DeterministicRandomService(Seed);
            fp angleDraw = baseline.NextFp01();
            fp radialDraw = baseline.NextFp01();
            fp angle = angleDraw * fpmath.PI_TIMES_2;
            fp distance = fpmath.sqrt(radialDraw) * radius;
            var expected = new fp2(fpmath.cos(angle), fpmath.sin(angle)) * distance;

            fp2 actual = service.RandomPointInsideCircle(radius);

            AssertFp2(actual, expected);
            Assert.That(service.NextUInt(), Is.EqualTo(baseline.NextUInt()));
        }

        [Test]
        public void SameSeedAndGeometryCalls_ProduceRawIdenticalSequence()
        {
            var first = new DeterministicRandomService(0xCAFED00Du);
            var second = new DeterministicRandomService(0xCAFED00Du);

            for (int index = 0; index < 32; index++)
            {
                AssertFp2(first.RandomDirection2D(), second.RandomDirection2D());
                AssertFp2(first.RandomPointOnCircle(Whole(3)), second.RandomPointOnCircle(Whole(3)));
                AssertFp2(
                    first.RandomPointInsideCircle(Whole(5)),
                    second.RandomPointInsideCircle(Whole(5)));
            }
        }

        [Test]
        public void CaptureRestore_ReplaysMixedGeometryOperations()
        {
            var service = new DeterministicRandomService(0xDEADBEEFu);
            service.NextUInt();
            DeterministicRandomSnapshot snapshot = service.Capture();
            fp2 expectedDirection = service.RandomDirection2D();
            fp2 expectedOnCircle = service.RandomPointOnCircle(Whole(4));
            fp2 expectedInsideCircle = service.RandomPointInsideCircle(Whole(6));

            service.Restore(snapshot);

            AssertFp2(service.RandomDirection2D(), expectedDirection);
            AssertFp2(service.RandomPointOnCircle(Whole(4)), expectedOnCircle);
            AssertFp2(service.RandomPointInsideCircle(Whole(6)), expectedInsideCircle);
        }

        [Test]
        public void ZeroRadius_ReturnsZeroAndPreservesNormalDrawCounts()
        {
            const uint Seed = 0x1234ABCDu;
            var onCircle = new DeterministicRandomService(Seed);
            var onBaseline = new DeterministicRandomService(Seed);
            var insideCircle = new DeterministicRandomService(Seed);
            var insideBaseline = new DeterministicRandomService(Seed);

            AssertFp2(onCircle.RandomPointOnCircle(fp.zero), default);
            onBaseline.NextFp01();
            Assert.That(onCircle.NextUInt(), Is.EqualTo(onBaseline.NextUInt()));

            AssertFp2(insideCircle.RandomPointInsideCircle(fp.zero), default);
            insideBaseline.NextFp01();
            insideBaseline.NextFp01();
            Assert.That(insideCircle.NextUInt(), Is.EqualTo(insideBaseline.NextUInt()));
        }

        [Test]
        public void NegativeGeometryRadius_IsRejectedWithoutChangingState()
        {
            var service = new DeterministicRandomService(0x778899AAu);
            DeterministicRandomSnapshot before = service.Capture();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.RandomPointOnCircle(Whole(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.RandomPointInsideCircle(Whole(-1)));

            Assert.That(service.Capture(), Is.EqualTo(before));
        }

        [Test]
        public void RandomPointsInsideCircle_RemainWithinFixedPointRadius()
        {
            var service = new DeterministicRandomService(0x0F1E2D3Cu);
            fp radius = Whole(7);
            fp radiusSq = radius * radius;
            fp quantizationMargin = fp.FromRaw(1L << 20);

            for (int index = 0; index < 128; index++)
            {
                fp2 point = service.RandomPointInsideCircle(radius);
                fp lengthSq = fpmath.dot(point, point);

                Assert.That(lengthSq, Is.LessThanOrEqualTo(radiusSq + quantizationMargin));
            }
        }

        [Test]
        public void ExecutionMode_DoesNotChangeRandomResults()
        {
            uint[] authority = ObserveMixedSequence(ExecutionMode.ServerAuthority);
            uint[] prediction = ObserveMixedSequence(ExecutionMode.ClientPrediction);
            uint[] replay = ObserveMixedSequence(ExecutionMode.ClientReplay);

            Assert.That(prediction, Is.EqualTo(authority));
            Assert.That(replay, Is.EqualTo(authority));
        }

        [Test]
        public void InvalidExclusiveRanges_AreRejected()
        {
            var service = new DeterministicRandomService(999u);

            Assert.Throws<ArgumentOutOfRangeException>(() => service.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.NextFp(fp.one, fp.one));
        }

        private static void AssertChanceConsumesOneDraw(fp probability)
        {
            const uint Seed = 0xABCDEF01u;
            var chanceService = new DeterministicRandomService(Seed);
            var baselineService = new DeterministicRandomService(Seed);

            chanceService.Chance01(probability);
            baselineService.NextUInt();

            Assert.That(chanceService.NextUInt(), Is.EqualTo(baselineService.NextUInt()));
        }

        private static fp Whole(int value)
        {
            return fp.FromRaw((long)value << 32);
        }

        private static void AssertFp2(fp2 actual, fp2 expected)
        {
            Assert.That(actual.x.RawValue, Is.EqualTo(expected.x.RawValue));
            Assert.That(actual.y.RawValue, Is.EqualTo(expected.y.RawValue));
        }

        private static uint[] ObserveMixedSequence(ExecutionMode mode)
        {
            var controller = new SimulationTickContextController();

            controller.BeginTick(1234, mode);

            try
            {
                var service = new DeterministicRandomService(0x2468ACE1u);

                return new[]
                {
                    unchecked((uint)service.NextInt()),
                    unchecked((uint)service.NextInt(-20, 80)),
                    service.NextBool() ? 1u : 0u,
                    unchecked((uint)service.NextFp01().RawValue),
                    service.Chance01(fp.FromRaw(1L << 31)) ? 1u : 0u,
                    service.NextUInt(),
                };
            }
            finally
            {
                controller.EndTick();
            }
        }
    }
}
