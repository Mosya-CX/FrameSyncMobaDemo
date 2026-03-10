using Unity.Mathematics.FixedPoint;

public sealed class CastExecution
{
    private readonly HeroUnit owner;
    private readonly AbilityRuntime runtime;
    private readonly AbilityTriggerContext context;

    private int stageIndex;
    private fp stageTimer;
    private bool started;

    public bool IsFinished { get; private set; }
    public bool IsCancelled { get; private set; }

    public CastExecution(HeroUnit owner, AbilityRuntime runtime, AbilityTriggerContext context)
    {
        this.owner = owner;
        this.runtime = runtime;
        this.context = context;
    }

    public void Start()
    {
        if (runtime.Data.Stages == null || runtime.Data.Stages.Length == 0)
        {
            runtime.PayCost();
            runtime.EnterCooldown();
            IsFinished = true;
            return;
        }

        if (!runtime.CanCommit(context))
        {
            IsCancelled = true;
            return;
        }

        runtime.PayCost();

        stageIndex = 0;
        started = true;
        EnterStage();
    }

    public void Tick(fp dt)
    {
        if (!started || IsFinished || IsCancelled)
            return;

        if (ShouldInterruptByControl())
        {
            Interrupt(true);
            return;
        }

        var stage = runtime.Data.Stages[stageIndex];

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

        EnterStage();
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

    public bool CanBeInterruptedBy(UnitOrder newOrder)
    {
        if (!started || IsFinished || IsCancelled)
            return true;

        if (newOrder is MoveOrder)
            return runtime.Data.CancelByMove;
        if (newOrder is AttackOrder)
            return runtime.Data.CancelByAttack;
        if (newOrder is CastOrder)
            return runtime.Data.CancelByCast;

        return true;
    }

    private void EnterStage()
    {
        var stage = runtime.Data.Stages[stageIndex];
        stageTimer = (fp)stage.Duration;
        ExecuteModules(stage.OnEnter);

        if (stage.Type == CastStageType.Execute)
        {
            // Execute 段开始时，如果需要，也可以把“正式触发”放这里
            // 你现有 AbilityBaseMoudle 都还是通过 OnEnter/OnTick/OnExit 调。
        }
    }

    private void ExitCurrentStage()
    {
        var stage = runtime.Data.Stages[stageIndex];
        ExecuteModules(stage.OnExit);
    }

    private void ExecuteModules(AbilityBaseMoudle[] modules)
    {
        if (modules == null)
            return;

        for (int i = 0; i < modules.Length; i++)
            modules[i].Apply(runtime);
    }

    private bool ShouldInterruptByControl()
    {
        var snapshot = owner.CrowdControlHandler.CurrentSnapshot;
        if (!runtime.Data.CancelByHardControl)
            return false;

        return snapshot.ForceInterruptCast || snapshot.BlockCast;
    }
}