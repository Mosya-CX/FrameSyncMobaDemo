using Unity.Mathematics.FixedPoint;
using UnityEngine;

public sealed class SkillExecution
{
    public readonly SkillDef Def;
    public readonly SkillRuntime Runtime;
    public readonly UnitCore Caster;
    public readonly SkillExecutionController Controller;
    public readonly SkillResolvedCast ResolvedCast;

    public int StepIndex { get; private set; }
    public fp StepElapsed { get; set; }

    public bool IsFinished { get; private set; }
    public bool IsCancelled { get; private set; }

    public SkillBlackboard Blackboard { get; private set; }
    public SkillBlackboard StepState { get; private set; }

    private uint castStartTick;
    private uint stepEnterTick;

    private SkillStepFlowRequest pendingFlow = SkillStepFlowRequest.Running;
    private SkillStepExitReason pendingExitReason = SkillStepExitReason.None;
    private bool triggerHandled = false;

    public SkillExecutionLane Lane => Def != null ? Def.ExecutionLane : SkillExecutionLane.Main;

    public SkillStepDef CurrentStep =>
        Def != null && Def.Steps != null && StepIndex >= 0 && StepIndex < Def.Steps.Length
            ? Def.Steps[StepIndex]
            : null;

    public uint CastStartTick => castStartTick;
    public uint StepEnterTick => stepEnterTick;

    public SkillExecution(
        SkillDef def,
        SkillRuntime runtime,
        UnitCore caster,
        SkillExecutionController controller,
        in SkillResolvedCast resolvedCast,
        int stepIndex,
        uint castStartTick,
        in SkillBlackboardSnapshot initialBlackboard)
    {
        Def = def;
        Runtime = runtime;
        Caster = caster;
        Controller = controller;
        ResolvedCast = resolvedCast;
        StepIndex = stepIndex;
        this.castStartTick = castStartTick;

        Blackboard = new SkillBlackboard();
        Blackboard.RestoreSnapshot(initialBlackboard);

        StepState = new SkillBlackboard();
    }

    public void Start(uint currentTick)
    {
        if (Def == null || Def.Steps == null || Def.Steps.Length == 0)
        {
            IsFinished = true;
            return;
        }

        if (!SkillStepValidationUtility.Validate(Def, out var reason))
        {
            Debug.LogError(reason);
            IsCancelled = true;
            IsFinished = true;
            return;
        }

        if (StepIndex < 0 || StepIndex >= Def.Steps.Length)
            StepIndex = 0;

        EnterCurrentStep(currentTick);
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (IsFinished || IsCancelled)
            return;

        var step = CurrentStep;
        if (step == null)
        {
            IsFinished = true;
            return;
        }

        ResetPendingFlow();
        step.OnTick(this, BuildContext(currentTick), dt, currentTick);
        ApplyPendingFlow(currentTick);
    }

    public bool TryTrigger(uint currentTick)
    {
        if (IsFinished || IsCancelled || CurrentStep == null)
            return false;

        triggerHandled = false;
        ResetPendingFlow();

        CurrentStep.OnTrigger(this, BuildContext(currentTick), currentTick);
        ApplyPendingFlow(currentTick);

        return triggerHandled || pendingFlow != SkillStepFlowRequest.Running;
    }

    public void MarkTriggerHandled()
    {
        triggerHandled = true;
    }

    public void RequestAdvance(SkillStepExitReason reason = SkillStepExitReason.Normal)
    {
        pendingFlow = SkillStepFlowRequest.Advance;
        pendingExitReason = reason;
    }

    public void RequestCancel(SkillStepExitReason reason = SkillStepExitReason.Interrupted)
    {
        pendingFlow = SkillStepFlowRequest.CancelExecution;
        pendingExitReason = reason;
    }

    public void Cancel()
    {
        if (IsFinished)
            return;

        IsCancelled = true;
    }

    private void ResetPendingFlow()
    {
        pendingFlow = SkillStepFlowRequest.Running;
        pendingExitReason = SkillStepExitReason.None;
    }

    private void ApplyPendingFlow(uint currentTick)
    {
        if (pendingFlow == SkillStepFlowRequest.CancelExecution)
        {
            CurrentStep?.OnExit(this, BuildContext(currentTick), pendingExitReason, currentTick);
            Cancel();
            return;
        }

        if (pendingFlow != SkillStepFlowRequest.Advance)
            return;

        CurrentStep?.OnExit(this, BuildContext(currentTick), pendingExitReason, currentTick);

        StepIndex++;
        if (Def == null || Def.Steps == null || StepIndex >= Def.Steps.Length)
        {
            IsFinished = true;
            return;
        }

        EnterCurrentStep(currentTick);
    }

    private void EnterCurrentStep(uint currentTick)
    {
        var step = CurrentStep;
        if (step == null)
        {
            IsFinished = true;
            return;
        }

        StepState.Clear();
        StepElapsed = fp.zero;
        stepEnterTick = currentTick;
        step.OnEnter(this, BuildContext(currentTick), currentTick);
    }

    public SkillActionLockProfile GetCurrentStepActionLock()
    {
        return CurrentStep != null
            ? CurrentStep.GetActionLockProfile(Def)
            : Def != null ? Def.DefaultActionLock : null;
    }

    private SkillEffectContext BuildContext(uint currentTick)
    {
        return new SkillEffectContext
        {
            Execution = this,
            Controller = Controller,
            Runtime = Runtime,
            Caster = Caster,
            Skill = Def,
            Step = CurrentStep,
            CurrentTick = currentTick,
            TargetUnit = ResolvedCast.TargetUnit,
            TargetPoint = ResolvedCast.TargetPoint,
            AimDirection = ResolvedCast.AimDirection,
            Blackboard = Blackboard,
            StepState = StepState,
            SharedBlackboard = Controller != null ? Controller.SharedBlackboard : null,
        };
    }

    public SkillExecutionSnapshot CaptureSnapshot()
    {
        return new SkillExecutionSnapshot
        {
            SkillId = Def != null ? Def.Id : 0,
            StepIndex = StepIndex,
            CasterUid = Caster != null ? Caster.UnitID : default,
            CastStartTick = castStartTick,
            StepEnterTick = stepEnterTick,
            StepElapsed = StepElapsed,
            IsFinished = IsFinished,
            IsCancelled = IsCancelled,
            HasTargetUnit = ResolvedCast.TargetUnit != null,
            TargetUnitUid = ResolvedCast.TargetUnit != null ? ResolvedCast.TargetUnit.UnitID : default,
            TargetPoint = ResolvedCast.TargetPoint,
            AimDirection = ResolvedCast.AimDirection,
            Blackboard = Blackboard != null ? Blackboard.CaptureSnapshot() : default,
            StepState = StepState != null ? StepState.CaptureSnapshot() : default,
        };
    }

    public void RestoreSnapshot(SkillExecutionSnapshot snapshot)
    {
        castStartTick = snapshot.CastStartTick;
        stepEnterTick = snapshot.StepEnterTick;
        StepIndex = snapshot.StepIndex;
        StepElapsed = snapshot.StepElapsed;
        IsFinished = snapshot.IsFinished;
        IsCancelled = snapshot.IsCancelled;

        Blackboard ??= new SkillBlackboard();
        Blackboard.RestoreSnapshot(snapshot.Blackboard);

        StepState ??= new SkillBlackboard();
        StepState.RestoreSnapshot(snapshot.StepState);
    }
}

public sealed class SkillExecutionSnapshot
{
    public int SkillId;
    public int StepIndex;
    public UnitUID CasterUid;
    public uint CastStartTick;
    public uint StepEnterTick;
    public fp StepElapsed;
    public bool IsFinished;
    public bool IsCancelled;
    public bool HasTargetUnit;
    public UnitUID TargetUnitUid;
    public fp3? TargetPoint;
    public fp3? AimDirection;
    public SkillBlackboardSnapshot Blackboard;
    public SkillBlackboardSnapshot StepState;
}
