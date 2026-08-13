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
        [SerializeField] private ForceMoveWallPolicy wallPolicy =
            ForceMoveWallPolicy.StopAtWall;
        [SerializeField] private bool resetAttackTimerOnStart;
        [Min(0f)] [SerializeField] private float maxTerrainCrossingDistance;
        [SerializeField] private bool extendThroughTerrain;

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
                WallPolicy = wallPolicy,
                ResetAttackTimerOnStart = resetAttackTimerOnStart,
                MaxTerrainCrossingDistance =
                    (fp)maxTerrainCrossingDistance,
                ExtendThroughTerrain = extendThroughTerrain,
            };
        }
    }
}
