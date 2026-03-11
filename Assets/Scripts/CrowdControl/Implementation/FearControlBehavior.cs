using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "Control_FearBehavior", menuName = "控制系统/行为/Fear")]
public class FearControlBehavior : ControlBehaviorBase
{
    public float FearRunDistance = 3f;

    public override void OnTick(CrowdControlRuntimeContext context, fp deltaTime)
    {
        if (context?.Owner is not HeroUnit hero)
            return;

        if (context.Source == null)
            return;

        var dir = hero.LogicPosition - context.Source.LogicPosition;
        if (fpmath.lengthsq(dir) <= 0)
            return;

        dir = fpmath.normalize(dir);
        var target = hero.LogicPosition + dir * (fp)FearRunDistance;
        hero.SetDestinationByOrder(target);
    }
}