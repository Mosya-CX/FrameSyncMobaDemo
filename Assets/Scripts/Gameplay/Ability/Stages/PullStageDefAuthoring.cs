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
        [SerializeField] private byte priority;

        public override StageDef Bake()
        {
            if (speedPerTick <= 0f ||
                minDistance < 0f)
            {
                throw new InvalidOperationException(
                    "Pull Stage requires positive speed and non-negative minimum distance.");
            }
            return new PullStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                SpeedPerTick = (fp)speedPerTick,
                MinDistance = (fp)minDistance,
                Priority = priority,
            };
        }
    }
}
