using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Compile-time fixed routing for the Unit events supported by current
    /// handlers. It owns no subscription state and therefore has no snapshot.
    /// </summary>
    public sealed class UnitEventBus
    {
        private readonly Unit owner;

        public UnitEventBus(Unit owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        internal void PublishDamageTaken(in DamageEventData data)
        {
            owner.AbilityHandler?.OnDamageTaken(data);
            owner.BuffHandler?.OnDamageTaken(data);
            owner.EquipmentHandler?.OnDamageTaken(data);
        }

        internal void PublishDamageDealt(in DamageEventData data)
        {
            owner.AbilityHandler?.OnDamageDealt(data);
            owner.BuffHandler?.OnDamageDealt(data);
            owner.EquipmentHandler?.OnDamageDealt(data);
        }

        internal void PublishHealTaken(in HealEventData data)
        {
            owner.AbilityHandler?.OnHealTaken(data);
            owner.BuffHandler?.OnHealTaken(data);
            owner.EquipmentHandler?.OnHealTaken(data);
        }

        internal void PublishHealDealt(in HealEventData data)
        {
            owner.AbilityHandler?.OnHealDealt(data);
            owner.BuffHandler?.OnHealDealt(data);
            owner.EquipmentHandler?.OnHealDealt(data);
        }

        internal void PublishShieldApplied(in ShieldEventData data)
        {
            owner.BuffHandler?.OnShieldApplied(data);
        }

        internal void PublishUnitDying(Unit unit)
        {
            owner.AbilityHandler?.OnUnitDying(unit);
            owner.BuffHandler?.OnUnitDying(unit);
            owner.EquipmentHandler?.OnUnitDying(unit);
        }

        internal void PublishUnitDeath(Unit unit)
        {
            owner.AbilityHandler?.OnUnitDeath(unit);
            owner.BuffHandler?.OnUnitDeath(unit);
            owner.EquipmentHandler?.OnUnitDeath(unit);
        }

        internal void PublishUnitKill(Unit victim)
        {
            owner.AbilityHandler?.OnUnitKill(victim);
            owner.BuffHandler?.OnUnitKill(victim);
            owner.EquipmentHandler?.OnUnitKill(victim);
        }

        internal void PublishLevelUp(int previousLevel, int newLevel)
        {
            owner.AbilityHandler?.OnLevelUp(previousLevel, newLevel);
        }

        internal void PublishUnitCollisionEnter(in UnitCollisionEnterEvent data)
        {
            owner.BuffHandler?.OnUnitCollisionEnter(data);
        }

        internal void PublishUnitCollisionExit(in UnitCollisionExitEvent data)
        {
            owner.BuffHandler?.OnUnitCollisionExit(data);
        }

        internal void Clear()
        {
            // There is intentionally no dynamic routing state to clear.
        }
    }
}
