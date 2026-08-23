using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Read-only buff snapshot for external consumers (design v14.2 11.2).
    /// TimeProgress is presentation data and never enters Gameplay.
    /// </summary>
    public readonly struct BuffInfo
    {
        public readonly BuffConfigId Id;
        public readonly string Name;
        public readonly string Description;
        public readonly string IconAddress;
        public readonly int StackCount;
        public readonly int MaxStacks;
        public readonly bool Infinite;
        public readonly int RemainingTicks;
        public readonly int DurationTicks;
        public readonly float TimeProgress;
        public readonly byte[] Tags;
        public readonly BuffSource Source;

        public BuffInfo(
            BuffConfigId id,
            string name,
            string description,
            string iconAddress,
            int stackCount,
            int maxStacks,
            bool infinite,
            int remainingTicks,
            int durationTicks,
            byte[] tags,
            BuffSource source)
        {
            Id = id;
            Name = name;
            Description = description;
            IconAddress = iconAddress;
            StackCount = stackCount;
            MaxStacks = maxStacks;
            Infinite = infinite;
            RemainingTicks = remainingTicks;
            DurationTicks = durationTicks;
            Tags = tags;
            Source = source;
            TimeProgress =
                durationTicks > 0
                    ? Mathf.Clamp01(
                        remainingTicks /
                        (float)durationTicks)
                    : 0f;
        }
    }
}
