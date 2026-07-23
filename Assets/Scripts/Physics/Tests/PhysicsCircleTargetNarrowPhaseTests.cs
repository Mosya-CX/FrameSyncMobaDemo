using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class PhysicsCircleTargetNarrowPhaseTests
    {
        private static readonly fp2 Origin = fp2.zero;
        private static readonly fp Radius = (fp)5m;
        private static readonly fp RadiusSq = Radius * Radius;

        #region ClosestPointOnSegment

        [Test]
        public void ClosestPoint_DegenerateSegment_ReturnsStart()
        {
            fp2 start = new fp2(3, 7);
            fp2 result = PhysicsGeometry2D.ClosestPointOnSegment(Origin, start, start);

            Assert.That(result, Is.EqualTo(start));
        }

        [Test]
        public void ClosestPoint_ProjectionBeforeStart_ClampsToStart()
        {
            fp2 start = new fp2((fp)10m, 0);
            fp2 end = new fp2((fp)20m, 0);
            fp2 result = PhysicsGeometry2D.ClosestPointOnSegment(Origin, start, end);

            Assert.That(result, Is.EqualTo(start));
        }

        [Test]
        public void ClosestPoint_ProjectionAfterEnd_ClampsToEnd()
        {
            fp2 start = new fp2((fp)10m, 0);
            fp2 end = new fp2((fp)20m, 0);
            fp2 query = new fp2((fp)30m, 0);
            fp2 result = PhysicsGeometry2D.ClosestPointOnSegment(query, start, end);

            Assert.That(result, Is.EqualTo(end));
        }

        [Test]
        public void ClosestPoint_ProjectionInsideSegment_ReturnsProjectedPoint()
        {
            fp2 start = new fp2(0, 0);
            fp2 end = new fp2((fp)10m, 0);
            fp2 query = new fp2((fp)5m, (fp)3m);
            fp2 result = PhysicsGeometry2D.ClosestPointOnSegment(query, start, end);

            Assert.That(result, Is.EqualTo(new fp2((fp)5m, 0)));
        }

        #endregion

        #region PointOverlapsCircle

        [Test]
        public void Point_StrictlyInsideCircle_ReturnsTrue()
        {
            fp2 point = new fp2((fp)3m, 0);

            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius), Is.True);
        }

        [Test]
        public void Point_StrictlyOutsideCircle_ReturnsFalse()
        {
            fp2 point = new fp2((fp)6m, 0);

            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius), Is.False);
        }

        [Test]
        public void Point_OnCircleBoundary_TangentReturnsTrue()
        {
            fp2 point = new fp2(Radius, 0);

            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius), Is.True);
        }

        [Test]
        public void Point_AtCircleCenter_ReturnsTrue()
        {
            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(Origin, Origin, Radius), Is.True);
        }

        [Test]
        public void PointOverlapsCircle_ZeroRadius_OnlyCenterOverlaps()
        {
            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(Origin, Origin, fp.zero), Is.True);
            Assert.That(PhysicsGeometry2D.PointOverlapsCircle(new fp2((fp)0m, (fp)1m), Origin, fp.zero), Is.False);
        }

        [Test]
        public void PointOverlapsCircle_NegativeRadius_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsGeometry2D.PointOverlapsCircle(Origin, Origin, (fp)(-1m)));
        }

        #endregion

        #region SweptPointOverlapsCircle

        [Test]
        public void SweptPoint_CrossesCircle_ReturnsTrue()
        {
            fp2 prev = new fp2((fp)(-10m), 0);
            fp2 curr = new fp2((fp)10m, 0);

            Assert.That(PhysicsGeometry2D.SweptPointOverlapsCircle(prev, curr, Origin, Radius), Is.True);
        }

        [Test]
        public void SweptPoint_MissesCircle_ReturnsFalse()
        {
            fp2 prev = new fp2((fp)(-10m), (fp)10m);
            fp2 curr = new fp2((fp)10m, (fp)10m);

            Assert.That(PhysicsGeometry2D.SweptPointOverlapsCircle(prev, curr, Origin, Radius), Is.False);
        }

        [Test]
        public void SweptPoint_TangentToCircle_ReturnsTrue()
        {
            fp2 prev = new fp2((fp)(-10m), Radius);
            fp2 curr = new fp2((fp)10m, Radius);

            Assert.That(PhysicsGeometry2D.SweptPointOverlapsCircle(prev, curr, Origin, Radius), Is.True);
        }

        [Test]
        public void SweptPoint_ZeroLength_EqualsPointBehavior()
        {
            fp2 pos = new fp2((fp)3m, 0);

            bool swept = PhysicsGeometry2D.SweptPointOverlapsCircle(pos, pos, Origin, Radius);
            bool point = PhysicsGeometry2D.PointOverlapsCircle(pos, Origin, Radius);

            Assert.That(swept, Is.EqualTo(point));
        }

        #endregion

        #region CircleOverlapsCircle

        [Test]
        public void CircleOverlaps_OverlappingCircles_ReturnsTrue()
        {
            Assert.That(
                PhysicsGeometry2D.CircleOverlapsCircle(new fp2((fp)3m, 0), (fp)3m, Origin, (fp)3m),
                Is.True);
        }

        [Test]
        public void CircleOverlaps_NonOverlappingCircles_ReturnsFalse()
        {
            Assert.That(
                PhysicsGeometry2D.CircleOverlapsCircle(new fp2((fp)20m, 0), (fp)3m, Origin, (fp)3m),
                Is.False);
        }

        [Test]
        public void CircleOverlaps_TangentCircles_ReturnsTrue()
        {
            Assert.That(
                PhysicsGeometry2D.CircleOverlapsCircle(new fp2((fp)10m, 0), (fp)5m, Origin, (fp)5m),
                Is.True);
        }

        [Test]
        public void CircleOverlaps_ZeroRadiusCircles_OnlyCentersOverlap()
        {
            Assert.That(
                PhysicsGeometry2D.CircleOverlapsCircle(Origin, fp.zero, Origin, fp.zero),
                Is.True);
            Assert.That(
                PhysicsGeometry2D.CircleOverlapsCircle(new fp2((fp)1m, 0), fp.zero, Origin, fp.zero),
                Is.False);
        }

        [Test]
        public void CircleOverlaps_NegativeRadius_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsGeometry2D.CircleOverlapsCircle(Origin, (fp)(-1m), Origin, Radius));
        }

        #endregion

        #region SegmentOverlapsCircle

        [Test]
        public void Segment_PassesThroughCircle_ReturnsTrue()
        {
            fp2 start = new fp2((fp)(-10m), 0);
            fp2 end = new fp2((fp)10m, 0);

            Assert.That(PhysicsGeometry2D.SegmentOverlapsCircle(start, end, fp.zero, Origin, Radius), Is.True);
        }

        [Test]
        public void Segment_MissesCircle_ReturnsFalse()
        {
            fp2 start = new fp2((fp)(-10m), (fp)20m);
            fp2 end = new fp2((fp)10m, (fp)20m);

            Assert.That(PhysicsGeometry2D.SegmentOverlapsCircle(start, end, fp.zero, Origin, Radius), Is.False);
        }

        [Test]
        public void Segment_WidthParticipatesInCombinedRadius()
        {
            fp2 start = new fp2((fp)(-10m), (fp)8m);
            fp2 end = new fp2((fp)10m, (fp)8m);

            // Without width: distance from center to segment = 8, radius = 5 -> miss
            Assert.That(PhysicsGeometry2D.SegmentOverlapsCircle(start, end, fp.zero, Origin, Radius), Is.False);
            // With width = 6: combined radius = 5 + 3 = 8, distance = 8 -> tangent hit
            Assert.That(PhysicsGeometry2D.SegmentOverlapsCircle(start, end, (fp)6m, Origin, Radius), Is.True);
        }

        [Test]
        public void Segment_TangentToCircle_ReturnsTrue()
        {
            fp2 start = new fp2((fp)(-10m), Radius);
            fp2 end = new fp2((fp)10m, Radius);

            Assert.That(PhysicsGeometry2D.SegmentOverlapsCircle(start, end, fp.zero, Origin, Radius), Is.True);
        }

        [Test]
        public void SegmentOverlaps_NegativeWidth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsGeometry2D.SegmentOverlapsCircle(
                    new fp2(0, 0), new fp2((fp)10m, 0), (fp)(-1m), Origin, Radius));
        }

        #endregion

        #region RectOverlapsCircle

        [Test]
        public void Rect_CircleInside_ReturnsTrue()
        {
            // Rect centered at origin, half extents (10,10), circle at origin radius 1
            Assert.That(
                PhysicsGeometry2D.RectOverlapsCircle(
                    Origin, new fp2((fp)1m, 0), new fp2(0, (fp)1m),
                    new fp2((fp)10m, (fp)10m), Origin, (fp)1m),
                Is.True);
        }

        [Test]
        public void Rect_CircleOutside_ReturnsFalse()
        {
            // Rect half extents (2,2), circle center at (10,0) radius 1
            Assert.That(
                PhysicsGeometry2D.RectOverlapsCircle(
                    Origin, new fp2((fp)1m, 0), new fp2(0, (fp)1m),
                    new fp2((fp)2m, (fp)2m), new fp2((fp)10m, 0), (fp)1m),
                Is.False);
        }

        [Test]
        public void Rect_CircleTangentToEdge_ReturnsTrue()
        {
            // Rect half extents (5,5), circle center at (6,0) radius 1 -> distance from rect edge = 1 = radius
            Assert.That(
                PhysicsGeometry2D.RectOverlapsCircle(
                    Origin, new fp2((fp)1m, 0), new fp2(0, (fp)1m),
                    new fp2((fp)5m, (fp)5m), new fp2((fp)6m, 0), (fp)1m),
                Is.True);
        }

        [Test]
        public void Rect_Rotated45Degrees_CorrectOverlap()
        {
            // Rect at origin, rotated 45 degrees (right = (0.707, 0.707), forward = (-0.707, 0.707))
            // half extents (5,5). Circle at (7,0) should overlap the rotated corner.
            fp sqrt2half = (fp)0.70710678m;
            fp2 right = new fp2(sqrt2half, sqrt2half);
            fp2 forward = new fp2(-sqrt2half, sqrt2half);

            Assert.That(
                PhysicsGeometry2D.RectOverlapsCircle(
                    Origin, right, forward,
                    new fp2((fp)5m, (fp)5m), new fp2((fp)7m, 0), (fp)2m),
                Is.True);
        }

        [Test]
        public void Rect_TranslatedEquivalent_ReturnsSameResult()
        {
            fp2 offset = new fp2((fp)100m, (fp)50m);
            fp2 right = new fp2((fp)1m, 0);
            fp2 forward = new fp2(0, (fp)1m);
            fp2 halfExtents = new fp2((fp)3m, (fp)3m);

            bool atOrigin = PhysicsGeometry2D.RectOverlapsCircle(
                Origin, right, forward, halfExtents, new fp2((fp)5m, 0), (fp)1m);
            bool translated = PhysicsGeometry2D.RectOverlapsCircle(
                offset, right, forward, halfExtents, offset + new fp2((fp)5m, 0), (fp)1m);

            Assert.That(translated, Is.EqualTo(atOrigin));
        }

        [Test]
        public void RectOverlaps_NegativeHalfExtent_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhysicsGeometry2D.RectOverlapsCircle(
                    Origin, new fp2((fp)1m, 0), new fp2(0, (fp)1m),
                    new fp2((fp)(-1m), (fp)1m), Origin, Radius));
        }

        #endregion

        #region Repeatability

        [Test]
        public void RepeatedCalls_ProduceIdenticalResults()
        {
            fp2 point = new fp2((fp)3m, (fp)4m);

            bool first = PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius);
            bool second = PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius);
            bool third = PhysicsGeometry2D.PointOverlapsCircle(point, Origin, Radius);

            Assert.That(new[] { first, second, third }, Is.All.EqualTo(first));
        }

        #endregion
    }
}