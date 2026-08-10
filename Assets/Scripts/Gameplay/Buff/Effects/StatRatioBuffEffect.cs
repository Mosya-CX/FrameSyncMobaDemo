using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Adds a FinalRatioAdd stat modifier (e.g. -0.4 = -40%) on the given
    /// stat while the buff is active, and removes it when the buff ends.
    /// Generic building block for slows, grievous wounds, etc.
    /// </summary>
    public sealed class StatRatioBuffEffect : BuffEffect
    {
        public StatId Stat;
        public fp Ratio;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
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
            if (owner?.StatHandler == null ||
                !HandleSlot.IsValid ||
                Ratio == fp.zero)
            {
                return;
            }
            var handle = owner.StatHandler
                .AddModifier(
                    Stat,
                    StatModifierOperation
                        .FinalRatioAdd,
                    Ratio);
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                handle);
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

        private void ReleaseHandle(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null ||
                !HandleSlot.IsValid)
            {
                return;
            }
            if (runtime.Blackboard.TryGetStatHandle(
                    HandleSlot,
                    out var handle) &&
                handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(
                    handle);
            }
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                default);
        }
    }
}
