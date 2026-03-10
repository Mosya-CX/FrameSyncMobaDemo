using UnityEngine;

public abstract class AbilityIndicatorBase : ScriptableObject
{
    public abstract void OnCreate();
    public abstract void ActiveIndicator();
    public abstract void UpdateIndicator();
    public abstract void InactiveIndicator();
}