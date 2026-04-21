using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "RemoveBuffOnRuntimeDestroyNode", menuName = "SkillSystem/RuntimeCallbacks/Remove Buff On Runtime Destroy")]
public sealed class RemoveBuffOnRuntimeDestroyNode : SkillRuntimeCallbackNode
{
    [LabelText("目标 BuffId")]
    public int BuffId;

    public override void Execute(in SkillRuntimeLifecycleContext context)
    {
        if (BuffId == 0 || context.Owner == null || context.Owner.BuffHandler == null)
            return;

        context.Owner.BuffHandler.TryRemoveBuff(BuffId);
    }
}
