using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

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
        [SerializeField] private DurationAuthoring controlDuration;
        [HideInInspector, Min(1)] [SerializeField] private int controlDurationTicks = 1;
        [SerializeField] private DurationAuthoring applyDelay;
        [HideInInspector, Min(0)] [SerializeField] private int applyDelayTicks;

        public override StageDef Bake(int tickRate = 30)
        {
            if (!selfBuffConfigId.IsValid ||
                radius <= 0f ||
                targetFilter.UnitKindMask.IsEmpty ||
                targetFilter.LifeStateMask.IsEmpty ||
                controlId <= 0 ||
                BakeHelpers.BakeDuration(
                    controlDuration,
                    controlDurationTicks,
                    tickRate) <= 0)
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
                ControlDurationTicks = BakeHelpers.BakeDuration(
                    controlDuration, controlDurationTicks, tickRate),
                ApplyDelayTicks = BakeHelpers.BakeDuration(
                    applyDelay, applyDelayTicks, tickRate),
            };
        }
    }
}
