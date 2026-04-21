using Unity.Mathematics.FixedPoint;

public struct SkillResolvedCast
{
    public UnitCore Caster;
    public UnitCore TargetUnit;
    public fp3? TargetPoint;
    public fp3? AimDirection;

    public bool NeedApproach;
    public fp3? ApproachPoint;
}
