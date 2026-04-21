using Unity.Mathematics.FixedPoint;

public enum SkillRequestSource : byte
{
    Player = 0,
    AI = 1,
    Trigger = 2,
    Script = 3,
    System = 4,
}

public struct SkillCastRequest
{
    public UnitUID CasterUid;
    public int SkillId;

    public SkillRequestSource Source;
    public bool IsPreview;
    public bool SmartCast;

    public UnitUID? TargetUnitUid;
    public fp3? TargetPoint;
    public fp3? AimDirection;

    public uint RequestTick;
    public SkillBlackboardSnapshot InitialBlackboard;
}
