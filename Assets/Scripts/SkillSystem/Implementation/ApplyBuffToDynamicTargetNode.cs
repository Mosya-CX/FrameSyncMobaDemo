using UnityEngine;

public enum SkillSimpleTargetResolveMode : byte
{
    Self = 0,
    ResolvedTargetUnit = 1,
}

[CreateAssetMenu(fileName = "ApplyBuffToDynamicTargetNode", menuName = "SkillSystem/Effects/Common/Apply Buff To Dynamic Target")]
public sealed class ApplyBuffToDynamicTargetNode : SkillEffectNode
{
    public SkillSimpleTargetResolveMode TargetMode = SkillSimpleTargetResolveMode.Self;
    public BuffData Buff;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (Buff == null || context.Caster == null)
            return;

        UnitCore target = TargetMode switch
        {
            SkillSimpleTargetResolveMode.Self => context.Caster,
            SkillSimpleTargetResolveMode.ResolvedTargetUnit => context.TargetUnit,
            _ => null
        };

        if (target == null || target.IsDead || target.BuffHandler == null)
            return;

        target.BuffHandler.AddBuff(Buff, context.Caster);
    }
}
