using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class TeleportStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float distance = 8f;

        public override StageDef Bake(int tickRate = 30)
        {
            return new TeleportStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                Distance = (fp)distance,
            };
        }
    }
}
