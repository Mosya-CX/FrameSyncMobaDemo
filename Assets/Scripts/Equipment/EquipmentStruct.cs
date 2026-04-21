using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public enum EquipmentTriggerType : byte
{
    None,
    OnDamageDealt,
    OnDamageTaken,
    OnAttackPerformed,
    OnAbilityCastStage,
    OnKill,
    OnAssist,
    OnDeath,
    OnHealDealt,
    OnHealTaken,
}

public readonly struct EquipmentContextKey<T>
{
    public readonly string Name;

    public EquipmentContextKey(string name)
    {
        Name = name;
    }
}

public sealed class EquipmentContext
{
    private readonly Dictionary<string, object> data = new();

    public EquipmentContextKey<T> Set<T>(string keyName, T value)
    {
        var key = new EquipmentContextKey<T>(keyName);
        data[key.Name] = value;
        return key;
    }

    public void Set<T>(EquipmentContextKey<T> key, T value)
    {
        data[key.Name] = value;
    }

    public T Get<T>(EquipmentContextKey<T> key)
    {
        return (T)data[key.Name];
    }

    public T Get<T>(string keyName)
    {
        return (T)data[keyName];
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

    public bool TryGet<T>(string keyName, out T value)
    {
        if (data.TryGetValue(keyName, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public T Take<T>(EquipmentContextKey<T> key)
    {
        var value = (T)data[key.Name];
        data.Remove(key.Name);
        return value;
    }

    public T Take<T>(string keyName)
    {
        var value = (T)data[keyName];
        data.Remove(keyName);
        return value;
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

    public bool TryTake<T>(string keyName, out T value)
    {
        if (data.TryGetValue(keyName, out var obj) && obj is T t)
        {
            data.Remove(keyName);
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public bool Contains(string keyName)
    {
        return data.ContainsKey(keyName);
    }

    public void Remove(string keyName)
    {
        data.Remove(keyName);
    }

    public void Clear()
    {
        data.Clear();
    }

    public EquipmentContext Clone()
    {
        var clone = new EquipmentContext();
        foreach (var kv in data)
            clone.data[kv.Key] = kv.Value;
        return clone;
    }
}

[Serializable]
public struct EquipmentStatModifierData
{
    public UnitStatType Type;
    public fp Value;
    public StatModifierType Mode;
}