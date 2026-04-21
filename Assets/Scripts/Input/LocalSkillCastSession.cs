using Unity.Mathematics.FixedPoint;

public enum LocalSkillCastSessionState : byte
{
    None = 0,
    Preview = 1,
}

public sealed class LocalSkillCastSession
{
    public HeroUnit Caster;
    public SkillDef Skill;
    public SkillSlot Slot;
    public int GroupIndex;

    public UnitCore HoveredUnit;
    public fp3? HoveredPoint;
    public fp3? AimDirection;

    public bool IsValid;
    public bool WaitingForConfirm;
    public LocalSkillCastSessionState State;

    public bool IsPreviewing => State == LocalSkillCastSessionState.Preview;

    public void Clear()
    {
        Caster = null;
        Skill = null;
        Slot = default;
        GroupIndex = 0;
        HoveredUnit = null;
        HoveredPoint = null;
        AimDirection = null;
        IsValid = false;
        WaitingForConfirm = false;
        State = LocalSkillCastSessionState.None;
    }
}
