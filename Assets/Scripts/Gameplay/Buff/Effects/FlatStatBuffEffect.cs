using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Applies a flat stat modifier (default MaxHealth) while the buff is
    /// active. The value is read from a runtime Fp blackboard slot so the
    /// applier (e.g. an equipment passive) can pass a dynamic amount after
    /// BuffHandler.Apply; OnTick keeps the modifier in sync with the slot.
    /// Removes the modifier on removal/death/despawn and re-applies it on
    /// respawn when the buff survives.
    /// </summary>
    public sealed class FlatStatBuffEffect : BuffEffect
    {
        public StatId Stat = StatId.MaxHealth;
        public BuffStateSlotId ValueSlot;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
            new[]
            {
                new BuffStateSlotDefinition
                {
                    SlotId = ValueSlot,
                    Kind = BuffValueKind.Fp,
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
            // The value slot may be written by the applier after Apply;
            // OnTick performs the first application.
        }

        public override void OnReapplied(
            BuffRuntime runtime,
            Unit owner)
        {
            ApplyOrUpdate(runtime, owner);
        }

        public override void OnTick(
            BuffRuntime runtime,
            Unit owner)
        {
            ApplyOrUpdate(runtime, owner);
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
            Release(runtime, owner);
        }

        public override void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
            Release(runtime, owner);
        }

        public override void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            Release(runtime, owner);
        }

        public override void ClearForRespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            ApplyOrUpdate(runtime, owner);
        }

        private void ApplyOrUpdate(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null ||
                !ValueSlot.IsValid ||
                !HandleSlot.IsValid)
            {
                return;
            }
            fp value = runtime.Blackboard
                .ReadFpOrDefault(ValueSlot);
            if (value <= fp.zero)
            {
                return;
            }
            if (runtime.Blackboard.TryGetStatHandle(
                    HandleSlot,
                    out StatModifierHandle handle) &&
                handle.IsValid)
            {
                owner.StatHandler.SetModifierValue(
                    handle,
                    value);
                return;
            }
            handle = owner.StatHandler.AddModifier(
                Stat,
                StatModifierOperation.FlatAdd,
                value);
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                handle);
        }

        private void Release(
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
                    out StatModifierHandle handle) &&
                handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(handle);
            }
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                default);
        }
    }
}
