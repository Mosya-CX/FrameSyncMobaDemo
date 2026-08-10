namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Minimal control instance (CC v6.2 4.1). Value type; no module objects,
    /// no Definition reference, no source metadata.
    /// </summary>
    public readonly struct CrowdControlInstance
    {
        public readonly int InstanceId;
        public readonly CrowdControlId ControlId;
        public readonly int StartTick;
        public readonly int ExpireTick;
        public readonly CrowdControlParamBlock Params;

        public CrowdControlInstance(
            int instanceId,
            CrowdControlId controlId,
            int startTick,
            int expireTick,
            in CrowdControlParamBlock parameters)
        {
            InstanceId = instanceId;
            ControlId = controlId;
            StartTick = startTick;
            ExpireTick = expireTick;
            Params = parameters;
        }

        public CrowdControlHandle MakeHandle(
            UnitUid ownerUid) =>
            new CrowdControlHandle(
                ownerUid,
                InstanceId);
    }
}
