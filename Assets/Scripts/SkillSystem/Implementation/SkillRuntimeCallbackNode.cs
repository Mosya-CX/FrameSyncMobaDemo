using UnityEngine;

public abstract class SkillRuntimeCallbackNode : ScriptableObject
{
    public abstract void Execute(in SkillRuntimeLifecycleContext context);
}
