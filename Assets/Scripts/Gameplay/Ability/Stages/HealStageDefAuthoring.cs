using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class HealStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float baseHeal = 50f;
        [SerializeField] private BuffTargetRule targetRule = BuffTargetRule.Self;

        public override StageDef Bake(int tickRate = 30)
        {
            return new HealStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                BaseHeal = (fp)baseHeal,
                TargetRule = targetRule,
            };
        }
    }
}
