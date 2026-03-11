using Unity.Mathematics.FixedPoint;

public sealed class AbilityExecutionContext
{
    public HeroUnit Caster;
    public AbilityRuntime Runtime;
    public AbilityData Data => Runtime.Data;

    public AbilityTriggerContext TriggerContext;

    public CastStageData Stage;
    public CastStageType StageType => Stage != null ? Stage.Type : CastStageType.None;

    public fp DeltaTime;
    public fp ElapsedStageTime;
    public fp RemainingStageTime;

    public uint CurrentTick;
    public uint CastStartTick;
    public uint StageEnterTick;

    public UnitCore TargetUnit;
    public fp3? TargetPosition;

    public AbilityContext Blackboard => Runtime.Blackboard;
}