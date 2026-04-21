using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "SummonStepDef", menuName = "SkillSystem/Steps/Summon Step")]
public sealed class SummonStepDef : SkillStepDef
{
    [TitleGroup("前摇"), LabelText("前摇时长")]
    [Min(0f)]
    public float StartupDuration = 0f;

    [TitleGroup("前摇"), LabelText("前摇动画Cue")]
    public string StartupAnimationCue;

    [TitleGroup("召唤态"), LabelText("最大存续时间")]
    [Min(0f)]
    public float MaxLifetime = 0f;

    [TitleGroup("召唤态"), LabelText("最大召唤数量")]
    public int MaxSummonCount = 1;

    [TitleGroup("召唤态"), LabelText("重复触发命令召唤物")]
    public bool RetriggerCommandsSummons = true;

    [TitleGroup("条件"), LabelText("步骤条件")]
    public SkillGateBase[] Gates;

    [TitleGroup("节点"), LabelText("开始节点")]
    public SkillEffectNode[] OnBeginNodes;

    [TitleGroup("节点"), LabelText("生成节点")]
    public SkillEffectNode[] OnSpawnNodes;

    [TitleGroup("节点"), LabelText("持续节点")]
    public SkillEffectNode[] OnTickNodes;

    [TitleGroup("节点"), LabelText("重复触发节点")]
    public SkillEffectNode[] OnRetriggerNodes;

    [TitleGroup("节点"), LabelText("到期结束节点")]
    public SkillEffectNode[] OnExpireNodes;

    [TitleGroup("节点"), LabelText("打断结束节点")]
    public SkillEffectNode[] OnInterruptedNodes;

    private const string SpawnedKey = "__Summon_Spawned";

    public override SkillGateBase[] StepGates => Gates;

    public override void OnEnter(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        execution.StepElapsed = fp.zero;
        execution.StepState.Set(SpawnedKey, false);

        if (!string.IsNullOrEmpty(StartupAnimationCue))
        {
            context.Controller?.EmitPresentationEvent(
                new SkillPresentationEvent(context.Caster, context.Skill != null ? context.Skill.Id : 0, StartupAnimationCue, context.TargetUnit, context.TargetPoint, context.AimDirection));
        }

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

        bool spawned = false;
        execution.StepState.TryGet(SpawnedKey, out spawned);

        if (!spawned && execution.StepElapsed >= (fp)StartupDuration)
        {
            execution.StepState.Set(SpawnedKey, true);
            ExecuteNodes(OnSpawnNodes, execution, context, currentTick);
            return;
        }

        if (spawned)
            ExecuteNodes(OnTickNodes, execution, context, currentTick);

        if ((fp)MaxLifetime > fp.zero && execution.StepElapsed >= (fp)(StartupDuration + MaxLifetime))
            execution.RequestAdvance(SkillStepExitReason.Expired);
    }

    public override void OnTrigger(SkillExecution execution, SkillEffectContext context, uint currentTick)
    {
        if (!RetriggerCommandsSummons)
            return;

        execution.MarkTriggerHandled();
        ExecuteNodes(OnRetriggerNodes, execution, context, currentTick);
    }

    public override void OnExit(SkillExecution execution, SkillEffectContext context, SkillStepExitReason reason, uint currentTick)
    {
        if (reason == SkillStepExitReason.Interrupted)
            ExecuteNodes(OnInterruptedNodes, execution, context, currentTick);
        else
            ExecuteNodes(OnExpireNodes, execution, context, currentTick);
    }
}
