using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public readonly struct PhysicsShape2D
    {
        internal PhysicsShape2D(
            PhysicsShapeKind kind,
            fp2 localOffset,
            fp radius,
            fp length,
            fp width,
            fp2 halfExtents,
            bool sweepFromPrev)
        {
            Kind = kind;
            LocalOffset = localOffset;
            Radius = radius;
            Length = length;
            Width = width;
            HalfExtents = halfExtents;
            SweepFromPrev = sweepFromPrev;
        }

        public PhysicsShapeKind Kind { get; }

        public fp2 LocalOffset { get; }

        public fp Radius { get; }

        public fp Length { get; }

        public fp Width { get; }

        public fp2 HalfExtents { get; }

        public bool SweepFromPrev { get; }

        public static PhysicsShape2D CreatePoint(
            fp2 localOffset,
            bool sweepFromPrev = false)
        {
            return new PhysicsShape2D(
                PhysicsShapeKind.Point,
                localOffset,
                fp.zero,
                fp.zero,
                fp.zero,
                default,
                sweepFromPrev);
        }

        public static PhysicsShape2D CreateCircle(
            fp2 localOffset,
            fp radius,
            bool sweepFromPrev = false)
        {
            if (radius < fp.zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "A deterministic circle radius must not be negative.");
            }

            return new PhysicsShape2D(
                PhysicsShapeKind.Circle,
                localOffset,
                radius,
                fp.zero,
                fp.zero,
                default,
                sweepFromPrev);
        }

        public static PhysicsShape2D CreateSegment(
            fp2 localOffset,
            fp length,
            fp width,
            bool sweepFromPrev = false)
        {
            ValidateNonnegative(length, nameof(length), "A deterministic segment length must not be negative.");
            ValidateNonnegative(width, nameof(width), "A deterministic segment width must not be negative.");

            return new PhysicsShape2D(
                PhysicsShapeKind.Segment,
                localOffset,
                fp.zero,
                length,
                width,
                default,
                sweepFromPrev);
        }

        public static PhysicsShape2D CreateRect(
            fp2 localOffset,
            fp2 halfExtents,
            bool sweepFromPrev = false)
        {
            ValidateHalfExtents(halfExtents);

            return new PhysicsShape2D(
                PhysicsShapeKind.Rect,
                localOffset,
                fp.zero,
                fp.zero,
                fp.zero,
                halfExtents,
                sweepFromPrev);
        }

        internal void ValidateSupported()
        {
            switch (Kind)
            {
                case PhysicsShapeKind.Point:
                    ValidatePointOrCircleFields(fp.zero);
                    return;

                case PhysicsShapeKind.Circle:
                    ValidateNonnegative(
                        Radius,
                        nameof(Radius),
                        "A deterministic circle radius must not be negative.");
                    ValidatePointOrCircleFields(Radius);
                    return;

                case PhysicsShapeKind.Segment:
                    ValidateNonnegative(
                        Length,
                        nameof(Length),
                        "A deterministic segment length must not be negative.");
                    ValidateNonnegative(
                        Width,
                        nameof(Width),
                        "A deterministic segment width must not be negative.");
                    ValidateSegmentFields();
                    return;

                case PhysicsShapeKind.Rect:
                    ValidateHalfExtents(HalfExtents);
                    ValidateRectFields();
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(Kind),
                        Kind,
                        "The physics shape kind is not defined.");
            }
        }

        private void ValidatePointOrCircleFields(fp expectedRadius)
        {
            if (Radius != expectedRadius ||
                Length != fp.zero ||
                Width != fp.zero ||
                HalfExtents.x != fp.zero ||
                HalfExtents.y != fp.zero)
            {
                throw new ArgumentException(
                    "Unused deterministic shape fields must be zero for Point and Circle shapes.");
            }
        }

        private void ValidateSegmentFields()
        {
            if (Radius != fp.zero || HalfExtents.x != fp.zero || HalfExtents.y != fp.zero)
            {
                throw new ArgumentException(
                    "Unused deterministic shape fields must be zero for Segment shapes.");
            }
        }

        private void ValidateRectFields()
        {
            if (Radius != fp.zero || Length != fp.zero || Width != fp.zero)
            {
                throw new ArgumentException(
                    "Unused deterministic shape fields must be zero for Rect shapes.");
            }
        }

        private static void ValidateHalfExtents(fp2 halfExtents)
        {
            ValidateNonnegative(
                halfExtents.x,
                nameof(halfExtents),
                "A deterministic Rect half extent must not be negative.");
            ValidateNonnegative(
                halfExtents.y,
                nameof(halfExtents),
                "A deterministic Rect half extent must not be negative.");
        }

        private static void ValidateNonnegative(fp value, string parameterName, string message)
        {
            if (value < fp.zero)
            {
                throw new ArgumentOutOfRangeException(parameterName, message);
            }
        }
    }
}
