using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "SwitchSkillGroupNode", menuName = "SkillSystem/Effects/Common/SwitchSkillGroup")]
public sealed class SwitchSkillGroupNode : SkillEffectNode
{
    [LabelText("目标技能组索引")]
    public int TargetGroupIndex;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null)
            return;

        var controller = context.Caster.GetComponent<SkillGroupController>();
        if (controller == null)
            return;

        controller.SwitchToGroup(TargetGroupIndex);
    }
}
