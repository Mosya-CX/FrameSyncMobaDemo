using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class EquipmentInfo// 装备的运行时实例(多个相同的装备都有独立的EquipmentInfo)
{
    public readonly EquipmentData data;
    public EquipmentHandler handler;
    
    public List<ModifierHandle> statModifierHandlers = new();
    public EquipmentEffectRuntime EffectRuntime;

    public EquipmentInfo(EquipmentData data, EquipmentHandler handler)
    {
        this.data = data;
        this.handler = handler;
    }
}

public class EquipmentEffectRuntime// 每个装备的唯一运行时实例(多个相同的装备共享一个EquipmentEffectRuntime)
{
    public readonly EquipmentData data;
    public EquipmentHandler handler;
    public EquipmentContext context = new();

    private Dictionary<EquipmentBaseEffect, fp> effectTable = new();
    private List<DamageCallback> effectCallbacks = new();

    public fp cooldownMultiplier = 1;// 冷却缩减

    public int stackCount;// 层数
    public int charge;  // 充能层数

    public int referenceCount;

    public EquipmentEffectRuntime(EquipmentData data, EquipmentHandler handler)
    {
        this.data = data;
        this.handler = handler;

        if (data.PassiveEffects != null)
            foreach (var effect in data.PassiveEffects)
                effectTable[effect] = 0;

        if (data.ActiveEffect != null)
            effectTable[data.ActiveEffect] = 0;
    }

    public void OnCreate()
    {
        foreach (var effect in effectTable.Keys)
            effect.OnEquip(this);  
    }

    public void Tick(fp dt)
    {
        foreach (var effect in effectTable.Keys)
        {
            if (effectTable[effect] > 0)
                effectTable[effect] -= dt;

            effect.OnTick(this, dt);
        }
    }

    public void OnRemove()
    {
        foreach (var effect in effectTable.Keys)
            effect.OnUnequip(this);
        effectTable.Clear();
    }

    public bool ApplyEffect(EquipmentBaseEffect effect)
    {
        if (effectTable.ContainsKey(effect) && !(effectTable[effect] > 0) && effect.Apply(this))
        {
            effectTable[effect] = ((fp)effect.BaseCooldown) * cooldownMultiplier;
            return true;
        }
        return false;
    }
}

public readonly struct EquipmentContextKey<T>
{
    public readonly string Name;
    public EquipmentContextKey(string name) => Name = name;
}

public class EquipmentContext
{
    private readonly Dictionary<string, object> data = new();

    public EquipmentContextKey<T> Set<T>(string keyName, T value)
    {
        var contextKey = new EquipmentContextKey<T>(keyName);
        data[contextKey.Name] = value;
        return contextKey;
    }

    public void Set<T>(EquipmentContextKey<T> key, T value)
    {
        data[key.Name] = value;
    }

    public T Get<T>(EquipmentContextKey<T> key)
    {
        return (T)data[key.Name];
    }

    public bool TryGet<T>(EquipmentContextKey<T> key, out T value)
    {
        if (data.TryGetValue(key.Name, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public T Take<T>(EquipmentContextKey<T> key)
    {
        var value = data[key.Name];
        data.Remove(key.Name);
        return (T)value;
    }

    public bool TryTake<T>(EquipmentContextKey<T> key, out T value)
    {
        if (data.TryGetValue(key.Name, out var obj) && obj is T t)
        {
            data.Remove(key.Name);
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public void Remove(string keyName) => data.Remove(keyName);

    public void Clear() => data.Clear();
}

[System.Serializable]
public struct EquipmentStatModifierData
{
    public UnitStatType Type;
    public fp Value;
    public StatModifierType Mode; // Add / Multiply
}

public static class EquipmentEffectRegistry
{
    private static readonly Dictionary<int, EquipmentBaseEffect> _effects = new();

    // 注册效果（通常游戏启动时调用）
    public static void Register(int id, EquipmentBaseEffect effect)
    {
        if (_effects.ContainsKey(id))
            throw new Exception($"Effect id {id} already registered");
        _effects[id] = effect;
    }

    // 获取效果实例（所有装备共用同一个逻辑实例）
    public static EquipmentBaseEffect Get(int id)
    {
        if (!_effects.TryGetValue(id, out var effect))
            throw new Exception($"Effect {id} not registered");
        return effect;
    }
}