using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class DecayingAbilityRankStatModifierBuffEffect : BuffEffect
    {
        public StatId StatId;
        public StatModifierOperation Operation;
        public AbilityLevelValue PeakValueByAbilityLevel;
        public int DecayTicks;
        public BuffStateSlotId HandleSlot;
        public BuffStateSlotId BurstRemainingTicksSlot;

        public override BuffStateSlotDefinition[] RequiredSlotDefinitions =>
            new[]
            {
                new BuffStateSlotDefinition
                {
                    SlotId = HandleSlot,
                    Kind = BuffValueKind.StatModifierHandle,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = BurstRemainingTicksSlot,
                    Kind = BuffValueKind.Int,
                },
            };

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            runtime.Blackboard.WriteInt(
                BurstRemainingTicksSlot,
                DecayTicks);
            Apply(runtime, owner);
        }

        public override void OnReapplied(BuffRuntime runtime, Unit owner)
        {
            runtime.Blackboard.WriteInt(
                BurstRemainingTicksSlot,
                DecayTicks);
            Apply(runtime, owner);
        }

        public override void OnTick(BuffRuntime runtime, Unit owner)
        {
            Apply(runtime, owner);
            int remaining = runtime.Blackboard.ReadIntOrDefault(
                BurstRemainingTicksSlot);
            if (remaining > 0)
                runtime.Blackboard.WriteInt(
                    BurstRemainingTicksSlot,
                    remaining - 1);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner) =>
            Release(runtime, owner);
        public override void ClearForDeath(BuffRuntime runtime, Unit owner) =>
            Release(runtime, owner);
        public override void ClearForDespawn(BuffRuntime runtime, Unit owner) =>
            Release(runtime, owner);
        public override void ClearForRespawn(BuffRuntime runtime, Unit owner) =>
            Apply(runtime, owner);

        private void Apply(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null ||
                owner.World == null ||
                !owner.World.TryGetUnit(runtime.SourceUnitUid, out Unit source))
                return;
            int level = source.AbilityHandler?
                .GetAbilityLevelById(runtime.Source.SourceConfigId) ?? 0;
            fp peak = PeakValueByAbilityLevel.Resolve(level);
            int remaining = runtime.Blackboard.ReadIntOrDefault(
                BurstRemainingTicksSlot);
            fp ratio = DecayTicks > 0
                ? fpmath.clamp((fp)remaining / (fp)DecayTicks, fp.zero, fp.one)
                : fp.zero;
            fp value = peak * ratio;
            if (runtime.Blackboard.TryGetStatHandle(
                    HandleSlot,
                    out StatModifierHandle handle))
            {
                owner.StatHandler.SetModifierValue(handle, value);
            }
            else
            {
                handle = owner.StatHandler.AddModifier(
                    StatId,
                    Operation,
                    value);
                runtime.Blackboard.WriteStatHandle(HandleSlot, handle);
            }
        }

        private void Release(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler != null &&
                runtime.Blackboard.TryGetStatHandle(
                    HandleSlot,
                    out StatModifierHandle handle) &&
                handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(handle);
            }
            runtime.Blackboard.WriteStatHandle(HandleSlot, default);
        }
    }
}
