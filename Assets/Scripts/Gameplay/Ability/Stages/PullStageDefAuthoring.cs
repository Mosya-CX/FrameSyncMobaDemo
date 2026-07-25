using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class PullStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float speedPerTick = 1.5f;
        [Min(0f)]
        [SerializeField] private float minDistance = 1f;

        public override StageDef Bake()
        {
            return new PullStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                SpeedPerTick = (fp)speedPerTick,
                MinDistance = (fp)minDistance,
            };
        }
    }
}
