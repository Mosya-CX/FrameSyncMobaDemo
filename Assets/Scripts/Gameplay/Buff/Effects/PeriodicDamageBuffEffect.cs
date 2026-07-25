using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class PeriodicDamageBuffEffect : BuffEffect
    {
        public fp DamagePerTick;
        public DamageType DamageType;

        public override void OnAdded(BuffRuntime runtime, Unit owner) { }
        public override void OnRemoved(BuffRuntime runtime, Unit owner) { }

        public override void OnTick(BuffRuntime runtime, Unit owner)
        {
            if (owner?.World?.CombatSystem == null || DamagePerTick <= fp.zero)
                return;
            if (!runtime.ShouldExecutePeriodic())
                return;

            var request = new DamageRequest
            {
                TargetUnitUid = owner.UnitUid,
                SourceUnitUid = runtime.SourceUnitUid,
                BaseDamage = DamagePerTick,
                DamageType = DamageType,
            };
            owner.World.CombatSystem.SubmitDamage(request);
        }
    }
}
