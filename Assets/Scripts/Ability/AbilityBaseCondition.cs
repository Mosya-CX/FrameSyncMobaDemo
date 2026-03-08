using UnityEngine;

public abstract class AbilityBaseCondition : ScriptableObject
{
    public abstract bool CanTrigger(AbilityInfo abilityInfo, in InputInfo inputInfo);
    public abstract void PayAbilityCost(AbilityInfo abilityInfo);
    public abstract void CancelReturn(AbilityInfo abilityInfo);
}

