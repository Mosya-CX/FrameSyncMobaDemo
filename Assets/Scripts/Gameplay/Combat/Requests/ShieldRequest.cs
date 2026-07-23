using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Shield absorption request (Combat v13.2 section 6.1).
    /// Submitted before damage settlement to place a shield on a target.
    /// </summary>
    public struct ShieldRequest
    {
        public CombatRequestHeader Header;
        public UnitUid SourceUnitUid;
        public UnitUid TargetUnitUid;

        /// <summary>Raw shield amount before modifier pipelines.</summary>
        public fp BaseValue;

        /// <summary>Physical, magic, or white shield.</summary>
        public ShieldType ShieldType;

        /// <summary>
        /// Deterministic lifetime in Gameplay ticks. A value of zero means that
        /// the shield has no natural expiry and must be consumed or removed.
        /// </summary>
        public int DurationTicks;

        /// <summary>True when this is an active (non-default) request.</summary>
        public bool IsValid =>
            SourceUnitUid.IsValid() && TargetUnitUid.IsValid() &&
            BaseValue > fp.zero && DurationTicks >= 0;

        public static readonly ShieldRequest None = default;
    }

    /// <summary>
    /// Shield type determines which damage channels the shield absorbs
    /// (Combat v13.2 section 6.1).
    /// </summary>
    public enum ShieldType : byte
    {
        White = 0,
        Physical = 1,
        Magic = 2,
        Black = 3,
    }
}
