using UnityEngine;
using Unity.Mathematics.FixedPoint;

/// <summary>
/// 技能运行协议的唯一层级。系统只保留 Step，不再有 Phase。
/// </summary>
public abstract class SkillStepDef : ScriptableObject
{
    public virtual SkillActionLockProfile GetActionLockProfile(SkillDef skill)
    {
        return skill != null ? skill.DefaultActionLock : null;
    }

    public virtual bool CanFollow(SkillStepDef previous, out string reason)
    {
        reason = null;
        return true;
    }

    public virtual SkillGateBase[] StepGates => null;

    /// <summary>
    /// 被动技能是否允许自动启动。默认仅在冷却好时自动启动。
    /// </summary>
    public virtual bool CanAutoStartPassive(UnitCore caster, SkillRuntime runtime)
    {
        return runtime != null && !runtime.IsCoolingDown;
    }

    public abstract void OnEnter(SkillExecution execution, SkillEffectContext context, uint currentTick);
    public abstract void OnTick(SkillExecution execution, SkillEffectContext context, fp deltaTime, uint currentTick);
    public abstract void OnTrigger(SkillExecution execution, SkillEffectContext context, uint currentTick);
    public abstract void OnExit(SkillExecution execution, SkillEffectContext context, SkillStepExitReason reason, uint currentTick);

    protected static bool CheckGates(SkillGateBase[] gates, SkillExecution execution)
    {
        if (gates == null)
            return true;

        for (int i = 0; i < gates.Length; i++)
        {
            var gate = gates[i];
            if (gate == null)
                continue;

            if (!gate.CheckRunning(execution).Passed)
                return false;
        }

        return true;
    }

    protected static void ExecuteNodes(SkillEffectNode[] nodes, SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        if (nodes == null)
            return;

        context.CurrentTick = currentTick;

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null)
                continue;

            node.Execute(execution, context);

            if (execution.IsCancelled || execution.IsFinished)
                return;
        }
    }
}
