using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class SelfBuffAreaControlStageDefAuthoring :
        StageDefAuthoring
    {
        [SerializeField] private BuffConfigId selfBuffConfigId;
        [Min(0f)] [SerializeField] private float radius;
        [SerializeField] private UnitTargetFilter targetFilter =
            UnitTargetFilter.Default;
        [Min(1)] [SerializeField] private int controlId = 1;
        [Min(1)] [SerializeField] private int controlDurationTicks = 1;
        [Min(0)] [SerializeField] private int applyDelayTicks;

        public override StageDef Bake()
        {
            if (!selfBuffConfigId.IsValid ||
                radius <= 0f ||
                targetFilter.UnitKindMask.IsEmpty ||
                targetFilter.LifeStateMask.IsEmpty ||
                controlId <= 0 ||
                controlDurationTicks <= 0)
            {
                throw new InvalidOperationException(
                    $"Self-buff area-control stage '{DebugName}' is invalid.");
            }
            return new SelfBuffAreaControlStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                SelfBuffConfigId = selfBuffConfigId,
                Radius = (fp)radius,
                TargetFilter = targetFilter,
                ControlId = new CrowdControlId(controlId),
                ControlDurationTicks = controlDurationTicks,
                ApplyDelayTicks = applyDelayTicks,
            };
        }
    }
}
