using Unity.Mathematics.FixedPoint;

public sealed class CastExecution
{
    private readonly HeroUnit owner;
    private readonly AbilityRuntime runtime;
    private readonly AbilityTriggerContext triggerContext;
    private readonly AbilityExecutionContext executionContext;

    private readonly uint castStartTick;

    private int stageIndex;
    private fp stageTimer;
    private fp elapsedStageTime;
    private bool started;

    public bool IsFinished { get; private set; }
    public bool IsCancelled { get; private set; }

    public AbilityRuntime Runtime => runtime;
    public CastStageData CurrentStage =>
        runtime.Data.Stages != null && stageIndex >= 0 && stageIndex < runtime.Data.Stages.Length
            ? runtime.Data.Stages[stageIndex]
            : null;

    public CastExecution(HeroUnit owner, AbilityRuntime runtime, AbilityTriggerContext triggerContext, uint castStartTick)
    {
        this.owner = owner;
        this.runtime = runtime;
        this.triggerContext = triggerContext;
        this.castStartTick = castStartTick;

        executionContext = new AbilityExecutionContext
        {
            Caster = owner,
            Runtime = runtime,
            TriggerContext = triggerContext,
            CastStartTick = castStartTick,
        };
    }

    public void Start(uint currentTick)
    {
        if (runtime.Data.Stages == null || runtime.Data.Stages.Length == 0)
        {
            runtime.PayCost();
            runtime.EnterCooldown();
            IsFinished = true;
            return;
        }

        if (!runtime.CanCommit(triggerContext))
        {
            IsCancelled = true;
            return;
        }

        runtime.PayCost();
        ResolveTarget();

        stageIndex = 0;
        started = true;
        EnterStage(currentTick);
    }

    public void RestoreFromSnapshot(CastExecutionSnapshot snapshot)
    {
        if (snapshot == null)
        {
            IsCancelled = true;
            return;
        }

        stageIndex = snapshot.StageIndex;
        stageTimer = snapshot.StageTimer;
        elapsedStageTime = snapshot.ElapsedStageTime;

        executionContext.CastStartTick = snapshot.CastStartTick;
        executionContext.StageEnterTick = snapshot.StageEnterTick;

        executionContext.TargetUnit = snapshot.TargetUnit;
        executionContext.TargetPosition = snapshot.TargetPosition;

        runtime.Blackboard = snapshot.Blackboard != null ? snapshot.Blackboard.Clone() : new AbilityContext();

        started = true;
        IsFinished = false;
        IsCancelled = false;

        if (CurrentStage != null)
        {
            executionContext.Stage = CurrentStage;
            executionContext.ElapsedStageTime = elapsedStageTime;
            executionContext.RemainingStageTime = stageTimer;
        }
    }

    public CastExecutionSnapshot CreateSnapshot()
    {
        return new CastExecutionSnapshot
        {
            Runtime = runtime,
            TriggerContext = triggerContext,
            CastStartTick = executionContext.CastStartTick,
            StageEnterTick = executionContext.StageEnterTick,
            StageIndex = stageIndex,
            StageTimer = stageTimer,
            ElapsedStageTime = elapsedStageTime,
            TargetUnit = executionContext.TargetUnit,
            TargetPosition = executionContext.TargetPosition,
            Blackboard = runtime.Blackboard.Clone(),
        };
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (!started || IsFinished || IsCancelled)
            return;

        if (ShouldInterruptByControl())
        {
            Interrupt(true);
            return;
        }

        var stage = runtime.Data.Stages[stageIndex];

        elapsedStageTime += dt;
        executionContext.DeltaTime = dt;
        executionContext.ElapsedStageTime = elapsedStageTime;
        executionContext.RemainingStageTime = stageTimer;
        executionContext.Stage = stage;
        executionContext.CurrentTick = currentTick;

        ExecuteModules(stage.OnTick);

        if (stage.Duration > 0)
        {
            stageTimer -= dt;
            if (stageTimer > 0)
                return;
        }

        ExitCurrentStage();

        stageIndex++;
        if (stageIndex >= runtime.Data.Stages.Length)
        {
            runtime.EnterCooldown();
            IsFinished = true;
            return;
        }

        EnterStage(currentTick);
    }

    public void Interrupt(bool byControl)
    {
        if (IsFinished || IsCancelled)
            return;

        if (started && runtime.Data.Stages != null && stageIndex < runtime.Data.Stages.Length)
            ExecuteModules(runtime.Data.Stages[stageIndex].OnExit);

        runtime.ReturnCost();
        runtime.EnterCooldown();
        IsCancelled = true;
    }

    public CastWindowType? ResolveCastWindow(int newAbilityId)
    {
        var stage = CurrentStage;
        if (stage?.CastWindows == null)
            return null;

        for (int i = 0; i < stage.CastWindows.Length; i++)
        {
            var rule = stage.CastWindows[i];
            if (rule.AbilityId == newAbilityId)
                return rule.Type;
        }

        return null;
    }

    public bool CanBeInterruptedBy(UnitOrder newOrder)
    {
        if (!started || IsFinished || IsCancelled)
            return true;

        if (newOrder is MoveOrder)
            return runtime.Data.CancelByMove;
        if (newOrder is AttackOrder)
            return runtime.Data.CancelByAttack;
        if (newOrder is CastOrder castOrder)
        {
            var window = ResolveCastWindow(castOrder.AbilityId);
            if (window.HasValue)
                return true;

            return runtime.Data.CancelByCast;
        }

        return true;
    }

    private void EnterStage(uint currentTick)
    {
        var stage = runtime.Data.Stages[stageIndex];
        stageTimer = (fp)stage.Duration;
        elapsedStageTime = 0;
        executionContext.Stage = stage;
        executionContext.ElapsedStageTime = 0;
        executionContext.RemainingStageTime = stageTimer;
        executionContext.StageEnterTick = currentTick;
        executionContext.CurrentTick = currentTick;
        owner.OnAbilityCastStagePerformed(runtime.Data.Id, stage.Type, executionContext.TargetUnit, executionContext.TargetPosition); owner.OnAbilityCastStagePerformed(runtime.Data.Id, stage.Type, executionContext.TargetUnit, executionContext.TargetPosition);
        ExecuteModules(stage.OnEnter);
    }

    private void ExitCurrentStage()
    {
        var stage = runtime.Data.Stages[stageIndex];
        executionContext.Stage = stage;
        ExecuteModules(stage.OnExit);
    }

    private void ExecuteModules(AbilityBaseMoudle[] modules)
    {
        if (modules == null)
            return;

        for (int i = 0; i < modules.Length; i++)
            modules[i].Apply(executionContext);
    }

    private void ResolveTarget()
    {
        executionContext.TargetUnit = null;
        executionContext.TargetPosition = triggerContext.TargetPosition;

        if (triggerContext.TargetUID.HasValue &&
            UnitManager.Instance.Spawns.TryGetValue(triggerContext.TargetUID.Value, out var unit))
        {
            executionContext.TargetUnit = unit;
            executionContext.TargetPosition = unit.LogicPosition;
        }
    }

    private bool ShouldInterruptByControl()
    {
        if (owner is not HeroUnit hero)
            return true;

        var snapshot = owner.CrowdControlHandler.CurrentSnapshot;
        if (runtime.Data.CancelByHardControl && (snapshot.ForceInterruptCast || snapshot.BlockCast))
            return true;

        if (hero.IsActionChannelBlocked(ActionChannelMask.Cast))
            return true;

        return false;
    }
}