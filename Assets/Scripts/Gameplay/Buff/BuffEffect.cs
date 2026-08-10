using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static buff effect module with fixed lifecycle execution paths
    /// (design v14.2 6.1). Runtime state (handles, counters) lives only in
    /// BuffRuntime.Blackboard slots declared through RequiredSlotDefinitions.
    /// </summary>
    public abstract class BuffEffect
    {
        public abstract void OnAdded(
            BuffRuntime runtime,
            Unit owner);

        public abstract void OnRemoved(
            BuffRuntime runtime,
            Unit owner);

        /// <summary>
        /// Called after the runtime has been fully removed from the store
        /// (after OnRemoved and Blackboard invalidation). Effects may re-apply
        /// a successor Buff here without colliding with the removed runtime.
        /// </summary>
        public virtual void OnRemovedComplete(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void OnReapplied(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void OnStackChanged(
            BuffRuntime runtime,
            Unit owner,
            int oldStacks,
            int newStacks)
        {
        }

        public virtual void OnTick(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void ClearForRespawn(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        /// <summary>
        /// Releases this effect's handles without running the Gameplay Removed
        /// reaction (design v14.2 1.10).
        /// </summary>
        public virtual void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        /// <summary>
        /// Blackboard slots this effect requires, with kind and default.
        /// Used to derive the layout when BuffDefinition.BlackboardLayout is
        /// not authored explicitly.
        /// </summary>
        public virtual BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                Array.Empty<BuffStateSlotDefinition>();

        public virtual void OnDamageTaken(
            BuffRuntime runtime,
            Unit owner,
            in DamageEventData data)
        {
        }

        public virtual void OnDamageDealt(
            BuffRuntime runtime,
            Unit owner,
            in DamageEventData data)
        {
        }

        public virtual void OnHealTaken(
            BuffRuntime runtime,
            Unit owner,
            in HealEventData data)
        {
        }

        public virtual void OnHealDealt(
            BuffRuntime runtime,
            Unit owner,
            in HealEventData data)
        {
        }

        public virtual void OnShieldApplied(
            BuffRuntime runtime,
            Unit owner,
            in ShieldEventData data)
        {
        }

        public virtual void OnAbilityCast(
            BuffRuntime runtime,
            Unit owner,
            in AbilityCastEventData data)
        {
        }

        public virtual void OnLevelUp(
            BuffRuntime runtime,
            Unit owner,
            int previousLevel,
            int newLevel)
        {
        }

        public virtual void OnUnitDying(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void OnUnitDeath(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public virtual void OnUnitKill(
            BuffRuntime runtime,
            Unit owner,
            Unit victim)
        {
        }

        public virtual void OnUnitAssist(
            BuffRuntime runtime,
            Unit owner,
            Unit victim)
        {
        }

        public virtual void OnHitDealt(
            BuffRuntime runtime,
            Unit owner,
            in OnHitEventData data)
        {
        }

        public virtual void OnUnitCollisionEnter(
            BuffRuntime runtime,
            Unit owner,
            in UnitCollisionEnterEvent data)
        {
        }

        public virtual void OnUnitCollisionExit(
            BuffRuntime runtime,
            Unit owner,
            in UnitCollisionExitEvent data)
        {
        }
    }

    /// <summary>
    /// Stat modifier module with per-stack value scaling
    /// (design v14.2 9.2). The handle lives in one blackboard slot.
    /// </summary>
    public sealed class StatModifierBuffEffect :
        BuffEffect
    {
        public StatId StatId;
        public StatModifierOperation Operation;
        public fp BaseValue;
        public fp ValuePerStack;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = HandleSlot,
                        Kind =
                            BuffValueKind
                                .StatModifierHandle,
                    },
                };

        private fp ComputeValue(int stacks)
        {
            int perStack =
                stacks > 0 ? stacks - 1 : 0;
            return BaseValue +
                ValuePerStack * perStack;
        }

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null)
                return;
            var handle = owner.StatHandler
                .AddModifier(
                    StatId,
                    Operation,
                    ComputeValue(
                        runtime.CurrentStacks));
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                handle);
        }

        public override void OnStackChanged(
            BuffRuntime runtime,
            Unit owner,
            int oldStacks,
            int newStacks)
        {
            if (owner?.StatHandler == null)
                return;
            if (!runtime.Blackboard
                    .TryGetStatHandle(
                        HandleSlot,
                        out var handle))
                return;
            owner.StatHandler.SetModifierValue(
                handle,
                ComputeValue(newStacks));
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForRespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null)
                return;
            if (runtime.Blackboard
                    .TryGetStatHandle(
                        HandleSlot,
                        out var existing) &&
                existing.IsValid)
                return;
            var handle = owner.StatHandler
                .AddModifier(
                    StatId,
                    Operation,
                    ComputeValue(
                        runtime.CurrentStacks));
            runtime.Blackboard.WriteStatHandle(
                HandleSlot,
                handle);
        }

        private void ReleaseHandle(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null)
                return;
            if (runtime.Blackboard
                    .TryGetStatHandle(
                        HandleSlot,
                        out var handle) &&
                handle.IsValid)
            {
                owner.StatHandler
                    .RemoveModifier(handle);
                runtime.Blackboard.WriteStatHandle(
                    HandleSlot,
                    default);
            }
        }
    }

    /// <summary>
    /// Combat modifier module (design v14.2 9.5). The handle lives in one
    /// blackboard slot.
    /// </summary>
    public sealed class CombatModifierBuffEffect :
        BuffEffect
    {
        public CombatModifierRecord Record;
        public BuffStateSlotId HandleSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = HandleSlot,
                        Kind =
                            BuffValueKind
                                .CombatModifierHandle,
                    },
                };

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.CombatModifiers == null ||
                Record == null)
                return;
            var handle = owner.CombatModifiers
                .Attach(Record);
            runtime.Blackboard.WriteCombatHandle(
                HandleSlot,
                handle);
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandle(runtime, owner);
        }

        public override void ClearForRespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.CombatModifiers == null ||
                Record == null)
                return;
            if (runtime.Blackboard
                    .TryGetCombatHandle(
                        HandleSlot,
                        out var existing) &&
                existing.IsValid)
                return;
            var handle = owner.CombatModifiers
                .Attach(Record);
            runtime.Blackboard.WriteCombatHandle(
                HandleSlot,
                handle);
        }

        private void ReleaseHandle(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.CombatModifiers == null)
                return;
            if (runtime.Blackboard
                    .TryGetCombatHandle(
                        HandleSlot,
                        out var handle) &&
                handle.IsValid)
            {
                owner.CombatModifiers
                    .Detach(handle);
                runtime.Blackboard.WriteCombatHandle(
                    HandleSlot,
                    default);
            }
        }
    }
}
