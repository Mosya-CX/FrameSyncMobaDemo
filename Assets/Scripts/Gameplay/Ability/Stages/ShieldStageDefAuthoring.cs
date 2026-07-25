using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ShieldStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float baseShield = 80f;
        [SerializeField] private ShieldType shieldType = ShieldType.Magic;
        [Min(0)]
        [SerializeField] private int durationTicks = 60;
        [SerializeField] private BuffTargetRule targetRule = BuffTargetRule.Self;

        public override StageDef Bake()
        {
            return new ShieldStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                BaseShield = (fp)baseShield,
                ShieldType = shieldType,
                DurationTicks = durationTicks,
                TargetRule = targetRule,
            };
        }
    }
}
