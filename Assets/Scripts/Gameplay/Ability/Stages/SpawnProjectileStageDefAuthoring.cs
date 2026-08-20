using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class SpawnProjectileStageDefAuthoring : StageDefAuthoring
    {
        [Min(1)]
        [SerializeField] private int projectileDefId = 1;
        [Min(0f)]
        [SerializeField] private float spawnOffsetDistance = 1f;

        public int ProjectileDefId => projectileDefId;
        public float SpawnOffsetDistance => spawnOffsetDistance;

        public override StageDef Bake(int tickRate = 30)
        {
            return new SpawnProjectileStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                ProjectileDefId = projectileDefId,
                SpawnOffsetDistance = (fp)spawnOffsetDistance,
            };
        }
    }
}
