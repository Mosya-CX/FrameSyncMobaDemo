using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "ToggleStepDef", menuName = "SkillSystem/Steps/Toggle Step")]
public sealed class ToggleStepDef : SkillStepDef
{
    [TitleGroup("开启前摇"), LabelText("启用前摇")]
    public bool HasStartup = true;

    [TitleGroup("开启前摇"), LabelText("前摇时长"), ShowIf(nameof(HasStartup))]
    [Min(0f)]
    public float StartupDuration = 0f;

    [TitleGroup("开启前摇"), LabelText("前摇动画Cue"), ShowIf(nameof(HasStartup))]
    public string StartupAnimationCue;

    [TitleGroup("开启态"), LabelText("开启期间允许移动")]
    public bool AllowMoveWhileOpen = true;

    [TitleGroup("开启态"), LabelText("开启期间允许攻击")]
    public bool AllowAttackWhileOpen = false;

    [TitleGroup("开启态"), LabelText("开启期间允许施法")]
    public bool AllowCastWhileOpen = true;

    [TitleGroup("开启态"), LabelText("开启期间允许冲刺")]
    public bool AllowDashWhileOpen = false;

    [TitleGroup("开启态"), LabelText("最大持续时间")]
    [Min(0f)]
    public float MaxOpenDuration = 0f;

    [TitleGroup("条件"), LabelText("步骤条件")]
    public SkillGateBase[] Gates;

    [TitleGroup("节点"), LabelText("开启节点")]
    public SkillEffectNode[] OnOpenNodes;

    [TitleGroup("节点"), LabelText("持续节点")]
    public SkillEffectNode[] OnTickNodes;

    [TitleGroup("节点"), LabelText("关闭节点")]
    public SkillEffectNode[] OnCloseNodes;

    [TitleGroup("节点"), LabelText("打断关闭节点")]
    public SkillEffectNode[] OnInterruptedCloseNodes;

    private const string OpenedKey = "__Toggle_Opened";

    public override SkillGateBase[] StepGates => Gates;

    public override SkillActionLockProfile GetActionLockProfile(SkillDef skill)
    {
        return new SkillActionLockProfile
        {
            Enabled = true,
            OccupiedChannels = ActionChannelMask.Cast,
            BlockedChannels =
                (AllowMoveWhileOpen ? ActionChannelMask.None : ActionChannelMask.Move | ActionChannelMask.Track) |
                (AllowAttackWhileOpen ? ActionChannelMask.None : ActionChannelMask.Attack) |
                (AllowCastWhileOpen ? ActionChannelMask.None : ActionChannelMask.Cast) |
                (AllowDashWhileOpen ? ActionChannelMask.None : ActionChannelMask.Dash)
        };
    }

    public override void OnEnter(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        execution.StepElapsed = fp.zero;
        execution.StepState.Set(OpenedKey, !HasStartup || StartupDuration <= 0f);

        if (HasStartup && !string.IsNullOrEmpty(StartupAnimationCue))
        {
            context.Controller?.EmitPresentationEvent(
                new SkillPresentationEvent(context.Caster, context.Skill != null ? context.Skill.Id : 0, StartupAnimationCue, context.TargetUnit, context.TargetPoint, context.AimDirection));
        }

        if (!HasStartup || StartupDuration <= 0f)
            ExecuteNodes(OnOpenNodes, execution, context, currentTick);
    }

    public override void OnTick(SkillExecution execution, SkillEffectContext context, fp deltaTime, uint currentTick)
    {
        if (!CheckGates(Gates, execution))
        {
            execution.RequestCancel(SkillStepExitReason.Interrupted);
            return;
        }

        execution.StepElapsed += deltaTime;

        bool opened = false;
        execution.StepState.TryGet(OpenedKey, out opened);

        if (!opened && execution.StepElapsed >= (fp)StartupDuration)
        {
            execution.StepState.Set(OpenedKey, true);
            ExecuteNodes(OnOpenNodes, execution, context, currentTick);
            return;
        }

        if (opened)
            ExecuteNodes(OnTickNodes, execution, context, currentTick);

        if ((fp)MaxOpenDuration > fp.zero && execution.StepElapsed >= (fp)(StartupDuration + MaxOpenDuration))
            execution.RequestAdvance(SkillStepExitReason.Expired);
    }

    public override void OnTrigger(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        execution.MarkTriggerHandled();
        execution.RequestAdvance(SkillStepExitReason.ToggleOff);
    }

    public override void OnExit(SkillExecution execution, SkillEffectContext context, SkillStepExitReason reason, uint currentTick)
    {
        if (reason == SkillStepExitReason.Interrupted)
            ExecuteNodes(OnInterruptedCloseNodes, execution, context, currentTick);
        else
            ExecuteNodes(OnCloseNodes, execution, context, currentTick);
    }
}
