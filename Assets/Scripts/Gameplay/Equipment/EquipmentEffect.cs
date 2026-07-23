using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment effect definition with invoke timing modules (Equipment/Gold v12 §3).
    /// Plain serializable class — ScriptableObject wrappers deferred to authoring layer.
    /// </summary>
    [Serializable]
    public sealed class EquipmentEffectDef
    {
        public string Name;
        public string Description;
        public bool IsActive;
        public EquipmentActiveSettings ActiveSettings;
        public EquipmentEffectModule[] Modules;

        public bool IsValid => Modules != null && Modules.Length > 0;
    }

    /// <summary>
    /// Active equipment use settings (cooldown, charge cost, targeting).
    /// </summary>
    [Serializable]
    public struct EquipmentActiveSettings
    {
        public int CooldownTicks;
        public int ChargeCost;
        public EquipmentCooldownGroupId SharedCooldownGroup;
    }

    /// <summary>
    /// When an equipment effect module executes.
    /// </summary>
    public enum EquipmentEffectInvokeTiming : byte
    {
        OnEquipped = 0,
        OnUnequipped = 1,
        Tick = 2,
        DamageTaken = 3,
        DamageDealt = 4,
        HealTaken = 5,
        HealDealt = 6,
        AbilityCast = 7,
        UnitDying = 8,
        UnitDeath = 9,
        UnitKill = 10,
        DynamicStatModifier = 11,
        CombatModifier = 12,
        ActiveUse = 13,
    }

    /// <summary>
    /// Abstract base for equipment effect modules. Modules hold static configuration;
    /// runtime state lives in EquipmentEffectModuleRuntimeState.
    /// </summary>
    [Serializable]
    public abstract class EquipmentEffectModule
    {
        public EquipmentEffectInvokeTiming[] InvokeTimings;

        public virtual bool CanExecute() => true;
        public abstract void Execute(Unit owner, EquipmentInstance instance);
    }

    /// <summary>
    /// Per-instance effect runtime holding module states.
    /// </summary>
    public sealed class EquipmentEffectRuntime
    {
        public EquipmentEffectDef Definition;
        public EquipmentEffectModuleRuntimeState[] ModuleStates;

        public EquipmentEffectRuntime(EquipmentEffectDef definition)
        {
            Definition = definition;
            if (definition?.Modules != null)
            {
                ModuleStates = new EquipmentEffectModuleRuntimeState[definition.Modules.Length];
            }
            else
            {
                ModuleStates = Array.Empty<EquipmentEffectModuleRuntimeState>();
            }
        }
    }

    /// <summary>
    /// Per-module runtime state (tick counters, cooldowns).
    /// </summary>
    public struct EquipmentEffectModuleRuntimeState
    {
        public int NextExecuteTick;
        public int InternalCooldownReadyTick;
        public int StackCount;
        public int TimerTicks;
    }
}
