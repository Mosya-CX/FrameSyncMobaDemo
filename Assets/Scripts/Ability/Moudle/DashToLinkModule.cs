using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "Ability_DashToLink", menuName = "技能系统/模块/按Link冲刺")]
public class DashToLinkModule : AbilityBaseMoudle
{
    public string LinkKey = "DefaultLink";
    public float Duration = 0.2f;

    public override void Apply(AbilityExecutionContext context)
    {
        if (context?.Caster == null)
            return;

        if (!context.Caster.AbilityLinkController.TryGetLink(LinkKey, out var link))
            return;

        fp3? pos = null;
        UnitCore unit = null;

        if (link.LinkedUnit != null)
        {
            unit = link.LinkedUnit;
            pos = link.LinkedUnit.LogicPosition;
        }
        else if (link.LinkedPosition.HasValue)
        {
            pos = link.LinkedPosition.Value;
        }

        if (!pos.HasValue)
            return;

        var spec = new DashSpec
        {
            Duration = (fp)Duration,
            Distance = 0,
            TrajectoryType = DashTrajectoryType.ToPoint,
        };

        context.Caster.DashMotor.StartDash(spec, pos, unit);
    }
}