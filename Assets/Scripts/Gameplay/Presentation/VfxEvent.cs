using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct VfxEvent
    {
        public PresentationEventId Id;
        public int VfxDefId;
        public fp2 WorldPosition;
        public fp2 WorldDirection;
        public UnitUid? AttachToUnit;
        public int SocketKey;
        public fp DurationScale;
    }
}
