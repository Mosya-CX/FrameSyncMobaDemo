using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct AbilitySignal
    {
        public byte Slot;
        public AbilitySignalVerb Verb;
        public AimSnapshot Aim;
        public static readonly AbilitySignal None = default;
    }

    public enum AbilitySignalVerb : byte
    {
        Focus = 0,
        Commit = 1,
        Cancel = 2,
    }

    public enum AimKind : byte
    {
        None = 0,
        Self = 1,
        Point = 2,
        Unit = 3,
        Direction = 4,
    }

    public readonly struct AimSnapshot : IEquatable<AimSnapshot>
    {
        public readonly AimKind Kind;
        public readonly UnitUid TargetUnitUid;
        public readonly fp2 TargetPoint;
        public readonly fp2 Direction;

        private AimSnapshot(
            AimKind kind,
            UnitUid targetUnitUid,
            fp2 targetPoint,
            fp2 direction)
        {
            Kind = kind;
            TargetUnitUid = targetUnitUid;
            TargetPoint = targetPoint;
            Direction = direction;
        }

        public static AimSnapshot Self => new AimSnapshot(
            AimKind.Self, default, default, default);

        public static AimSnapshot ForPoint(fp2 targetPoint) => new AimSnapshot(
            AimKind.Point, default, targetPoint, default);

        public static AimSnapshot ForUnit(UnitUid targetUnitUid)
        {
            if (!targetUnitUid.IsValid())
            {
                throw new ArgumentException("Unit aim requires a valid UnitUid.", nameof(targetUnitUid));
            }

            return new AimSnapshot(AimKind.Unit, targetUnitUid, default, default);
        }

        public static AimSnapshot ForDirection(fp2 direction)
        {
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction, out fp2 normalized, out _))
            {
                throw new ArgumentException(
                    "Direction aim requires a non-zero direction.", nameof(direction));
            }

            return new AimSnapshot(AimKind.Direction, default, default, normalized);
        }

        public bool Equals(AimSnapshot other)
        {
            return Kind == other.Kind
                && TargetUnitUid == other.TargetUnitUid
                && TargetPoint.Equals(other.TargetPoint)
                && Direction.Equals(other.Direction);
        }

        public override bool Equals(object obj) => obj is AimSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ TargetUnitUid.GetHashCode();
                hash = (hash * 397) ^ TargetPoint.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(AimSnapshot left, AimSnapshot right) => left.Equals(right);
        public static bool operator !=(AimSnapshot left, AimSnapshot right) => !left.Equals(right);

        public static readonly AimSnapshot None = default;
    }
}
