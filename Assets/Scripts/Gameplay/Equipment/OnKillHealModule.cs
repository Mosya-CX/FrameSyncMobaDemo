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

        public override void Execute(Unit owner, EquipmentInstance instance)
        {
            if (owner?.World?.CombatSystem == null)
                return;

            // Internal cooldown
            var state = FindModuleState(instance);
            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 && currentTick < state.NextExecuteTick)
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
