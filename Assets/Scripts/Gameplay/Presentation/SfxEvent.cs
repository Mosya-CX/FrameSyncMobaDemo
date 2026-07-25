using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct SfxEvent
    {
        public PresentationEventId Id;
        public int SfxDefId;
        public SfxAnchor Anchor;
        public fp2 WorldPosition;
        public UnitUid? AttachToUnit;
        public int SocketKey;
        public fp PitchScale;
        public fp VolumeScale;
    }
}
