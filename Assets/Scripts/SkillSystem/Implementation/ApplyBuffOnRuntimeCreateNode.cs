using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "ApplyBuffOnRuntimeCreateNode", menuName = "SkillSystem/RuntimeCallbacks/Apply Buff On Runtime Create")]
public sealed class ApplyBuffOnRuntimeCreateNode : SkillRuntimeCallbackNode
{
    [LabelText("目标 Buff")]
    public BuffData Buff;

    [LabelText("若已存在则跳过")]
    public bool SkipIfAlreadyPresent = true;

    public override void Execute(in SkillRuntimeLifecycleContext context)
    {
        if (Buff == null || context.Owner == null || context.Owner.BuffHandler == null)
            return;

        if (SkipIfAlreadyPresent && context.Owner.BuffHandler.TryGetBuff(Buff.Id, out _))
            return;

        context.Owner.BuffHandler.AddBuff(Buff, context.Owner);
    }
}
