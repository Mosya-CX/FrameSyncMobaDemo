using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Grants a per-stack stat value on kill. Uses one stat handle whose value
    /// is updated through StatHandler.SetModifierValue (design v14.2 9.3).
    /// </summary>
    public sealed class OnKillStatBuffEffect : BuffEffect
    {
        public StatId StatId = StatId.AttackDamage;
        public StatModifierOperation Operation =
            StatModifierOperation.FlatAdd;
        public fp ValuePerStack = (fp)5;
        public int MaxStacks = 5;
        public DurationAuthoring Duration;
        [HideInInspector] public int DurationTicks = 300;

        public override void BakeTime(int tickRate)
        {
            DurationTicks = Duration.IsAuthored
                ? Duration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(DurationTicks, tickRate);
        }
        public BuffStateSlotId StackCountSlot;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = StackCountSlot,
                        Kind = BuffValueKind.Int,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = HandleSlot,
                        Kind =
                            BuffValueKind
                                .StatModifierHandle,
                    },
                };

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
            runtime.Blackboard.WriteInt(
                StackCountSlot,
                0);
        }

        public override void OnUnitKill(
            BuffRuntime runtime,
            Unit owner,
            Unit victim)
        {
            if (owner?.StatHandler == null ||
                ValuePerStack <= fp.zero)
                return;
            int stacks = runtime.Blackboard
                .ReadIntOrDefault(StackCountSlot);
            if (stacks >= MaxStacks)
                return;
            int newStacks = stacks + 1;
            runtime.Blackboard.WriteInt(
                StackCountSlot,
                newStacks);
            if (runtime.Blackboard
                    .TryGetStatHandle(
                        HandleSlot,
                        out var handle) &&
                handle.IsValid)
            {
                owner.StatHandler.SetModifierValue(
                    handle,
                    ValuePerStack * newStacks);
            }
            else
            {
                var created = owner.StatHandler
                    .AddModifier(
                        StatId,
                        Operation,
                        ValuePerStack * newStacks);
                runtime.Blackboard.WriteStatHandle(
                    HandleSlot,
                    created);
            }
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForRespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            runtime.Blackboard.WriteInt(
                StackCountSlot,
                0);
        }

        private void ReleaseHandle(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null)
                return;
            if (runtime.Blackboard
                    .TryGetStatHandle(
                        HandleSlot,
                        out var handle) &&
                handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(
                    handle);
                runtime.Blackboard.WriteStatHandle(
                    HandleSlot,
                    default);
            }
            runtime.Blackboard.WriteInt(
                StackCountSlot,
                0);
        }
    }
}
