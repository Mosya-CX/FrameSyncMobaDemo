using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Semantic attachment anchor for SFX events (Attack Design v6.2 2.2,
    /// Presentation Design v13.2 section 5). Values match the common socket
    /// names of PresentationSocketSet; managers resolve the anchor to a
    /// socket Transform on the unit presentation host.
    /// </summary>
    public enum PresentationAnchor : byte
    {
        UnitRoot = 0,
        Head = 1,
        Chest = 2,
        HandR = 3,
        HandL = 4,
        FootR = 5,
        FootL = 6,
        ProjectileOrigin = 7,
    }

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
