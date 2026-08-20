using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class ShieldOverTimeBuffEffect : BuffEffect
    {
        public fp ShieldPerTick;
        public ShieldType ShieldType;
        public DurationAuthoring ShieldDuration;
        [HideInInspector] public int ShieldDurationTicks = 60;

        public override void BakeTime(int tickRate)
        {
            ShieldDurationTicks = ShieldDuration.IsAuthored
                ? ShieldDuration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        ShieldDurationTicks,
                        tickRate);
        }

        public override void OnAdded(BuffRuntime runtime, Unit owner) { }
        public override void OnRemoved(BuffRuntime runtime, Unit owner) { }

        public override void OnTick(BuffRuntime runtime, Unit owner)
        {
            if (owner?.World?.CombatSystem == null || ShieldPerTick <= fp.zero)
                return;
            if (!runtime.ShouldExecutePeriodic())
                return;

            var request = new ShieldRequest
            {
                TargetUnitUid = owner.UnitUid,
                SourceUnitUid = runtime.SourceUnitUid,
                BaseValue = ShieldPerTick,
                ShieldType = ShieldType,
                DurationTicks = ShieldDurationTicks,
            };
            owner.World.CombatSystem.SubmitShield(request);
        }
    }
}
