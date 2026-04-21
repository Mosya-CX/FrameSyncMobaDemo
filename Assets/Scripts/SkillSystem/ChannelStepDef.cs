using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "ChannelStepDef", menuName = "SkillSystem/Steps/Channel Step")]
public sealed class ChannelStepDef : SkillStepDef
{
    [TitleGroup("时序"), LabelText("最大引导时长")]
    [Min(0f)]
    public float MaxChannelDuration = 0f;

    [TitleGroup("行为"), LabelText("允许提前结束")]
    public bool CanEndEarly = true;

    [TitleGroup("行为"), LabelText("可被打断")]
    public bool CanBeInterrupted = true;

    [TitleGroup("行为"), LabelText("引导时允许移动")]
    public bool AllowMove = false;

    [TitleGroup("行为"), LabelText("引导时允许攻击")]
    public bool AllowAttack = false;

    [TitleGroup("行为"), LabelText("引导时允许施法")]
    public bool AllowCast = false;

    [TitleGroup("行为"), LabelText("引导时允许冲刺")]
    public bool AllowDash = false;

    [TitleGroup("行为"), LabelText("提前结束黑板键")]
    public string ReleaseBlackboardKey = "InputReleased";

    [TitleGroup("条件"), LabelText("步骤条件")]
    public SkillGateBase[] Gates;

    [TitleGroup("节点"), LabelText("开始节点")]
    public SkillEffectNode[] OnBeginNodes;

    [TitleGroup("节点"), LabelText("持续节点")]
    public SkillEffectNode[] OnTickNodes;

    [TitleGroup("节点"), LabelText("正常结束节点")]
    public SkillEffectNode[] OnNormalEndNodes;

    [TitleGroup("节点"), LabelText("提前结束节点")]
    public SkillEffectNode[] OnEarlyEndNodes;

    [TitleGroup("节点"), LabelText("打断结束节点")]
    public SkillEffectNode[] OnInterruptedEndNodes;

    public override SkillGateBase[] StepGates => Gates;

    public override SkillActionLockProfile GetActionLockProfile(SkillDef skill)
    {
        return new SkillActionLockProfile
        {
            Enabled = true,
            OccupiedChannels = ActionChannelMask.Cast,
            BlockedChannels =
                (AllowMove ? ActionChannelMask.None : ActionChannelMask.Move | ActionChannelMask.Track) |
                (AllowAttack ? ActionChannelMask.None : ActionChannelMask.Attack) |
                (AllowCast ? ActionChannelMask.None : ActionChannelMask.Cast) |
                (AllowDash ? ActionChannelMask.None : ActionChannelMask.Dash)
        };
    }

    public override void OnEnter(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        execution.StepElapsed = fp.zero;
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
        ExecuteNodes(OnTickNodes, execution, context, currentTick);

        if (execution.IsCancelled)
            return;

        if (CanEndEarly && context.Blackboard != null &&
            context.Blackboard.TryGet(ReleaseBlackboardKey, out bool released) && released)
        {
            execution.RequestAdvance(SkillStepExitReason.EarlyEnd);
            return;
        }

        if ((fp)MaxChannelDuration > fp.zero && execution.StepElapsed >= (fp)MaxChannelDuration)
            execution.RequestAdvance(SkillStepExitReason.Normal);
    }

    public override void OnTrigger(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        if (CanEndEarly)
        {
            execution.MarkTriggerHandled();
            execution.RequestAdvance(SkillStepExitReason.EarlyEnd);
        }
    }

    public override void OnExit(SkillExecution execution, SkillEffectContext context, SkillStepExitReason reason, uint currentTick)
    {
        switch (reason)
        {
            case SkillStepExitReason.Normal:
                ExecuteNodes(OnNormalEndNodes, execution, context, currentTick);
                break;
            case SkillStepExitReason.EarlyEnd:
                ExecuteNodes(OnEarlyEndNodes, execution, context, currentTick);
                break;
            case SkillStepExitReason.Interrupted:
                if (CanBeInterrupted)
                    ExecuteNodes(OnInterruptedEndNodes, execution, context, currentTick);
                break;
        }
    }
}
