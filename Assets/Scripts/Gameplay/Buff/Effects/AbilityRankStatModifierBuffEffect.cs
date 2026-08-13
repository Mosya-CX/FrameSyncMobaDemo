using System;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class AbilityRankStatModifierBuffEffect : BuffEffect
    {
        public StatId StatId;
        public StatModifierOperation Operation;
        public AbilityLevelValue ValueByAbilityLevel;
        public bool NegateValue;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[] RequiredSlotDefinitions =>
            new[]
            {
                new BuffStateSlotDefinition
                {
                    SlotId = HandleSlot,
                    Kind = BuffValueKind.StatModifierHandle,
                },
            };

        public override void OnAdded(BuffRuntime runtime, Unit owner) =>
            Apply(runtime, owner);

        public override void OnReapplied(BuffRuntime runtime, Unit owner) =>
            Apply(runtime, owner);

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
            var value = ValueByAbilityLevel.Resolve(level);
            if (NegateValue)
                value = -value;
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
