using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class PullStageDefAuthoring : StageDefAuthoring
    {
        [Tooltip("Movement speed in logic distance units per second.")]
        [Min(0f)]
        [SerializeField] private float speedPerSecond;
        [HideInInspector, Min(0f)]
        [SerializeField] private float speedPerTick = 1.5f;
        [Min(0f)]
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private byte priority;

        public override StageDef Bake(int tickRate = 30)
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
                SpeedPerTick = (fp)(
                    speedPerSecond > 0f
                        ? speedPerSecond / tickRate
                        : speedPerTick * 30f / tickRate),
                MinDistance = (fp)minDistance,
                Priority = priority,
            };
        }
    }
}
