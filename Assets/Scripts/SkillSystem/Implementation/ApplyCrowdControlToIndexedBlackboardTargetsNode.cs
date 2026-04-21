using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "ApplyCrowdControlToIndexedBlackboardTargetsNode", menuName = "SkillSystem/Effects/Common/Apply CrowdControl To Indexed Blackboard Targets")]
public sealed class ApplyCrowdControlToIndexedBlackboardTargetsNode : SkillEffectNode
{
    public string CountKey = "Targets.Count";
    public string TargetPrefix = "Targets";
    public CrowdControlData Control;
    public float Duration = 0.25f;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null || context.Blackboard == null || Control == null)
            return;

        if (!context.Blackboard.TryGet(CountKey, out int count) || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            if (!context.Blackboard.TryGet($"{TargetPrefix}_{i}", out UnitUID uid))
                continue;

            if (!UnitManager.Instance.Spawns.TryGetValue(uid, out var target))
                continue;

            if (target == null || target.IsDead)
                continue;

            target.CrowdControlHandler?.AddControl(Control, (fp)Duration, context.Caster);
        }
    }
}
