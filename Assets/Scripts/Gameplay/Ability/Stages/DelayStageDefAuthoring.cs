using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class DelayStageDefAuthoring : StageDefAuthoring
    {
        public override StageDef Bake()
        {
            return new DelayStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
            };
        }
    }
}
