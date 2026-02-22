using UnityEngine;

public abstract class SkillIndicatorModule : ScriptableObject
{
    public abstract ISkillIndicatorRuntime CreateRuntime();
}
