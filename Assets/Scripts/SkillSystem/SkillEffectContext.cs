using Unity.Mathematics.FixedPoint;

public struct SkillEffectContext
{
    public SkillExecution Execution;
    public SkillExecutionController Controller;
    public SkillRuntime Runtime;

    public UnitCore Caster;
    public SkillDef Skill;
    public SkillStepDef Step;
    public uint CurrentTick;

    public UnitCore TargetUnit;
    public fp3? TargetPoint;
    public fp3? AimDirection;

    public SkillBlackboard Blackboard;
    public SkillBlackboard StepState;
    public SkillBlackboard SharedBlackboard;
}
