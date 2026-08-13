using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment module that heals the owner when they kill an enemy unit.
    /// Invoke timing: UnitKill.
    /// Uses EquipmentEffectDispatch context to determine the kill event.
    /// </summary>
    [System.Serializable]
    public sealed class OnKillHealModule : EquipmentEffectModule
    {
        /// <summary>Heal amount on kill (absolute HP).</summary>
        public fp HealAmount;

        /// <summary>Minimum ticks between activations (0 = every kill).</summary>
        public int InternalCooldownTicks;

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            if (owner?.World?.CombatSystem == null)
                return;

            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 &&
                currentTick < state.InternalCooldownReadyTick)
                return;

            // Heal on kill
            if (HealAmount > fp.zero)
            {
                var request = new HealRequest
                {
                    TargetUnitUid = owner.UnitUid,
                    SourceUnitUid = owner.UnitUid,
                    BaseValue = HealAmount,
                };
                owner.World.CombatSystem.SubmitHeal(request);
            }

            state.InternalCooldownReadyTick = checked(
                currentTick + InternalCooldownTicks);
        }
    }
}
