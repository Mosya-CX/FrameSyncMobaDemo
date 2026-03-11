using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "Control_TauntBehavior", menuName = "控制系统/行为/Taunt")]
public class TauntControlBehavior : ControlBehaviorBase
{
    public override void OnTick(CrowdControlRuntimeContext context, fp deltaTime)
    {
        if (context?.Owner is not HeroUnit hero)
            return;

        if (context.Source == null)
            return;

        hero.SetTargetByOrder(context.Source);
    }
}