using UnityEngine;

public abstract class AbilityIndicatorBase : ScriptableObject
{
    public abstract void OnCreate();
    public abstract void OnShow(AbilityPreviewContext context);
    public abstract void OnUpdate(AbilityPreviewContext context, AbilityPreviewResult result, float deltaTime);
    public abstract void OnHide();
}