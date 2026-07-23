using System.Collections.Generic;
using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Applies equipment passive effects on purchase and cleans them up on removal.
    /// Handles Buff creation, Modifier registration, and persistent effect runtimes.
    /// (Equipment/Gold v12 §1.7, §3)
    /// </summary>
    public static class EquipmentPassiveApplier
    {
        /// <summary>
        /// Applies all passive effects of an equipment definition to the owner.
        /// Called when equipment is first added to a slot.
        /// </summary>
        public static void ApplyOnEquip(Unit owner, EquipmentDefinition definition, EquipmentInstance instance)
        {
            if (owner == null || definition == null) return;

            // Fixed stats are already applied by EquipmentHandler.Add
            // Here we handle any additional passive effects that require runtime creation

            if (definition.Effects == null) return;

            for (int i = 0; i < definition.Effects.Length; i++)
            {
                var effectDef = definition.Effects[i];
                if (effectDef == null || effectDef.Modules == null) continue;

                // Execute OnEquipped-timed modules
                for (int m = 0; m < effectDef.Modules.Length; m++)
                {
                    var module = effectDef.Modules[m];
                    if (module?.InvokeTimings == null) continue;
                    for (int t = 0; t < module.InvokeTimings.Length; t++)
                    {
                        if (module.InvokeTimings[t] == EquipmentEffectInvokeTiming.OnEquipped
                            && module.CanExecute())
                        {
                            module.Execute(owner, instance);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all passive effects of an equipment instance from the owner.
        /// Called when equipment is removed from a slot (sell, undo, component consumption).
        /// </summary>
        public static void RemoveOnUnequip(Unit owner, EquipmentInstance instance)
        {
            if (owner == null || instance?.Definition?.Effects == null) return;

            for (int i = 0; i < instance.Definition.Effects.Length; i++)
            {
                var effectDef = instance.Definition.Effects[i];
                if (effectDef == null || effectDef.Modules == null) continue;

                // Execute OnUnequipped-timed modules
                for (int m = 0; m < effectDef.Modules.Length; m++)
                {
                    var module = effectDef.Modules[m];
                    if (module?.InvokeTimings == null) continue;
                    for (int t = 0; t < module.InvokeTimings.Length; t++)
                    {
                        if (module.InvokeTimings[t] == EquipmentEffectInvokeTiming.OnUnequipped
                            && module.CanExecute())
                        {
                            module.Execute(owner, instance);
                        }
                    }
                }
            }

            // Release runtime handles
            if (instance._fixedStatHandles != null && owner.StatHandler != null)
            {
                for (int i = 0; i < instance._fixedStatHandles.Length; i++)
                {
                    var handle = instance._fixedStatHandles[i];
                    if (handle.IsValid)
                        owner.StatHandler.RemoveModifier(handle);
                }
                instance._fixedStatHandles = null;
            }
        }

        /// <summary>
        /// Rebuilds all passive effect handles for the current life stage.
        /// Called during respawn to re-register stats and effects.
        /// </summary>
        public static void RebuildForRespawn(Unit owner, EquipmentInstance instance)
        {
            if (owner == null || instance?.Definition == null) return;
            var definition = instance.Definition;

            // Re-register fixed stats
            if (!definition.IsBaked)
                throw new InvalidOperationException($"Equipment {definition.Id} must be baked before runtime use.");
            if (definition.BakedFixedStats != null && owner.StatHandler != null)
            {
                var handles = new List<StatModifierHandle>();
                for (int i = 0; i < definition.BakedFixedStats.Length; i++)
                {
                    var fs = definition.BakedFixedStats[i];
                    var handle = owner.StatHandler.AddModifier(
                        fs.Stat, StatModifierOperation.FlatAdd, fs.Value);
                    handles.Add(handle);
                }
                instance._fixedStatHandles = handles.ToArray();
            }

            // Rebuild EffectRuntimes
            if (definition.Effects != null && definition.Effects.Length > 0)
            {
                instance.EffectRuntimes = new EquipmentEffectRuntime[definition.Effects.Length];
                for (int i = 0; i < definition.Effects.Length; i++)
                {
                    instance.EffectRuntimes[i] = new EquipmentEffectRuntime(definition.Effects[i]);
                }
            }
        }
    }
}
