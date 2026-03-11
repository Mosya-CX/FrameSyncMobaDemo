using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public readonly struct DamageDealtEvent
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;
    public readonly DamageResult Result;

    public DamageDealtEvent(UnitCore source, UnitCore target, DamageResult result)
    {
        Source = source;
        Target = target;
        Result = result;
    }
}

public readonly struct DamageTakenEvent
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;
    public readonly DamageResult Result;

    public DamageTakenEvent(UnitCore source, UnitCore target, DamageResult result)
    {
        Source = source;
        Target = target;
        Result = result;
    }
}

public readonly struct HealDealtEvent
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;
    public readonly HealResult Result;

    public HealDealtEvent(UnitCore source, UnitCore target, HealResult result)
    {
        Source = source;
        Target = target;
        Result = result;
    }
}

public readonly struct HealTakenEvent
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;
    public readonly HealResult Result;

    public HealTakenEvent(UnitCore source, UnitCore target, HealResult result)
    {
        Source = source;
        Target = target;
        Result = result;
    }
}

public readonly struct AttackEvent
{
    public readonly UnitCore Attacker;
    public readonly UnitCore Target;

    public AttackEvent(UnitCore attacker, UnitCore target)
    {
        Attacker = attacker;
        Target = target;
    }
}

public readonly struct AbilityCastStageEvent
{
    public readonly HeroUnit Caster;
    public readonly int AbilityId;
    public readonly CastStageType StageType;
    public readonly UnitCore TargetUnit;
    public readonly fp3? TargetPosition;

    public AbilityCastStageEvent(HeroUnit caster, int abilityId, CastStageType stageType, UnitCore targetUnit, fp3? targetPosition)
    {
        Caster = caster;
        AbilityId = abilityId;
        StageType = stageType;
        TargetUnit = targetUnit;
        TargetPosition = targetPosition;
    }
}

public readonly struct KillEvent
{
    public readonly UnitCore Killer;
    public readonly UnitCore Victim;

    public KillEvent(UnitCore killer, UnitCore victim)
    {
        Killer = killer;
        Victim = victim;
    }
}

public readonly struct AssistEvent
{
    public readonly UnitCore Assistant;
    public readonly UnitCore Victim;
    public readonly UnitCore Killer;

    public AssistEvent(UnitCore assistant, UnitCore victim, UnitCore killer)
    {
        Assistant = assistant;
        Victim = victim;
        Killer = killer;
    }
}

public readonly struct DyingEvent
{
    public readonly UnitCore Victim;
    public readonly UnitCore Killer;

    public DyingEvent(UnitCore victim, UnitCore killer)
    {
        Victim = victim;
        Killer = killer;
    }
}

public readonly struct DeathEvent
{
    public readonly UnitCore Victim;
    public readonly UnitCore Killer;
    public readonly IReadOnlyList<UnitCore> Assisters;

    public DeathEvent(UnitCore victim, UnitCore killer, IReadOnlyList<UnitCore> assisters)
    {
        Victim = victim;
        Killer = killer;
        Assisters = assisters;
    }
}