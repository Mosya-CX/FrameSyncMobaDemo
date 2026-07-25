using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class ShieldOverTimeBuffEffect : BuffEffect
    {
        public fp ShieldPerTick;
        public ShieldType ShieldType;
        public int ShieldDurationTicks = 60;

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
