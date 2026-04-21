using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "CastStepDef", menuName = "SkillSystem/Steps/Cast Step")]
public sealed class CastStepDef : SkillStepDef
{
    [TitleGroup("时序"), LabelText("前摇时长")]
    [Min(0f)]
    public float CastTime = 0f;

    [TitleGroup("动作限制"), LabelText("动作锁"), InlineProperty, HideLabel]
    public SkillActionLockProfile ActionLock = new SkillActionLockProfile();

    [TitleGroup("条件"), LabelText("步骤条件")]
    public SkillGateBase[] Gates;

    [TitleGroup("节点"), LabelText("进入节点")]
    public SkillEffectNode[] OnBeginNodes;

    [TitleGroup("节点"), LabelText("执行节点")]
    public SkillEffectNode[] OnExecuteNodes;

    [TitleGroup("节点"), LabelText("打断结束节点")]
    public SkillEffectNode[] OnInterruptedNodes;

    private const string ExecuteDoneKey = "__Cast_ExecuteDone";

    public override SkillGateBase[] StepGates => Gates;

    public override SkillActionLockProfile GetActionLockProfile(SkillDef skill)
    {
        return ActionLock != null ? ActionLock : base.GetActionLockProfile(skill);
    }

    public override void OnEnter(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        execution.StepElapsed = fp.zero;
        execution.StepState.Set(ExecuteDoneKey, false);
        ExecuteNodes(OnBeginNodes, execution, context, currentTick);
    }

    public override void OnTick(SkillExecution execution, SkillEffectContext context, fp deltaTime, uint currentTick)
    {
        if (!CheckGates(Gates, execution))
        {
            execution.RequestCancel(SkillStepExitReason.Interrupted);
            return;
        }

        execution.StepElapsed += deltaTime;

        bool executeDone = false;
        execution.StepState.TryGet(ExecuteDoneKey, out executeDone);

        if (!executeDone && execution.StepElapsed >= (fp)CastTime)
        {
            execution.StepState.Set(ExecuteDoneKey, true);
            ExecuteNodes(OnExecuteNodes, execution, context, currentTick);

            if (!execution.IsCancelled)
                execution.RequestAdvance(SkillStepExitReason.Normal);
        }
    }

    public override void OnTrigger(SkillExecution execution, SkillEffectContext context, uint currentTick) { }

    public override void OnExit(SkillExecution execution, SkillEffectContext context, SkillStepExitReason reason, uint currentTick)
    {
        if (reason == SkillStepExitReason.Interrupted)
            ExecuteNodes(OnInterruptedNodes, execution, context, currentTick);
    }
}
