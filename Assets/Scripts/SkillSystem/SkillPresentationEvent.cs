using System;
using Unity.Mathematics.FixedPoint;

[Serializable]
public struct SkillPresentationEvent
{
    public UnitUID CasterUid;
    public int SkillId;
    public string CueName;

    public bool HasTargetUnit;
    public UnitUID TargetUnitUid;

    public fp3? TargetPoint;
    public fp3? AimDirection;

    public SkillPresentationEvent(
        UnitCore caster,
        int skillId,
        string cueName,
        UnitCore targetUnit = null,
        fp3? targetPoint = null,
        fp3? aimDirection = null)
    {
        CasterUid = caster != null ? caster.UnitID : default;
        SkillId = skillId;
        CueName = cueName;
        HasTargetUnit = targetUnit != null;
        TargetUnitUid = targetUnit != null ? targetUnit.UnitID : default;
        TargetPoint = targetPoint;
        AimDirection = aimDirection;
    }
}
