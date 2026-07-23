using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public readonly struct UnitContactPair :
        IEquatable<UnitContactPair>, IComparable<UnitContactPair>
    {
        public readonly RuntimeUidQueryValue MinUid;
        public readonly RuntimeUidQueryValue MaxUid;

        public UnitContactPair(
            RuntimeUidQueryValue first,
            RuntimeUidQueryValue second)
        {
            if (!first.IsValid || !second.IsValid || first == second)
                throw new ArgumentException(
                    "A Unit contact pair requires two distinct valid runtime UIDs.");
            if (first.CompareTo(second) < 0)
            {
                MinUid = first;
                MaxUid = second;
            }
            else
            {
                MinUid = second;
                MaxUid = first;
            }
        }

        public int CompareTo(UnitContactPair other)
        {
            int min = MinUid.CompareTo(other.MinUid);
            return min != 0 ? min : MaxUid.CompareTo(other.MaxUid);
        }

        public bool Equals(UnitContactPair other) =>
            MinUid == other.MinUid && MaxUid == other.MaxUid;

        public override bool Equals(object obj) =>
            obj is UnitContactPair other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (MinUid.GetHashCode() * 397) ^ MaxUid.GetHashCode(); }
        }
    }

    public struct UnitCollisionEventBufferSnapshot
    {
        public UnitContactPair[] PreviousPairs;
        public static readonly UnitCollisionEventBufferSnapshot Empty = default;
    }

    public struct PhysicsRuntimeSnapshot
    {
        public UnitCollisionEventBufferSnapshot CollisionBuffer;
        public static readonly PhysicsRuntimeSnapshot Empty = default;
    }

    public interface IUnitCollisionParticipant
    {
        bool CanParticipateInUnitCollision { get; }
        void PublishUnitCollisionEnter(
            RuntimeUidQueryValue otherUid,
            fp2 contactNormal);
        void PublishUnitCollisionExit(RuntimeUidQueryValue otherUid);
    }
}
