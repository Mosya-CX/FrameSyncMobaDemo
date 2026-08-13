using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class RefreshDurationOnKillParticipationBuffEffect :
        BuffEffect
    {
        public int ExtendTicks;
        public int MaximumRemainingTicks;
        public int RestartBurstTicks;
        public BuffStateSlotId BurstRemainingTicksSlot;

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner)
        {
        }

        public override BuffStateSlotDefinition[] RequiredSlotDefinitions =>
            new[]
            {
                new BuffStateSlotDefinition
                {
                    SlotId = BurstRemainingTicksSlot,
                    Kind = BuffValueKind.Int,
                },
            };

        public override void OnUnitKill(
            BuffRuntime runtime,
            Unit owner,
            Unit victim) => Refresh(runtime, victim);

        public override void OnUnitAssist(
            BuffRuntime runtime,
            Unit owner,
            Unit victim) => Refresh(runtime, victim);

        private void Refresh(BuffRuntime runtime, Unit victim)
        {
            if (victim == null || victim.UnitKind != UnitKind.Hero)
                return;
            int remaining = runtime.RemainingTicks + ExtendTicks;
            if (remaining > MaximumRemainingTicks)
                remaining = MaximumRemainingTicks;
            runtime.SetRemainingTicks(remaining);
            runtime.Blackboard.WriteInt(
                BurstRemainingTicksSlot,
                RestartBurstTicks);
        }
    }
}
