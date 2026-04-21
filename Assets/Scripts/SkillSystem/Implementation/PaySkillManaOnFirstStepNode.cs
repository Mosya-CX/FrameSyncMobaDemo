using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "PaySkillManaOnFirstStepNode", menuName = "SkillSystem/Effects/Common/Pay Skill Mana On First Step")]
public sealed class PaySkillManaOnFirstStepNode : SkillEffectNode
{
    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null || context.Skill == null)
            return;

        if (execution.StepIndex != 0)
            return;

        if (context.Skill.ManaCost > 0)
            context.Caster.Stats.ModifyMana(-(fp)context.Skill.ManaCost);
    }
}
