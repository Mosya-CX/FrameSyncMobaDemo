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

        public override void Execute(Unit owner, EquipmentInstance instance)
        {
            if (owner == null) return;

            // Internal cooldown
            var state = FindModuleState(instance);
            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 && currentTick < state.NextExecuteTick)
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
                owner.AbilityHandler.ApplyCooldownReductionPercent(
                    CooldownReductionPercent, currentTick);
            }

            state.NextExecuteTick = currentTick + InternalCooldownTicks;
        }

        private EquipmentEffectModuleRuntimeState FindModuleState(EquipmentInstance instance)
        {
            if (instance?.EffectRuntimes == null)
                return default;

            for (int fxIdx = 0; fxIdx < instance.EffectRuntimes.Length; fxIdx++)
            {
                var fx = instance.EffectRuntimes[fxIdx];
                if (fx?.Definition?.Modules == null) continue;

                for (int modIdx = 0; modIdx < fx.Definition.Modules.Length; modIdx++)
                {
                    if (ReferenceEquals(fx.Definition.Modules[modIdx], this) &&
                        fx.ModuleStates != null && modIdx < fx.ModuleStates.Length)
                    {
                        var s = fx.ModuleStates[modIdx];
                        fx.ModuleStates[modIdx] = s;
                        return s;
                    }
                }
            }

            return default;
        }
    }
}
