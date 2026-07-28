using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Presentation
{
    [Serializable]
    public struct AbilityAnimationPlan
    {
        public int CastAnimationHash;
        public int ChannelAnimationHash;
        public int CancelAnimationHash;
        public fp CastDurationTicks;
        public fp ChannelDurationTicks;
        public StageAnimationBinding[] StageBindings;
        public static readonly AbilityAnimationPlan Default = new AbilityAnimationPlan
        {
            CastAnimationHash = 0,
            ChannelAnimationHash = 0,
            CancelAnimationHash = 0,
            CastDurationTicks = fp.zero,
            ChannelDurationTicks = fp.zero,
            StageBindings = Array.Empty<StageAnimationBinding>(),
        };
    }
}
