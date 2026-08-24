using FrameSyncMoba.Deterministic;
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
                Header = CombatRequestHeader.Create(
                    runtime.SourceUnitUid,
                    owner.UnitUid,
                    CombatSourceType.Buff,
                    runtime.ConfigId.Value,
                    runtime.ConfigId.Value,
                    originActionId:
                        CombatActionIdentityFactory.CreateFromSource(
                            owner.World,
                            runtime.SourceUnitUid,
                            CombatSourceType.Buff,
                            runtime.ConfigId.Value,
                            SimulationTickContext.Current.Tick -
                                runtime.ElapsedTicks,
                            owner.GameplayParticipantId,
                            runtime.ConfigId.Value),
                    effectOrdinal:
                        CombatFairnessKey.ComposeEffectOrdinal(
                            runtime.ConfigId.Value,
                            runtime.ElapsedTicks)),
                BaseDamage = DamagePerTick,
                DamageType = DamageType,
            };
            owner.World.CombatSystem.SubmitDamage(request);
        }
    }
}
