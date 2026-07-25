using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ApplyBuffStageDefAuthoring : StageDefAuthoring
    {
        [Min(1)]
        [SerializeField] private int buffConfigId = 1;
        [SerializeField] private BuffTargetRule targetRule = BuffTargetRule.Self;

        public int BuffConfigId => buffConfigId;
        public BuffTargetRule TargetRule => targetRule;

        public override StageDef Bake()
        {
            return new ApplyBuffStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                BuffConfigId = new BuffConfigId(buffConfigId),
                TargetRule = targetRule,
            };
        }
    }
}
