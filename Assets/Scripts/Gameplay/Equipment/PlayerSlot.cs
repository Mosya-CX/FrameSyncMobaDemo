using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public readonly struct PlayerSlot : IEquatable<PlayerSlot>, IComparable<PlayerSlot>
    {
        public readonly int SlotIndex;
        public readonly TeamId Team;
        public PlayerSlot(int slotIndex, TeamId team) { SlotIndex = slotIndex; Team = team; }
        public bool Equals(PlayerSlot other) => SlotIndex == other.SlotIndex && Team == other.Team;
        public override bool Equals(object obj) => obj is PlayerSlot other && Equals(other);
        public override int GetHashCode() => SlotIndex.GetHashCode() ^ Team.GetHashCode();
        public int CompareTo(PlayerSlot other)
        {
            int cmp = Team.CompareTo(other.Team);
            return cmp != 0 ? cmp : SlotIndex.CompareTo(other.SlotIndex);
        }
        public static bool operator ==(PlayerSlot a, PlayerSlot b) => a.Equals(b);
        public static bool operator !=(PlayerSlot a, PlayerSlot b) => !a.Equals(b);
        public override string ToString() => $"Slot{SlotIndex}_Team{Team}";
        public static implicit operator int(PlayerSlot slot) => slot.SlotIndex;
    }
}
