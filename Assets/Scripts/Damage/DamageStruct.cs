using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class DamageInfo
{
    public UnitUID sourceUid;
    public UnitUID targetUid;

    public UnitCore Source => UnitManager.Instance.Spawns.ContainsKey(sourceUid) ? UnitManager.Instance.Spawns[sourceUid] : null;
    public UnitCore Target => UnitManager.Instance.Spawns.ContainsKey(targetUid) ? UnitManager.Instance.Spawns[targetUid] : null;

    public fp basicPhysicalDamage;
    public fp basicMagicalDamage;

    public fp physicalDamageAdder;
    public fp magicalDamageAdder;

    public fp physicalDamageMultiplier = 1;
    public fp magicalDamageMultiplier = 1;

    public bool isCrited;
    public fp critedDamageAdder;
    public fp critedDamageMultiplier = 1;

    public HashSet<string> tags = new();

    public object extra;

    public fp GetTotal()
    {
        var critedPhysicalPart = isCrited ? (basicPhysicalDamage * (1 - Source.Stats.Get(UnitStatType.CritMultiplier)) + critedDamageAdder) * critedDamageMultiplier : 0;
        var critedMagicalPart = isCrited ? (basicMagicalDamage * (1 - Source.Stats.Get(UnitStatType.CritMultiplier)) + critedDamageAdder) * critedDamageMultiplier : 0;
        return (basicPhysicalDamage + critedPhysicalPart + physicalDamageAdder) * physicalDamageMultiplier + (basicMagicalDamage + critedMagicalPart + magicalDamageAdder) * magicalDamageMultiplier;
    }
}

public class DamageTagConst
{
    public const string FromAttack = "FromAttack";// 来自攻击
    public const string FromAbility = "FromAbility";// 来自技能
    public const string FromBuff = "FromBuff";// 来自Buff
    public const string FromEquipment = "FromEquipment";// 来自装备
    public const string FromHero = "FromHero";// 来自英雄
    public const string FromMob = "FromMob";// 来自小兵
    public const string FromMonster = "FromMonster";// 来自野怪
    public const string ToHero = "ToHero";// 作用于英雄
    public const string ToMob = "ToMob";// 作用于小兵
    public const string ToMonster = "ToMonster";// 作用于野怪
    public const string MeleeCase = "MeleeCase";// 近战造成
    public const string RangeCase = "RangeCase";// 远程造成
    public const string PeriodicDamage = "PeriodicDamage";// 持续伤害
    public const string ProcDamage = "ProcDamage";// 特效伤害
}

public delegate void DamageCallback(in DamageInfo damageInfo);

public delegate void DamageModifier(ref DamageInfo damageInfo);