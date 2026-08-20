using System;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class StunStageDefAuthoring : StageDefAuthoring
    {
        [SerializeField] private DurationAuthoring duration;
        [HideInInspector, Min(0)]
        [SerializeField] private int durationTicks = 30;

        public override StageDef Bake(int tickRate = 30)
        {
            return new StunStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                DurationTicks = BakeHelpers.BakeDuration(
                    duration, durationTicks, tickRate),
            };
        }
    }
}
