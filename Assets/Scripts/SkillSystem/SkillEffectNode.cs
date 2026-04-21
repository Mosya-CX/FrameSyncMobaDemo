using UnityEngine;

/// <summary>
/// 技能效果节点基类。由 Step 协议在合适时机调用。
/// </summary>
public abstract class SkillEffectNode : ScriptableObject
{
    public abstract void Execute(SkillExecution execution, SkillEffectContext context);
}
