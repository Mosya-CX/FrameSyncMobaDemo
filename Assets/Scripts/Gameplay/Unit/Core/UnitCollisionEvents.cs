using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public readonly struct UnitCollisionEnterEvent
    {
        public readonly UnitUid OtherUnitUid;
        public readonly fp2 ContactNormal;

        public UnitCollisionEnterEvent(UnitUid otherUnitUid, fp2 contactNormal)
        {
            OtherUnitUid = otherUnitUid;
            ContactNormal = contactNormal;
        }
    }

    public readonly struct UnitCollisionExitEvent
    {
        public readonly UnitUid OtherUnitUid;

        public UnitCollisionExitEvent(UnitUid otherUnitUid)
        {
            OtherUnitUid = otherUnitUid;
        }
    }
}
