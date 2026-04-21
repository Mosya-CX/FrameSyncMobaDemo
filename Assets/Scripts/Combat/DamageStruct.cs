using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public enum DamageSourceKind : byte
{
    Attack,
    Ability,
    Buff,
    Equipment,
    Proc,
}

public sealed class DamageRequest
{
    public UnitCore Source;
    public UnitCore Target;

    public DamageSourceKind SourceKind;

    public fp BasePhysicalDamage;
    public fp BaseMagicalDamage;
    public bool CanCrit;

    public HashSet<string> Tags = new();
    public object Extra;
}

public sealed class DamageContext
{
    public UnitCore Source;
    public UnitCore Target;

    public DamageSourceKind SourceKind;

    public fp BasePhysicalDamage;
    public fp BaseMagicalDamage;

    public fp BonusPhysicalDamage;
    public fp BonusMagicalDamage;

    public fp PhysicalMultiplier = 1;
    public fp MagicalMultiplier = 1;

    public bool IsCrit;
    public fp CritBonusDamage;
    public fp CritMultiplier = 1;

    public HashSet<string> Tags = new();
    public object Extra;

    public fp FinalPhysicalBeforeReduction;
    public fp FinalMagicalBeforeReduction;

    public fp PhysicalReductionMultiplier = 1;
    public fp MagicalReductionMultiplier = 1;
}

public readonly struct DamageResult
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;

    public readonly fp FinalPhysicalDamage;
    public readonly fp FinalMagicalDamage;
    public readonly fp TotalDamage;

    public readonly bool IsCrit;
    public readonly IReadOnlyCollection<string> Tags;

    public DamageResult(UnitCore source, UnitCore target, fp finalPhysicalDamage, fp finalMagicalDamage, bool isCrit, IReadOnlyCollection<string> tags)
    {
        Source = source;
        Target = target;
        FinalPhysicalDamage = finalPhysicalDamage;
        FinalMagicalDamage = finalMagicalDamage;
        TotalDamage = finalPhysicalDamage + finalMagicalDamage;
        IsCrit = isCrit;
        Tags = tags;
    }
}

public static class DamageTagConst
{
    public const string FromAttack = "FromAttack";
    public const string FromAbility = "FromAbility";
    public const string FromBuff = "FromBuff";
    public const string FromEquipment = "FromEquipment";
    public const string FromHero = "FromHero";
    public const string FromMob = "FromMob";
    public const string FromMonster = "FromMonster";
    public const string ToHero = "ToHero";
    public const string ToMob = "ToMob";
    public const string ToMonster = "ToMonster";
    public const string MeleeCase = "MeleeCase";
    public const string RangeCase = "RangeCase";
    public const string PeriodicDamage = "PeriodicDamage";
    public const string ProcDamage = "ProcDamage";
}