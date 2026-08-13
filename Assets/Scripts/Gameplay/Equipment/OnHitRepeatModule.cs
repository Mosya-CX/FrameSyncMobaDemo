namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Requests one repeat of eligible On-Hit effects after a stable number of
    /// real attack hits while a required Buff is at full stacks. The repeat is
    /// not an attack and cannot advance this module again.
    /// </summary>
    [System.Serializable]
    public sealed class OnHitRepeatModule : EquipmentEffectModule
    {
        public BuffConfigId RequiredBuffConfigId;
        public int RequiredStacks = 1;
        public int TriggerEvery = 3;

        public override bool CanExecute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            return context.Owner?.BuffHandler != null &&
                   context.Dispatch != null &&
                   RequiredBuffConfigId.IsValid &&
                   RequiredStacks > 0 &&
                   TriggerEvery > 0;
        }

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            if (context.OnHit.IsRepeated)
                return;

            int stacks = context.Owner.BuffHandler.TryGetRuntime(
                RequiredBuffConfigId,
                out BuffRuntime runtime)
                    ? runtime.CurrentStacks
                    : 0;
            if (stacks < RequiredStacks)
            {
                state.TriggerCount = 0;
                return;
            }

            state.TriggerCount = checked(state.TriggerCount + 1);
            if (state.TriggerCount < TriggerEvery)
                return;

            state.TriggerCount = 0;
            context.Dispatch.RequestRepeatedOnHit();
        }
    }
}
