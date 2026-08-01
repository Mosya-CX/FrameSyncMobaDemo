using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Key identifying a specific flow field (Team + RadiusClass).
    /// Used by RouteRuntime to reference the correct TeamFlowFieldData at runtime.
    /// (Pathfinding Design v13.1 section 14.6, section 8.3)
    /// </summary>
    [Serializable]
    public struct FlowFieldKey : IEquatable<FlowFieldKey>
    {
        public byte TeamId;
        public byte RadiusClass;

        public FlowFieldKey(byte teamId, RadiusClass rc)
        {
            TeamId = teamId;
            RadiusClass = (byte)rc;
        }

        /// <summary>Compressed key for Dictionary lookup: (TeamId &lt;&lt; 2) | RadiusClass.</summary>
        public int Packed => (TeamId << 2) | RadiusClass;

        public bool Equals(FlowFieldKey other) =>
            TeamId == other.TeamId && RadiusClass == other.RadiusClass;

        public override bool Equals(object obj) =>
            obj is FlowFieldKey other && Equals(other);

        public override int GetHashCode() => Packed;

        public static bool operator ==(FlowFieldKey a, FlowFieldKey b) => a.Equals(b);
        public static bool operator !=(FlowFieldKey a, FlowFieldKey b) => !a.Equals(b);
    }
}
