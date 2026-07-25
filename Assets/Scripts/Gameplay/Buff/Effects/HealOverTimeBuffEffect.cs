using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class HealOverTimeBuffEffect : BuffEffect
    {
        public fp HealPerTick;

        public override void OnAdded(BuffRuntime runtime, Unit owner) { }
        public override void OnRemoved(BuffRuntime runtime, Unit owner) { }

        public override void OnTick(BuffRuntime runtime, Unit owner)
        {
            if (owner?.World?.CombatSystem == null || HealPerTick <= fp.zero)
                return;
            if (!runtime.ShouldExecutePeriodic())
                return;

            var request = new HealRequest
            {
                TargetUnitUid = owner.UnitUid,
                SourceUnitUid = runtime.SourceUnitUid,
                BaseValue = HealPerTick,
            };
            owner.World.CombatSystem.SubmitHeal(request);
        }
    }
}
