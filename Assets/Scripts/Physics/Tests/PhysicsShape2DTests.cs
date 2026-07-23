using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class PhysicsShape2DTests
    {
        [Test]
        public void PointFactory_PreservesFormalFieldsAndZerosUnusedFields()
        {
            fp2 offset = Vector(2, -3);

            PhysicsShape2D shape = PhysicsShape2D.CreatePoint(offset, true);

            Assert.That(shape.Kind, Is.EqualTo(PhysicsShapeKind.Point));
            AssertVector(shape.LocalOffset, offset);
            Assert.That(shape.Radius, Is.EqualTo(fp.zero));
            Assert.That(shape.Length, Is.EqualTo(fp.zero));
            Assert.That(shape.Width, Is.EqualTo(fp.zero));
            AssertVector(shape.HalfExtents, default);
            Assert.That(shape.SweepFromPrev, Is.True);
        }

        [Test]
        public void CircleFactory_PreservesFormalFieldsAndZerosUnusedFields()
        {
            fp2 offset = Vector(-1, 4);
            fp radius = Whole(3);

            PhysicsShape2D shape = PhysicsShape2D.CreateCircle(offset, radius, true);

            Assert.That(shape.Kind, Is.EqualTo(PhysicsShapeKind.Circle));
            AssertVector(shape.LocalOffset, offset);
            Assert.That(shape.Radius, Is.EqualTo(radius));
            Assert.That(shape.Length, Is.EqualTo(fp.zero));
            Assert.That(shape.Width, Is.EqualTo(fp.zero));
            AssertVector(shape.HalfExtents, default);
            Assert.That(shape.SweepFromPrev, Is.True);
        }

        [Test]
        public void NegativeCircleRadius_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsShape2D.CreateCircle(default, Whole(-1)));
        }

        [Test]
        public void SegmentFactory_PreservesFormalFieldsAndZerosUnusedFields()
        {
            fp2 offset = Vector(3, -4);
            fp length = Whole(8);
            fp width = Whole(2);

            PhysicsShape2D shape = PhysicsShape2D.CreateSegment(offset, length, width, true);

            Assert.That(shape.Kind, Is.EqualTo(PhysicsShapeKind.Segment));
            AssertVector(shape.LocalOffset, offset);
            Assert.That(shape.Radius, Is.EqualTo(fp.zero));
            Assert.That(shape.Length, Is.EqualTo(length));
            Assert.That(shape.Width, Is.EqualTo(width));
            AssertVector(shape.HalfExtents, default);
            Assert.That(shape.SweepFromPrev, Is.True);
        }

        [Test]
        public void RectFactory_PreservesFormalFieldsAndZerosUnusedFields()
        {
            fp2 offset = Vector(-2, 5);
            fp2 halfExtents = Vector(4, 7);

            PhysicsShape2D shape = PhysicsShape2D.CreateRect(offset, halfExtents, true);

            Assert.That(shape.Kind, Is.EqualTo(PhysicsShapeKind.Rect));
            AssertVector(shape.LocalOffset, offset);
            Assert.That(shape.Radius, Is.EqualTo(fp.zero));
            Assert.That(shape.Length, Is.EqualTo(fp.zero));
            Assert.That(shape.Width, Is.EqualTo(fp.zero));
            AssertVector(shape.HalfExtents, halfExtents);
            Assert.That(shape.SweepFromPrev, Is.True);
        }

        [Test]
        public void NegativeSegmentDimensions_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsShape2D.CreateSegment(default, Whole(-1), fp.zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsShape2D.CreateSegment(default, fp.zero, Whole(-1)));
        }

        [Test]
        public void NegativeRectHalfExtents_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsShape2D.CreateRect(default, Vector(-1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsShape2D.CreateRect(default, Vector(0, -1)));
        }

        [Test]
        public void SegmentAndRectUnusedFields_MustRemainZero()
        {
            var invalidSegment = new PhysicsShape2D(
                PhysicsShapeKind.Segment,
                default,
                Whole(1),
                Whole(2),
                Whole(1),
                default,
                false);
            var invalidRect = new PhysicsShape2D(
                PhysicsShapeKind.Rect,
                default,
                fp.zero,
                Whole(1),
                fp.zero,
                Vector(2, 3),
                false);

            Assert.Throws<ArgumentException>(() => invalidSegment.ValidateSupported());
            Assert.Throws<ArgumentException>(() => invalidRect.ValidateSupported());
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
