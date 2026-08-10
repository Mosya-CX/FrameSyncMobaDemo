using System;

namespace FrameSyncMoba.Unit
{
    public struct BuffRuntimeSnapshot
    {
        public BuffConfigId ConfigId;
        public UnitUid SourceUnitUid;
        public BuffSourceType SourceType;
        public int SourceConfigId;
        public int RemainingTicks;
        public int CurrentStacks;
        public int ElapsedTicks;
        public bool IsPermanent;
        public int PeriodicTimer;
        public RemovalReason RemovalReason;
        public bool IsRemoving;
        public BuffBlackboardSnapshot Blackboard;

        public static readonly BuffRuntimeSnapshot Default = new BuffRuntimeSnapshot
        {
            ConfigId = BuffConfigId.Invalid,
        };
    }

    public struct BuffHandlerSnapshot
    {
        public BuffRuntimeSnapshot[] Buffs;

        public static readonly BuffHandlerSnapshot Empty = new BuffHandlerSnapshot
        {
            Buffs = Array.Empty<BuffRuntimeSnapshot>(),
        };
    }
}
