using System;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class RefreshDurationOnKillParticipationBuffEffect :
        BuffEffect
    {
        public DurationAuthoring ExtendDuration;
        public DurationAuthoring MaximumRemainingDuration;
        public DurationAuthoring RestartBurstDuration;
        [HideInInspector] public int ExtendTicks;
        [HideInInspector] public int MaximumRemainingTicks;
        [HideInInspector] public int RestartBurstTicks;
        public BuffStateSlotId BurstRemainingTicksSlot;

        public override void BakeTime(int tickRate)
        {
            ExtendTicks = Bake(ExtendDuration, ExtendTicks, tickRate);
            MaximumRemainingTicks = Bake(
                MaximumRemainingDuration,
                MaximumRemainingTicks,
                tickRate);
            RestartBurstTicks = Bake(
                RestartBurstDuration,
                RestartBurstTicks,
                tickRate);
        }

        private static int Bake(
            in DurationAuthoring duration,
            int legacyTicks,
            int tickRate) =>
            duration.IsAuthored
                ? duration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(legacyTicks, tickRate);

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
