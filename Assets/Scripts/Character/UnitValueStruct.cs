using Unity.Mathematics.FixedPoint;
using System.Collections.Generic;
using System;
using System.Linq;
using static UnitStat;
using UnityEngine;

public enum UnitStatType
{
    MaxHealth,
    MaxMana,

    AttackDamage,
    AbilityPower,

    Armor,
    MagicResist,

    AttackSpeed,
    AttackRange,

    CritChance,
    CritDamage,

    MoveSpeed,
    HealthRegen,
    ManaRegen,

    Tenacity,
    LifeSteal,
    SpellVamp,

    ArmorPenFlat,
    ArmorPenPercent,
    MagicPenFlat,
    MagicPenPercent,
}

[System.Serializable]
public class UnitStat
{
    private fp baseValue;
    private fp growth;
    private int level;

    private readonly List<StatModifier> flatModifiers = new(4);
    private readonly List<StatModifier> percentAddModifiers = new(4);
    private readonly List<StatModifier> percentMultModifiers = new(4);

    private fp finalValue;
    public fp FinalValue => finalValue;

    #region 初始化

    public void Init(fp baseValue, fp growth)
    {
        this.baseValue = baseValue;
        this.growth = growth;
        level = 1;
        Recalculate();
    }

    public void SetLevel(int level)
    {
        this.level = level;
        Recalculate();
    }

    #endregion

    #region Modifier管理

    public void AddModifier(StatModifier modifier)
    {
        switch (modifier.Type)
        {
            case StatModifierType.Flat:
                flatModifiers.Add(modifier);
                break;

            case StatModifierType.PercentAdd:
                percentAddModifiers.Add(modifier);
                break;

            case StatModifierType.PercentMult:
                percentMultModifiers.Add(modifier);
                break;
        }

        Recalculate();
    }

    public void RemoveModifier(ModifierHandle handle)
    {
        RemoveFromList(flatModifiers, handle);
        RemoveFromList(percentAddModifiers, handle);
        RemoveFromList(percentMultModifiers, handle);

        Recalculate();
    }

    private void RemoveFromList(List<StatModifier> list, ModifierHandle handle)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Handle.Equals(handle))
                list.RemoveAt(i);
        }
    }

    public void ClearModifiers()
    {
        flatModifiers.Clear();
        percentAddModifiers.Clear();
        percentMultModifiers.Clear();
        Recalculate();
    }

    #endregion

    #region 计算

    private void Recalculate()
    {
        // 成长值
        fp grown = baseValue + growth * (level - 1);

        // 固定值
        fp flatSum = fp.zero;
        for (int i = 0; i < flatModifiers.Count; i++)
        {
            flatSum += flatModifiers[i].Value;
        }

        // 百分比加算
        fp percentAddSum = fp.zero;
        for (int i = 0; i < percentAddModifiers.Count; i++)
        {
            percentAddSum += percentAddModifiers[i].Value;
        }

        // 百分比乘算
        fp percentMult = fp.one;
        for (int i = 0; i < percentMultModifiers.Count; i++)
        {
            percentMult *= (fp.one + percentMultModifiers[i].Value);
        }

        // 最终计算顺序固定
        finalValue = grown + flatSum;
        finalValue *= (fp.one + percentAddSum);
        finalValue *= percentMult;
    }

    #endregion

    #region 快照和恢复
    [System.Serializable]
    public struct UnitStatSnapshot
    {
        public fp baseValue;
        public fp growth;
        public int level;
        public List<StatModifierData> flatModifiers;
        public List<StatModifierData> percentAddModifiers;
        public List<StatModifierData> percentMultModifiers;
    }

    [System.Serializable]
    public struct StatModifierData
    {
        public int handleId;   
        public StatModifierType type;
        public fp value;
    }

    public UnitStatSnapshot Capture()
    {
        return new UnitStatSnapshot
        {
            baseValue = this.baseValue,
            growth = this.growth,
            level = this.level,
            flatModifiers = this.flatModifiers.Select(m => new StatModifierData { handleId = m.Handle.id, type = m.Type, value = m.Value }).ToList(),
            percentAddModifiers = this.percentAddModifiers.Select(m => new StatModifierData { handleId = m.Handle.id, type = m.Type, value = m.Value }).ToList(),
            percentMultModifiers = this.percentMultModifiers.Select(m => new StatModifierData { handleId = m.Handle.id, type = m.Type, value = m.Value }).ToList()
        };
    }

    public void Restore(UnitStatSnapshot snap)
    {
        this.baseValue = snap.baseValue;
        this.growth = snap.growth;
        this.level = snap.level;

        // 重建 modifier 列表
        this.flatModifiers.Clear();
        this.flatModifiers.AddRange(snap.flatModifiers.Select(d => new StatModifier(new ModifierHandle(d.handleId), d.type, d.value)));

        this.percentAddModifiers.Clear();
        this.percentAddModifiers.AddRange(snap.percentAddModifiers.Select(d => new StatModifier(new ModifierHandle(d.handleId), d.type, d.value)));

        this.percentMultModifiers.Clear();
        this.percentMultModifiers.AddRange(snap.percentMultModifiers.Select(d => new StatModifier(new ModifierHandle(d.handleId), d.type, d.value)));

        // 重新计算 finalValue
        Recalculate();
    }
    #endregion
}

public class UnitStats
{
    private readonly Dictionary<UnitStatType, UnitStat> stats = new();

    // ===== 当前值 =====
    private fp _currentHealth;
    public fp CurrentHealth => _currentHealth;

    private fp _currentMana;
    public fp CurrentMana => _currentMana;

    // ===== 派生属性 =====
    public fp PhysicalDamageReduction { get; private set; }
    public fp MagicDamageReduction { get; private set; }
    public fp RealMoveSpeed {  get; private set; }

    // 基准常熟
    private fp REAL_MOVE_SPEED_AMP => 0.017m;
    private fp ARMOR_BASE => 100;
    private fp MAGIC_RESIST_BASE => 100;
    #region 初始化

    public void Init(UnitPropertyConfig config)
    {
        Add(UnitStatType.MaxHealth, config.baseHealth, config.healthGrowth);
        Add(UnitStatType.MaxMana, config.baseMana, config.manaGrowth);

        Add(UnitStatType.AttackDamage, config.baseAttackDamage, config.attackGrowth);
        Add(UnitStatType.AbilityPower, config.baseAbilityPower, config.abilityGrowth);

        Add(UnitStatType.Armor, config.baseArmor, config.armorGrowth);
        Add(UnitStatType.MagicResist, config.baseMagicResist, config.magicResistGrowth);

        Add(UnitStatType.AttackSpeed, config.baseAttackSpeed, config.attackSpeedGrowth);
        Add(UnitStatType.AttackRange, config.baseAttackRange, 0);

        Add(UnitStatType.CritChance, config.baseCritChance, 0);
        Add(UnitStatType.CritDamage, config.baseCritDamage, 0);

        Add(UnitStatType.MoveSpeed, config.baseMoveSpeed, 0);
        Add(UnitStatType.HealthRegen, config.baseHealthRegen, 0);
        Add(UnitStatType.ManaRegen, config.baseManaRegen, 0);

        Add(UnitStatType.Tenacity, 0, 0);
        Add(UnitStatType.LifeSteal, 0, 0);
        Add(UnitStatType.SpellVamp, 0, 0);

        Add(UnitStatType.ArmorPenFlat, 0, 0);
        Add(UnitStatType.ArmorPenPercent, 0, 0);
        Add(UnitStatType.MagicPenFlat, 0, 0);
        Add(UnitStatType.MagicPenPercent, 0, 0);

        _currentHealth = Get(UnitStatType.MaxHealth);
        _currentMana = Get(UnitStatType.MaxMana);

        RecalculateDerived();
    }

    private void Add(UnitStatType type, float baseValue, float growth)
    {
        var stat = new UnitStat();
        stat.Init((fp)baseValue, (fp)growth);
        stats[type] = stat;
    }

    #endregion

    #region 对外接口

    public fp Get(UnitStatType type)
    {
        return stats[type].FinalValue;
    }

    public void AddModifier(UnitStatType type, StatModifier modifier)
    {
        stats[type].AddModifier(modifier);
        RecalculateDerived();
        ClampCurrentValues();
    }

    public void RemoveModifier(UnitStatType type, ModifierHandle handle)
    {
        stats[type].RemoveModifier(handle);
        RecalculateDerived();
        ClampCurrentValues();
    }

    public void RemoveModifierFromAllStats(ModifierHandle handle)
    {
        foreach (var stat in stats.Values)
            stat.RemoveModifier(handle);

        RecalculateDerived();
        ClampCurrentValues();
    }

    public void SetLevel(int level)
    {
        foreach (var stat in stats.Values)
            stat.SetLevel(level);

        ClampCurrentValues();
        RecalculateDerived();
    }

    #endregion

    public void ModifyHealth(fp delta)
    {
        _currentHealth += delta;
        ClampHealth();
    }

    public void ModifyMana(fp delta)
    {
        _currentMana += delta;
        ClampMana();
    }

    private void ClampHealth()
    {
        fp max = Get(UnitStatType.MaxHealth);
        if (_currentHealth > max) _currentHealth = max;
        if (_currentHealth < fp.zero) _currentHealth = fp.zero;
    }

    private void ClampMana()
    {
        fp max = Get(UnitStatType.MaxMana);
        if (_currentMana > max) _currentMana = max;
        if (_currentMana < fp.zero) _currentMana = fp.zero;
    }

    private void ClampCurrentValues()
    {
        ClampHealth();
        ClampMana();
    }

    private void RecalculateDerived()
    {
        fp armor = Get(UnitStatType.Armor);
        fp mr = Get(UnitStatType.MagicResist);
        fp ms = Get(UnitStatType.MoveSpeed);

        PhysicalDamageReduction = armor / (ARMOR_BASE + armor);
        MagicDamageReduction = mr / (MAGIC_RESIST_BASE + mr);
        RealMoveSpeed = REAL_MOVE_SPEED_AMP * ms;
    }

    public void Clean()
    {
        stats.Clear();
        _currentHealth = fp.zero;
        _currentMana = fp.zero;
        PhysicalDamageReduction = fp.zero;
        MagicDamageReduction = fp.zero;
    }

    #region 快照和恢复
    [System.Serializable]
    public struct UnitStatsSnapshot
    {
        public fp currentHealth;
        public fp currentMana;
        public Dictionary<UnitStatType, UnitStatSnapshot> statSnapshots;
    }
    public UnitStatsSnapshot Capture()
    {
        var statSnapshots = new Dictionary<UnitStatType, UnitStatSnapshot>();
        foreach (var kv in stats)
        {
            statSnapshots[kv.Key] = kv.Value.Capture();
        }

        return new UnitStatsSnapshot
        {
            currentHealth = _currentHealth,
            currentMana = _currentMana,
            statSnapshots = statSnapshots
        };
    }

    public void Restore(UnitStatsSnapshot snap)
    {
        // 直接设置当前值
        _currentHealth = snap.currentHealth;
        _currentMana = snap.currentMana;

        // 恢复每个属性的 UnitStat
        foreach (var kv in snap.statSnapshots)
        {
            if (stats.TryGetValue(kv.Key, out var stat))
                stat.Restore(kv.Value);
            else
                Debug.LogError($"UnitStat for {kv.Key} missing during restore");
        }

        // 重新计算派生值
        RecalculateDerived();
    }
    #endregion
}

public enum StatModifierType
{
    Flat,           // 直接加法
    PercentAdd,     // 加法百分比 (叠加)
    PercentMult     // 最终乘算 (逐个相乘)
}

public readonly struct StatModifier
{
    public readonly ModifierHandle Handle;
    public readonly StatModifierType Type;
    public readonly fp Value;

    public StatModifier(ModifierHandle handle, StatModifierType type, fp value)
    {
        Handle = handle;
        Type = type;
        Value = value;
    }
}

public readonly struct ModifierHandle : IEquatable<ModifierHandle>
{
    public readonly int id;

    public ModifierHandle(int id)
    {
        this.id = id;
    }

    public bool Equals(ModifierHandle other) => id == other.id;
    public override bool Equals(object obj) => obj is ModifierHandle other && Equals(other);
    public override int GetHashCode() => id;
}

public static class ModifierHandleGenerator
{
    private static int nextId = 1;

    public static ModifierHandle Create()
    {
        return new ModifierHandle(nextId++);
    }

    public static void Reset()
    {
        nextId = 1;
    }
}