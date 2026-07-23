namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoritative team identity (Unit v27.3 section 1.2).
    /// A plain byte: 0 is valid (Neutral/Unspecified), 1-255 are team slots.
    /// Immutable after Unit construction; registered in <see cref="TeamRegistry"/>.
    /// </summary>
    public readonly struct TeamId : System.IEquatable<TeamId>
    {
        public readonly byte Value;

        public TeamId(byte value)
        {
            Value = value;
        }

        public static readonly TeamId Neutral = new TeamId(0);

        public bool Equals(TeamId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is TeamId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(TeamId left, TeamId right) => left.Equals(right);

        public static bool operator !=(TeamId left, TeamId right) => !left.Equals(right);

        public override string ToString() => $"Team:{Value}";
    }
}