using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// One independently ordered shield owned by a Unit's StatHandler.
    /// </summary>
    public struct ShieldInstance
    {
        public int ShieldInstanceId;
        public ShieldType ShieldType;
        public fp CurrentValue;
        public fp MaxValue;
        public int StartLogicTick;
        public int ExpireLogicTick;
        public UnitUid SourceUnitUid;
        public CrowdControlImmunityHandle CrowdControlImmunityHandle;

        public bool IsActive => CurrentValue > fp.zero;
    }
}
