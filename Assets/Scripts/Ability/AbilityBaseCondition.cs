using UnityEngine;

public abstract class AbilityBaseCondition : ScriptableObject
{
    public abstract bool CanStartPreview(AbilityRuntime runtime);
    public abstract bool CanCommit(AbilityRuntime runtime, in AbilityTriggerContext context);
    public abstract void PayAbilityCost(AbilityRuntime runtime);
    public abstract void CancelReturn(AbilityRuntime runtime);
}