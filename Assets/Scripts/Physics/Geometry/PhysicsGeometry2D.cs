using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public static class PhysicsGeometry2D
    {
        private static readonly fp FacingLengthSqThreshold = 0.00000001m;
        private static readonly fp NormalizedLengthSqTolerance = fp.FromRaw(1L << 16);
        private static readonly fp Half = 0.5m;

        public static bool TryCreateFacing(fp2 input, out fp2 forward, out fp2 right)
        {
            fp lengthSq = fpmath.dot(input, input);
            if (lengthSq <= FacingLengthSqThreshold)
            {
                forward = default;
                right = default;
                return false;
            }

            forward = fpmath.normalize(input);
            right = PerpRight(forward);
            return true;
        }

        public static fp2 PerpRight(fp2 forward)
        {
            return new fp2(forward.y, -forward.x);
        }

        public static void ValidateTransform(in PhysicsTransform2D transform)
        {
            fp forwardLengthSq = fpmath.dot(transform.Forward, transform.Forward);
            bool hasForward = forwardLengthSq > FacingLengthSqThreshold;
            bool hasRight = fpmath.dot(transform.Right, transform.Right) > FacingLengthSqThreshold;

            if (!hasForward && !hasRight)
            {
                return;
            }

            if (!hasForward || !hasRight || transform.Right.x != transform.Forward.y ||
                transform.Right.y != -transform.Forward.x)
            {
                throw new ArgumentException(
                    "A restored physics facing must contain a matching Forward and clockwise Right pair.",
                    nameof(transform));
            }

            if (fpmath.abs(forwardLengthSq - fp.one) > NormalizedLengthSqTolerance)
            {
                throw new ArgumentException(
                    "A restored physics Forward vector must be normalized.",
                    nameof(transform));
            }
        }

        public static fp2 GetPointWorld(
            in PhysicsTransform2D transform,
            in PhysicsShape2D shape)
        {
            return transform.Position +
                   (transform.Right * shape.LocalOffset.x) +
                   (transform.Forward * shape.LocalOffset.y);
        }

        public static void GetSegmentWorld(
            in PhysicsTransform2D transform,
            in PhysicsShape2D shape,
            out fp2 start,
            out fp2 end,
            out fp width)
        {
            fp2 center = GetPointWorld(transform, shape);
            fp halfLength = shape.Length * Half;
            fp2 halfSegment = transform.Forward * halfLength;

            start = center - halfSegment;
            end = center + halfSegment;
            width = shape.Width;
        }

        public static void GetRectWorld(
            in PhysicsTransform2D transform,
            in PhysicsShape2D shape,
            out fp2 center,
            out fp2 right,
            out fp2 forward,
            out fp2 halfExtents)
        {
            center = GetPointWorld(transform, shape);
            right = transform.Right;
            forward = transform.Forward;
            halfExtents = shape.HalfExtents;
        }

        public static PhysicsBounds2D CalculateBounds(
            in PhysicsTransform2D transform,
            in PhysicsShape2D shape)
        {
            shape.ValidateSupported();
            fp2 point = GetPointWorld(transform, shape);

            switch (shape.Kind)
            {
                case PhysicsShapeKind.Point:
                    return shape.SweepFromPrev
                        ? FromSegment(transform.PrevPosition, point)
                        : FromPoint(point);

                case PhysicsShapeKind.Circle:
                    PhysicsBounds2D current = FromCircle(point, shape.Radius);
                    if (!shape.SweepFromPrev)
                    {
                        return current;
                    }

                    PhysicsBounds2D sweep = Expand(
                        FromSegment(transform.PrevPosition, point),
                        shape.Radius);
                    return Union(current, sweep);

                case PhysicsShapeKind.Segment:
                    GetSegmentWorld(
                        transform,
                        shape,
                        out fp2 start,
                        out fp2 end,
                        out fp width);
                    return Expand(FromSegment(start, end), width * Half);

                case PhysicsShapeKind.Rect:
                    GetRectWorld(
                        transform,
                        shape,
                        out fp2 center,
                        out fp2 right,
                        out fp2 forward,
                        out fp2 halfExtents);
                    fp2 extents =
                        (fpmath.abs(right) * halfExtents.x) +
                        (fpmath.abs(forward) * halfExtents.y);
                    return new PhysicsBounds2D(center - extents, center + extents);

                default:
                    throw new InvalidOperationException(
                        "Shape validation accepted a kind without bounds implementation.");
            }
        }

        private static PhysicsBounds2D FromPoint(fp2 point)
        {
            return new PhysicsBounds2D(point, point);
        }

        private static PhysicsBounds2D FromSegment(fp2 start, fp2 end)
        {
            return new PhysicsBounds2D(fpmath.min(start, end), fpmath.max(start, end));
        }

        private static PhysicsBounds2D FromCircle(fp2 center, fp radius)
        {
            fp2 extents = new fp2(radius, radius);
            return new PhysicsBounds2D(center - extents, center + extents);
        }

        private static PhysicsBounds2D Expand(in PhysicsBounds2D bounds, fp amount)
        {
            fp2 extents = new fp2(amount, amount);
            return new PhysicsBounds2D(bounds.Min - extents, bounds.Max + extents);
        }

        private static PhysicsBounds2D Union(
            in PhysicsBounds2D first,
            in PhysicsBounds2D second)
        {
            return new PhysicsBounds2D(
                fpmath.min(first.Min, second.Min),
                fpmath.max(first.Max, second.Max));
        }
        /// <summary>
        /// Closest point on a segment to a query point (Physics v13.1 section 8.5).
        /// Degenerate (zero-length) segment returns start.
        /// </summary>
        public static fp2 ClosestPointOnSegment(fp2 point, fp2 start, fp2 end)
        {
            fp2 ab = end - start;
            fp lengthSq = fpmath.dot(ab, ab);

            if (lengthSq == fp.zero)
            {
                return start;
            }

            fp t = fpmath.dot(point - start, ab) / lengthSq;
            t = fpmath.clamp(t, fp.zero, fp.one);
            return start + (ab * t);
        }

        /// <summary>
        /// Point vs target circle (Physics v13.1 section 8.5).
        /// Tangent contact counts as overlap.
        /// </summary>
        public static bool PointOverlapsCircle(fp2 point, fp2 circleCenter, fp circleRadius)
        {
            ValidateNonnegativeRadius(circleRadius);
            fp2 d = point - circleCenter;
            return fpmath.dot(d, d) <= circleRadius * circleRadius;
        }

        /// <summary>
        /// Swept point (segment prev->curr) vs target circle (Physics v13.1 section 8.5).
        /// Uses ClosestPointOnSegment for the circle center.
        /// </summary>
        public static bool SweptPointOverlapsCircle(
            fp2 previous,
            fp2 current,
            fp2 circleCenter,
            fp circleRadius)
        {
            ValidateNonnegativeRadius(circleRadius);
            fp2 closest = ClosestPointOnSegment(circleCenter, previous, current);
            fp2 d = circleCenter - closest;
            return fpmath.dot(d, d) <= circleRadius * circleRadius;
        }

        /// <summary>
        /// Circle vs target circle (Physics v13.1 section 8.5).
        /// Combined radius; tangent contact counts as overlap.
        /// </summary>
        public static bool CircleOverlapsCircle(
            fp2 center,
            fp radius,
            fp2 targetCenter,
            fp targetRadius)
        {
            ValidateNonnegativeRadius(radius);
            ValidateNonnegativeRadius(targetRadius);
            fp r = radius + targetRadius;
            fp2 d = center - targetCenter;
            return fpmath.dot(d, d) <= r * r;
        }

        /// <summary>
        /// Segment vs target circle (Physics v13.1 section 8.5).
        /// Combined radius = targetRadius + width/2; uses ClosestPointOnSegment.
        /// </summary>
        public static bool SegmentOverlapsCircle(
            fp2 start,
            fp2 end,
            fp width,
            fp2 targetCenter,
            fp targetRadius)
        {
            ValidateNonnegativeRadius(targetRadius);
            ValidateNonnegativeWidth(width);
            fp2 closest = ClosestPointOnSegment(targetCenter, start, end);
            fp r = targetRadius + (width * Half);
            fp2 d = targetCenter - closest;
            return fpmath.dot(d, d) <= r * r;
        }

        /// <summary>
        /// Oriented Rect vs target circle (Physics v13.1 section 8.5).
        /// Converts target center into Rect local space, clamps to half extents,
        /// compares remaining local delta against target radius squared.
        /// </summary>
        public static bool RectOverlapsCircle(
            fp2 center,
            fp2 right,
            fp2 forward,
            fp2 halfExtents,
            fp2 targetCenter,
            fp targetRadius)
        {
            ValidateNonnegativeRadius(targetRadius);
            ValidateNonnegativeHalfExtents(halfExtents);

            fp2 delta = targetCenter - center;
            fp localX = fpmath.dot(delta, right);
            fp localY = fpmath.dot(delta, forward);

            fp clampedX = fpmath.clamp(localX, -halfExtents.x, halfExtents.x);
            fp clampedY = fpmath.clamp(localY, -halfExtents.y, halfExtents.y);

            fp2 d = new fp2(localX - clampedX, localY - clampedY);
            return fpmath.dot(d, d) <= targetRadius * targetRadius;
        }

        private static void ValidateNonnegativeRadius(fp radius)
        {
            if (radius < fp.zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "A deterministic circle radius must not be negative.");
            }
        }

        private static void ValidateNonnegativeWidth(fp width)
        {
            if (width < fp.zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "A deterministic segment width must not be negative.");
            }
        }

        private static void ValidateNonnegativeHalfExtents(fp2 halfExtents)
        {
            if (halfExtents.x < fp.zero || halfExtents.y < fp.zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfExtents),
                    "A deterministic Rect half extent must not be negative.");
            }
        }
    }
}

