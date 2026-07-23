using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Healing request (Combat v13.2 section 8.1).
    /// Submitted to restore target health through the healing pipeline.
    /// </summary>
    public struct HealRequest
    {
        public CombatRequestHeader Header;
        public UnitUid SourceUnitUid;
        public UnitUid TargetUnitUid;

        /// <summary>Raw heal amount before modifier pipelines.</summary>
        public fp BaseValue;

        /// <summary>True when this is an active request.</summary>
        public bool IsValid =>
            SourceUnitUid.IsValid() && TargetUnitUid.IsValid() && BaseValue > fp.zero;

        public static readonly HealRequest None = default;
    }
}
