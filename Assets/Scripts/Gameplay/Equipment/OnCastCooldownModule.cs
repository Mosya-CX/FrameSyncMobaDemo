using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment module that restores cast resource (mana) and/or reduces
    /// remaining ability cooldowns when the owner casts an ability.
    /// Invoke timing: AbilityCast.
    /// </summary>
    [System.Serializable]
    public sealed class OnCastCooldownModule : EquipmentEffectModule
    {
        /// <summary>Flat cast resource (mana) restored on cast.</summary>
        public fp ManaRestore;

        /// <summary>
        /// Percentage (0.0-1.0) of total cooldown removed from remaining cooldown
        /// of the ability that was just cast.
        /// 0.25 = reduce remaining cooldown by 25 percent of the total cooldown.
        /// Applied to all ability slots.
        /// </summary>
        public fp CooldownReductionPercent;

        /// <summary>Minimum ticks between activations (0 = every cast).</summary>
        public int InternalCooldownTicks;

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            if (owner == null) return;

            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 &&
                currentTick < state.InternalCooldownReadyTick)
                return;

            // Mana restore
            if (ManaRestore > fp.zero && owner.StatHandler != null)
            {
                fp currentMana = owner.StatHandler.CurrentCastResource;
                fp newMana = currentMana + ManaRestore;
                owner.StatHandler.SetCurrentCastResource(newMana);
            }

            // Cooldown reduction for all abilities on cooldown
            if (CooldownReductionPercent > fp.zero && owner.AbilityHandler != null)
            {
            owner.AbilityHandler?.ApplyCooldownReductionPercent(
                    CooldownReductionPercent, currentTick);
            }

            state.InternalCooldownReadyTick = checked(
                currentTick + InternalCooldownTicks);
        }
    }
}
