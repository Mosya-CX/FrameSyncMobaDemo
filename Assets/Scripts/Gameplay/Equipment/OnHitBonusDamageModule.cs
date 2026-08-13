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

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            EquipmentInstance instance = context.Instance;
            if (owner?.World?.CombatSystem == null || BonusDamage <= fp.zero)
                return;

            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 &&
                currentTick < state.InternalCooldownReadyTick)
                return;

            UnitUid targetUid = context.OnHit.TargetUid;
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
            state.InternalCooldownReadyTick = checked(
                currentTick + InternalCooldownTicks);
        }
    }
}
