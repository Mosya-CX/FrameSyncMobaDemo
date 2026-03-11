using UnityEngine;
using Unity.Mathematics.FixedPoint;

[CreateAssetMenu(fileName = "Ability_DashModule", menuName = "技能系统/模块/Dash")]
public class DashAbilityModule : AbilityBaseMoudle
{
    public float Distance = 4f;
    public float Duration = 0.2f;
    public DashTrajectoryType TrajectoryType = DashTrajectoryType.Linear;

    public override void Apply(AbilityExecutionContext context)
    {
        if (context?.Caster == null)
            return;

        var spec = new DashSpec
        {
            Distance = (fp)Distance,
            Duration = (fp)Duration,
            TrajectoryType = TrajectoryType,
        };

        context.Caster.DashMotor.StartDash(spec, context.TargetPosition, context.TargetUnit);
    }
}