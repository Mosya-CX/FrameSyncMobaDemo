using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unique identity of one invisible tag application. Composed of the
    /// source unit, the source kind (Ability/Buff/Item/...), the concrete
    /// source id and the logic Tick, so two casts of the same effect (e.g.
    /// two Varus R) always produce different Uids while the tag Key stays
    /// identical. Same Tick + same source + same id can only apply once per
    /// target (the caller deduplicates), so no per-frame sequence is needed.
    /// </summary>
    [Serializable]
    public readonly struct UnitTagUid :
        IEquatable<UnitTagUid>,
        IComparable<UnitTagUid>
    {
        public readonly UnitUid SourceUnit;
        public readonly byte SourceKind;
        public readonly int SourceId;
        public readonly int Tick;

        public UnitTagUid(
            UnitUid sourceUnit,
            byte sourceKind,
            int sourceId,
            int tick)
        {
            SourceUnit = sourceUnit;
            SourceKind = sourceKind;
            SourceId = sourceId;
            Tick = tick;
        }

        public bool IsValid =>
            SourceUnit.IsValid() &&
            SourceId != 0;

        public bool Equals(UnitTagUid other)
        {
            return SourceUnit == other.SourceUnit &&
                SourceKind == other.SourceKind &&
                SourceId == other.SourceId &&
                Tick == other.Tick;
        }

        public override bool Equals(object obj) =>
            obj is UnitTagUid other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceUnit.GetHashCode();
                hash = (hash * 397) ^ SourceKind;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Tick;
                return hash;
            }
        }

        public int CompareTo(UnitTagUid other)
        {
            int c = SourceUnit.CompareTo(other.SourceUnit);
            if (c != 0) return c;
            c = SourceKind.CompareTo(other.SourceKind);
            if (c != 0) return c;
            c = SourceId.CompareTo(other.SourceId);
            if (c != 0) return c;
            return Tick.CompareTo(other.Tick);
        }

        public override string ToString() =>
            $"TagUid({SourceUnit},{SourceKind},{SourceId}@{Tick})";

        public static bool operator ==(
            UnitTagUid left,
            UnitTagUid right) =>
            left.Equals(right);

        public static bool operator !=(
            UnitTagUid left,
            UnitTagUid right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// One invisible tag on a Unit: a string identifier, a remaining-lifetime
    /// counter and the unique application Uid. Kept deliberately outside the
    /// Buff system - a lightweight cross-Tick marker for deduplication and
    /// per-cast isolation. RemainingTicks == 0 means permanent.
    /// </summary>
    [Serializable]
    public struct UnitTag :
        IComparable<UnitTag>
    {
        public string Key;
        public int RemainingTicks;
        public UnitTagUid Uid;

        public bool IsValid =>
            !string.IsNullOrEmpty(Key) &&
            Uid.IsValid;

        public int CompareTo(UnitTag other)
        {
            return string.CompareOrdinal(
                Key ?? string.Empty,
                other.Key ?? string.Empty);
        }
    }
}
