using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class StunStageDefAuthoring : StageDefAuthoring
    {
        [Min(0)]
        [SerializeField] private int durationTicks = 30;

        public override StageDef Bake()
        {
            return new StunStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                DurationTicks = durationTicks,
            };
        }
    }
}
