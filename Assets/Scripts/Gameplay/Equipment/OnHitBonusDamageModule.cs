using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment module that deals bonus damage when the owner hits an enemy.
    /// Invoke timing: OnHitDealt.
    /// Uses EquipmentEffectDispatch context to determine the target.
    /// Has an internal cooldown to prevent per-hit overflow.
    /// </summary>
    [System.Serializable]
    public sealed class OnHitBonusDamageModule : EquipmentEffectModule
    {
        /// <summary>Bonus damage per hit.</summary>
        public fp BonusDamage;

        /// <summary>Damage type of the bonus damage.</summary>
        public DamageType DamageType = DamageType.Physical;

        /// <summary>Minimum ticks between activations (0 = every hit).</summary>
        public int InternalCooldownTicks;

        public override void Execute(Unit owner, EquipmentInstance instance)
        {
            if (owner?.World?.CombatSystem == null || BonusDamage <= fp.zero)
                return;

            // Internal cooldown via module state
            var state = FindOrCreateModuleState(instance);
            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 && currentTick < state.NextExecuteTick)
                return;

            // Get target from dispatch context
            var dispatch = GetDispatch(owner);
            if (dispatch == null) return;
            UnitUid targetUid = dispatch.LastOnHit.TargetUid;
            if (!targetUid.IsValid()) return;

            // Deal bonus damage
            var request = new DamageRequest
            {
                Header = CombatRequestHeader.Create(
                    owner.UnitUid,
                    targetUid,
                    CombatSourceType.AttackEffect,
                    instance.Definition?.Id ?? 0,
                    instance.Definition?.Id ?? 0),
                BaseDamage = BonusDamage,
                DamageType = DamageType,
            };
            owner.World.CombatSystem.SubmitDamage(request);

            // Update cooldown
            state.NextExecuteTick = currentTick + InternalCooldownTicks;
        }

        private static EquipmentEffectDispatch GetDispatch(Unit owner)
        {
            // Access via internal field on EquipmentHandler
            return owner?.EquipmentHandler?.GetEffectDispatch();
        }

        private EquipmentEffectModuleRuntimeState FindOrCreateModuleState(EquipmentInstance instance)
        {
            if (instance?.EffectRuntimes == null)
                return default;

            for (int fxIdx = 0; fxIdx < instance.EffectRuntimes.Length; fxIdx++)
            {
                var fx = instance.EffectRuntimes[fxIdx];
                if (fx?.Definition?.Modules == null) continue;

                for (int modIdx = 0; modIdx < fx.Definition.Modules.Length; modIdx++)
                {
                    if (ReferenceEquals(fx.Definition.Modules[modIdx], this))
                    {
                        if (fx.ModuleStates != null && modIdx < fx.ModuleStates.Length)
                        {
                            var s = fx.ModuleStates[modIdx];
                            fx.ModuleStates[modIdx] = s; // write back
                            return s;
                        }
                        return default;
                    }
                }
            }

            return default;
        }
    }
}
