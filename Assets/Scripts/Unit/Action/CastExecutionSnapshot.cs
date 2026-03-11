using Unity.Mathematics.FixedPoint;

public sealed class CastExecutionSnapshot
{
    public AbilityRuntime Runtime;
    public AbilityTriggerContext TriggerContext;

    public uint CastStartTick;
    public uint StageEnterTick;

    public int StageIndex;
    public fp StageTimer;
    public fp ElapsedStageTime;

    public UnitCore TargetUnit;
    public fp3? TargetPosition;

    public AbilityContext Blackboard;
}