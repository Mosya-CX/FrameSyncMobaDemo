using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class PhysicsGeometry2DTests
    {
        [Test]
        public void Facing_UsesFixedPointNormalizationAndClockwisePerpendicular()
        {
            fp2 input = Vector(3, 4);

            bool created = PhysicsGeometry2D.TryCreateFacing(input, out fp2 forward, out fp2 right);

            Assert.That(created, Is.True);
            fp2 expectedForward = fpmath.normalize(input);
            AssertVector(forward, expectedForward);
            Assert.That(right.x.RawValue, Is.EqualTo(forward.y.RawValue));
            Assert.That(right.y.RawValue, Is.EqualTo((-forward.x).RawValue));
        }

        [Test]
        public void BelowThresholdFacing_IsRejectedWithoutDivision()
        {
            fp2 input = new fp2(fp.FromRaw(1L), fp.zero);

            bool created = PhysicsGeometry2D.TryCreateFacing(input, out fp2 forward, out fp2 right);

            Assert.That(created, Is.False);
            AssertVector(forward, default);
            AssertVector(right, default);
        }

        [Test]
        public void PointBounds_UseOffsetAdjustedCurrentPoint()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(7, 8));
            PhysicsShape2D shape = PhysicsShape2D.CreatePoint(Vector(2, 3));

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(12, 23));
            AssertVector(bounds.Max, Vector(12, 23));
        }

        [Test]
        public void SweptPointBounds_UseFormalPrevPositionToCurrentWorldPoint()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(30, 5));
            PhysicsShape2D shape = PhysicsShape2D.CreatePoint(Vector(2, 3), true);

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(12, 5));
            AssertVector(bounds.Max, Vector(30, 23));
        }

        [Test]
        public void CircleBounds_ExpandOffsetAdjustedCenterByRadius()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(7, 8));
            PhysicsShape2D shape = PhysicsShape2D.CreateCircle(Vector(2, 3), Whole(4));

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(8, 19));
            AssertVector(bounds.Max, Vector(16, 27));
        }

        [Test]
        public void SweptCircleBounds_UnionExpandedSweepAndCurrentCircle()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(30, 5));
            PhysicsShape2D shape = PhysicsShape2D.CreateCircle(Vector(2, 3), Whole(4), true);

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(8, 1));
            AssertVector(bounds.Max, Vector(34, 27));
        }

        [Test]
        public void SegmentWorld_UsesOffsetCenterForwardLengthAndWidth()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(30, 5));
            PhysicsShape2D shape = PhysicsShape2D.CreateSegment(Vector(2, 3), Whole(6), Whole(4));

            PhysicsGeometry2D.GetSegmentWorld(
                transform,
                shape,
                out fp2 start,
                out fp2 end,
                out fp width);

            AssertVector(start, Vector(12, 20));
            AssertVector(end, Vector(12, 26));
            Assert.That(width.RawValue, Is.EqualTo(Whole(4).RawValue));
        }

        [Test]
        public void SegmentBounds_ExpandWorldEndpointsByHalfWidth()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(-100, -100));
            PhysicsShape2D shape = PhysicsShape2D.CreateSegment(Vector(2, 3), Whole(6), Whole(4), true);

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(10, 18));
            AssertVector(bounds.Max, Vector(14, 28));
        }

        [Test]
        public void RectWorld_UsesOffsetCenterAndStoredBasis()
        {
            PhysicsTransform2D transform = Transform(Vector(10, 20), Vector(30, 5));
            PhysicsShape2D shape = PhysicsShape2D.CreateRect(Vector(2, 3), Vector(4, 2));

            PhysicsGeometry2D.GetRectWorld(
                transform,
                shape,
                out fp2 center,
                out fp2 right,
                out fp2 forward,
                out fp2 halfExtents);

            AssertVector(center, Vector(12, 23));
            AssertVector(right, Vector(1, 0));
            AssertVector(forward, Vector(0, 1));
            AssertVector(halfExtents, Vector(4, 2));
        }

        [Test]
        public void OrientedRectBounds_UseAbsoluteRightAndForwardContributions()
        {
            var transform = new PhysicsTransform2D(
                Vector(10, 20),
                Vector(-100, -100),
                Vector(1, 0),
                Vector(0, -1));
            PhysicsShape2D shape = PhysicsShape2D.CreateRect(Vector(2, 3), Vector(4, 2), true);

            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(bounds.Min, Vector(11, 14));
            AssertVector(bounds.Max, Vector(15, 22));
        }

        [Test]
        public void DegenerateSegmentAndRect_ProduceExactPointBounds()
        {
            PhysicsTransform2D transform = Transform(Vector(5, 7), Vector(-20, 30));
            PhysicsShape2D segment = PhysicsShape2D.CreateSegment(Vector(2, 3), fp.zero, fp.zero, true);
            PhysicsShape2D rect = PhysicsShape2D.CreateRect(Vector(2, 3), default, true);

            PhysicsBounds2D segmentBounds = PhysicsGeometry2D.CalculateBounds(transform, segment);
            PhysicsBounds2D rectBounds = PhysicsGeometry2D.CalculateBounds(transform, rect);

            AssertVector(segmentBounds.Min, Vector(7, 10));
            AssertVector(segmentBounds.Max, Vector(7, 10));
            AssertVector(rectBounds.Min, Vector(7, 10));
            AssertVector(rectBounds.Max, Vector(7, 10));
        }

        [Test]
        public void IdenticalInputs_ProduceIdenticalRawBounds()
        {
            PhysicsTransform2D transform = Transform(Vector(-5, 12), Vector(8, -7));
            PhysicsShape2D shape = PhysicsShape2D.CreateRect(Vector(3, -2), Vector(6, 4), true);

            PhysicsBounds2D first = PhysicsGeometry2D.CalculateBounds(transform, shape);
            PhysicsBounds2D second = PhysicsGeometry2D.CalculateBounds(transform, shape);

            AssertVector(first.Min, second.Min);
            AssertVector(first.Max, second.Max);
        }

        private static PhysicsTransform2D Transform(fp2 position, fp2 previous)
        {
            return new PhysicsTransform2D(position, previous, Vector(0, 1), Vector(1, 0));
        }

        private static fp Whole(int value)
        {
            return fp.FromRaw((long)value << 32);
        }

        private static fp2 Vector(int x, int y)
        {
            return new fp2(Whole(x), Whole(y));
        }

        private static void AssertVector(fp2 actual, fp2 expected)
        {
            Assert.That(actual.x.RawValue, Is.EqualTo(expected.x.RawValue));
            Assert.That(actual.y.RawValue, Is.EqualTo(expected.y.RawValue));
        }
    }
}
