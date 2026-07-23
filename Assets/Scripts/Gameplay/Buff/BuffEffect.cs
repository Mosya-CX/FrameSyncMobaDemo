using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public abstract class BuffEffect
    {
        public abstract void OnAdded(BuffRuntime runtime, Unit owner);
        public abstract void OnRemoved(BuffRuntime runtime, Unit owner);
        public virtual void OnStackChanged(BuffRuntime runtime, Unit owner, int oldStacks, int newStacks) { }

        public virtual void ClearForDeath(BuffRuntime runtime, Unit owner) { }

        public virtual void ClearForRespawn(BuffRuntime runtime, Unit owner) { }
        public virtual void OnDamageTaken(BuffRuntime runtime, Unit owner, in DamageEventData data) { }
        public virtual void OnDamageDealt(BuffRuntime runtime, Unit owner, in DamageEventData data) { }
        public virtual void OnHealTaken(BuffRuntime runtime, Unit owner, in HealEventData data) { }
        public virtual void OnHealDealt(BuffRuntime runtime, Unit owner, in HealEventData data) { }
        public virtual void OnShieldApplied(BuffRuntime runtime, Unit owner, in ShieldEventData data) { }
        public virtual void OnUnitDying(BuffRuntime runtime, Unit owner) { }
        public virtual void OnUnitDeath(BuffRuntime runtime, Unit owner) { }
        public virtual void OnUnitKill(BuffRuntime runtime, Unit owner, Unit victim) { }
        public virtual void OnUnitCollisionEnter(
            BuffRuntime runtime,
            Unit owner,
            in UnitCollisionEnterEvent data) { }
        public virtual void OnUnitCollisionExit(
            BuffRuntime runtime,
            Unit owner,
            in UnitCollisionExitEvent data) { }
    }

    public sealed class StatModifierBuffEffect : BuffEffect
    {
        public StatId StatId;
        public StatModifierOperation Operation;
        public fp Value;
        public string SlotKey = "_stat";

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null) return;
            var handle = owner.StatHandler.AddModifier(StatId, Operation, Value);
            runtime.Blackboard.SetStatHandle(SlotKey, handle);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null) return;
            if (runtime.Blackboard.TryGetStatHandle(SlotKey, out var handle) && handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(handle);
            }
        }

        public override void ClearForDeath(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null) return;
            if (runtime.Blackboard.TryGetStatHandle(SlotKey, out var handle) && handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(handle);
                runtime.Blackboard.SetStatHandle(SlotKey, default);
            }
        }

        public override void ClearForRespawn(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null) return;
            if (runtime.Blackboard.TryGetStatHandle(SlotKey, out var existing) && existing.IsValid)
                return;
            var handle = owner.StatHandler.AddModifier(StatId, Operation, Value);
            runtime.Blackboard.SetStatHandle(SlotKey, handle);
        }
    }

    public sealed class CombatModifierBuffEffect : BuffEffect
    {
        public CombatModifierRecord Record;
        public string SlotKey = "_combat";

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            if (owner?.CombatModifiers == null || Record == null) return;
            var handle = owner.CombatModifiers.Attach(Record);
            runtime.Blackboard.SetCombatHandle(SlotKey, handle);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner)
        {
            if (owner?.CombatModifiers == null) return;
            if (runtime.Blackboard.TryGetCombatHandle(SlotKey, out var handle) && handle.IsValid)
            {
                owner.CombatModifiers.Detach(handle);
            }
        }

        public override void ClearForDeath(BuffRuntime runtime, Unit owner)
        {
            if (owner?.CombatModifiers == null) return;
            if (runtime.Blackboard.TryGetCombatHandle(SlotKey, out var handle) && handle.IsValid)
            {
                owner.CombatModifiers.Detach(handle);
                runtime.Blackboard.SetCombatHandle(SlotKey, default);
            }
        }

        public override void ClearForRespawn(BuffRuntime runtime, Unit owner)
        {
            if (owner?.CombatModifiers == null || Record == null) return;
            if (runtime.Blackboard.TryGetCombatHandle(SlotKey, out var existing) && existing.IsValid)
                return;
            var handle = owner.CombatModifiers.Attach(Record);
            runtime.Blackboard.SetCombatHandle(SlotKey, handle);
        }
    }
}
