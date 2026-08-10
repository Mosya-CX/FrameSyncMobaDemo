using System;

namespace FrameSyncMoba.Unit
{
    public enum PresentationSourceKind
    {
        Unit,
        Projectile,
    }

    public enum SfxAnchor
    {
        UnitRoot,
        Camera,
        World,
    }

    public static class PresentationEventKeys
    {
        public const int CombatHit = 1;
        public const int CombatDeath = 2;
        public const int AbilityCast = 3;
        public const int BuffApplied = 4;
        public const int BuffDetonated = 5;
    }

    public struct PresentationEventId : IEquatable<PresentationEventId>
    {
        public int SourceLogicTick;
        public PresentationSourceKind SourceKind;
        public UnitUid SourceRuntimeUid;
        public int EventSequence;
        public int EventKey;

        public bool Equals(PresentationEventId other)
        {
            return SourceLogicTick == other.SourceLogicTick
                && SourceKind == other.SourceKind
                && SourceRuntimeUid.Equals(other.SourceRuntimeUid)
                && EventSequence == other.EventSequence
                && EventKey == other.EventKey;
        }

        public override bool Equals(object obj)
            => obj is PresentationEventId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceLogicTick;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ SourceRuntimeUid.GetHashCode();
                hash = (hash * 397) ^ EventSequence;
                hash = (hash * 397) ^ EventKey;
                return hash;
            }
        }

        public static bool operator ==(PresentationEventId a, PresentationEventId b) => a.Equals(b);
        public static bool operator !=(PresentationEventId a, PresentationEventId b) => !a.Equals(b);
    }
}
