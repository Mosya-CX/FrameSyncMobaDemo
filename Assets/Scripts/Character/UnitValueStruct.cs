using Unity.Mathematics.FixedPoint;
using System.Collections.Generic;   

public enum UnitStatType
{
    // ===== 生命系统 =====
    MaxHealth,          // 最大生命
    HealthRegen,        // 每秒生命回复

    // ===== 法力系统 =====
    MaxMana,            // 最大法力
    ManaRegen,          // 每秒法力回复

    // ===== 攻击 =====
    AttackDamage,       // 物理攻击
    AbilityPower,       // 法术强度
    AttackSpeed,        // 攻速
    AttackRange,        // 攻击距离

    // ===== 暴击 =====
    CritChance,         // 暴击率
    CritDamage,         // 暴击伤害倍数

    // ===== 防御 =====
    Armor,              // 护甲
    MagicResist,        // 魔抗

    // ===== 穿透 =====
    ArmorPenFlat,       // 固定护甲穿透
    ArmorPenPercent,    // 百分比护甲穿透
    MagicPenFlat,       // 固定魔法穿透
    MagicPenPercent,    // 百分比魔法穿透

    // ===== 功能 =====
    MoveSpeed,          // 移动速度
    CooldownReduction,  // 冷却缩减
    Tenacity,           // 韧性

    // ===== 吸血 =====
    LifeSteal,          // 生命偷取
    SpellVamp,          // 全能吸血
}

[System.Serializable]
public class UnitStat
{
    private fp baseValue;
    private fp growth;
    private int level;

    private fp flatBonus;
    private fp percentBonus;

    private fp finalValue;

    public fp FinalValue => finalValue;

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

    public void AddFlat(fp value)
    {
        flatBonus += value;
        Recalculate();
    }

    public void AddPercent(fp value)
    {
        percentBonus += value;
        Recalculate();
    }

    private void Recalculate()
    {
        fp grown = baseValue + growth * (level - 1);
        finalValue = (grown + flatBonus) * (fp.one + percentBonus);
    }
}

[System.Serializable]
public class UnitStats
{
    private Dictionary<UnitStatType, UnitStat> stats = new();

    // ===== 当前值 =====
    public fp CurrentHealth { get; private set; }
    public fp CurrentMana { get; private set; }

    // ===== 隐性数值 =====
    public fp PhysicalDamageReduction { get; private set; } // 物理减伤率
    public fp MagicDamageReduction { get; private set; }    // 魔法减伤率

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

        CurrentHealth = Get(UnitStatType.MaxHealth);
        CurrentMana = Get(UnitStatType.MaxMana);

        RecalculateDerived();
    }

    private void Add(UnitStatType type, float baseValue, float growth)
    {
        var stat = new UnitStat();
        stat.Init((fp)baseValue, (fp)growth);
        stats[type] = stat;
    }

    public fp Get(UnitStatType type)
    {
        return stats[type].FinalValue;
    }

    public void SetLevel(int level)
    {
        foreach (var s in stats.Values)
            s.SetLevel(level);

        ClampCurrentValues();
        RecalculateDerived();
    }

    // ===== 当前值操作 =====

    public void ModifyHealth(fp delta)
    {
        CurrentHealth += delta;
        ClampHealth();
    }

    public void ModifyMana(fp delta)
    {
        CurrentMana += delta;
        ClampMana();
    }

    private void ClampHealth()
    {
        fp max = Get(UnitStatType.MaxHealth);
        if (CurrentHealth > max) CurrentHealth = max;
        if (CurrentHealth < 0) CurrentHealth = 0;
    }

    private void ClampMana()
    {
        fp max = Get(UnitStatType.MaxMana);
        if (CurrentMana > max) CurrentMana = max;
        if (CurrentMana < 0) CurrentMana = 0;
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
        
        PhysicalDamageReduction = armor / (100 + armor);
        MagicDamageReduction = mr / (100 + mr);
    }

    public void Clean()
    {
        stats.Clear();

        CurrentHealth = fp.zero;
        CurrentMana = fp.zero;

        PhysicalDamageReduction = fp.zero;
        MagicDamageReduction = fp.zero;
    }
}