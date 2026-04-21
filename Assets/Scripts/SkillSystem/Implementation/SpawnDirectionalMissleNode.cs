using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "SpawnDirectionalMissleNode", menuName = "SkillSystem/Effects/Common/Spawn Directional Missle")]
public sealed class SpawnDirectionalMissleNode : SkillEffectNode
{
    [LabelText("导弹 PrefabId")]
    public short MisslePrefabId;

    [LabelText("若无目标点则使用朝向距离")]
    public float FallbackDistance = 10f;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null)
            return;

        fp3 keyPoint;
        if (context.TargetPoint.HasValue)
        {
            keyPoint = context.TargetPoint.Value;
        }
        else
        {
            fp3 toward = ResolveToward(context);
            keyPoint = context.Caster.LogicPosition + toward * (fp)FallbackDistance;
        }

        MissleManager.Instance.SpawnNow(MisslePrefabId, new DirectionalMissleInitialData(context.Caster, keyPoint));
    }

    private static fp3 ResolveToward(SkillEffectContext context)
    {
        if (context.AimDirection.HasValue && fpmath.lengthsq(context.AimDirection.Value) > fp.zero)
            return fpmath.normalize(context.AimDirection.Value);

        return context.Caster.Direction;
    }
}
