using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Snapshot DTO for in-game HUD data sent to Lua each frame.
    /// Contains health, resource, cooldown, gold, and buff info.
    /// Read-only; never enters GameplaySnapshot.
    ///
    /// Design: UI/Lua v9.1 section 1.4.
    /// </summary>
    public struct HudSnapshotDto
    {
        public fp HealthCurrent;
        public fp HealthMax;
        public fp ResourceCurrent;
        public fp ResourceMax;
        public AbilityCooldownDto[] AbilityCooldowns;
        public int AvailableGold;
        public int ConfirmedGold;
        public BuffInfoDto[] BuffInfos;

        public static readonly HudSnapshotDto Empty = new HudSnapshotDto
        {
            AbilityCooldowns = System.Array.Empty<AbilityCooldownDto>(),
            BuffInfos = System.Array.Empty<BuffInfoDto>(),
        };
    }

    public struct AbilityCooldownDto
    {
        public int Slot;
        public int RemainingTicks;
        public int TotalTicks;
    }

    public struct BuffInfoDto
    {
        public int BuffId;
        public int StackCount;
        public int RemainingTicks;
        public int MaxStacks;
        public int IconIndex;
    }
}
