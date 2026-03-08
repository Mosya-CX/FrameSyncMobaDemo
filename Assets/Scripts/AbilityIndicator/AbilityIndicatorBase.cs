using UnityEngine;

public abstract class AbilityIndicatorBase : ScriptableObject
{
    public abstract void OnCreate(AbilityIndicatorRuntime runtimeData);
    public abstract void ActiveIndicator(AbilityIndicatorRuntime runtimeData);
    public abstract void UpdateIndicator(AbilityIndicatorRuntime runtimeData);
    public abstract void InactiveIndicator(AbilityIndicatorRuntime runtimeData);
}
