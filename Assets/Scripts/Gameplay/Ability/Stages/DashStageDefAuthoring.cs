using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class DashStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float speedPerTick = 1f;
        [Min(0f)]
        [SerializeField] private float totalDistance = 8f;

        public float SpeedPerTick => speedPerTick;
        public float TotalDistance => totalDistance;

        public override StageDef Bake()
        {
            if (StageKey <= 0 ||
                speedPerTick <= 0f ||
                totalDistance <= 0f)
            {
                throw new InvalidOperationException(
                    "Dash Stage requires a positive StageKey, speed and total distance.");
            }
            return new DashStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                SpeedPerTick = (fp)speedPerTick,
                TotalDistance = (fp)totalDistance,
            };
        }
    }
}
