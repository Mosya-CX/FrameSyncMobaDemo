using Unity.Mathematics.FixedPoint;

public sealed class CastOrder : UnitOrder
{
    private readonly AbilityCommand command;
    private AbilityRuntime runtime;
    private CastExecution execution;
    private UnitCore targetUnit;

    private CastExecutionSnapshot pausedSnapshot;

    private enum State
    {
        Prepare,
        Approaching,
        Executing,
        PausedForInsertedCast,
    }

    private State state;

    public int AbilityId => command.AbilityId;
    public bool QueueIfBusy => command.QueueIfBusy;
    public AbilityTriggerContext CommandContext => command.Context;
    public bool HasPausedSnapshot => pausedSnapshot != null;

    public CastOrder(HeroUnit owner, AbilityCommand command) : base(owner)
    {
        this.command = command;
    }

    public override void OnEnter()
    {
        if (!Owner.AbilityHandler.TryGetRuntime(command.AbilityId, out runtime))
        {
            IsCancelled = true;
            return;
        }

        if (Owner is not HeroUnit hero || !hero.CanStartCast())
        {
            IsCancelled = true;
            return;
        }

        state = pausedSnapshot != null ? State.PausedForInsertedCast : State.Prepare;
    }

    public void Tick(fp dt, uint currentTick)
    {
        switch (state)
        {
            case State.Prepare:
                TickPrepare(currentTick);
                break;

            case State.Approaching:
                TickApproaching(currentTick);
                break;

            case State.Executing:
                execution.Tick(dt, currentTick);
                if (execution.IsFinished)
                    IsFinished = true;
                else if (execution.IsCancelled)
                    IsCancelled = true;
                break;

            case State.PausedForInsertedCast:
                break;
        }
    }

    public override bool CanBeInterruptedBy(UnitOrder newOrder)
    {
        if (state == State.Approaching)
            return true;

        if (state == State.Executing && newOrder is CastOrder newCast && execution != null)
        {
            var window = execution.ResolveCastWindow(newCast.AbilityId);
            if (window.HasValue)
                return true;
        }

        return execution == null || execution.CanBeInterruptedBy(newOrder);
    }

    public CastWindowType? ResolveCastWindow(int newAbilityId)
    {
        if (state != State.Executing || execution == null)
            return null;

        return execution.ResolveCastWindow(newAbilityId);
    }

    public CastExecutionSnapshot PauseForInsert()
    {
        if (state != State.Executing || execution == null)
            return null;

        pausedSnapshot = execution.CreateSnapshot();
        state = State.PausedForInsertedCast;
        return pausedSnapshot;
    }

    public void ResumeFromPausedSnapshot()
    {
        if (pausedSnapshot == null)
        {
            IsCancelled = true;
            return;
        }

        execution = new CastExecution(
            (HeroUnit)Owner,
            pausedSnapshot.Runtime,
            pausedSnapshot.TriggerContext,
            pausedSnapshot.CastStartTick);

        execution.RestoreFromSnapshot(pausedSnapshot);
        pausedSnapshot = null;
        state = State.Executing;
    }

    public override void Cancel()
    {
        execution?.Interrupt(false);
        base.Cancel();
    }

    public object CaptureOrderState()
    {
        return new CastOrderStateSnapshot
        {
            StateValue = (byte)state,
            HasPausedSnapshot = pausedSnapshot != null,
            PausedSnapshot = pausedSnapshot,
            HasExecutionSnapshot = execution != null && state == State.Executing,
            ExecutionSnapshot = execution != null && state == State.Executing ? execution.CreateSnapshot() : null,
        };
    }

    public void RestoreOrderState(object stateObj)
    {
        if (stateObj is not CastOrderStateSnapshot snap)
            return;

        state = (State)snap.StateValue;
        pausedSnapshot = snap.HasPausedSnapshot ? snap.PausedSnapshot : null;

        if (snap.HasExecutionSnapshot && snap.ExecutionSnapshot != null)
        {
            if (Owner.AbilityHandler.TryGetRuntime(command.AbilityId, out runtime))
            {
                execution = new CastExecution(
                    (HeroUnit)Owner,
                    runtime,
                    snap.ExecutionSnapshot.TriggerContext,
                    snap.ExecutionSnapshot.CastStartTick);

                execution.RestoreFromSnapshot(snap.ExecutionSnapshot);
            }
        }
        else
        {
            execution = null;
        }
    }

    private void TickPrepare(uint currentTick)
    {
        if (!runtime.CanCommit(command.Context))
        {
            IsCancelled = true;
            return;
        }

        if (!TryResolveTargetAndRange(out bool needApproach))
        {
            IsCancelled = true;
            return;
        }

        if (needApproach)
        {
            if (!runtime.Data.AllowAutoApproach)
            {
                IsCancelled = true;
                return;
            }

            BeginApproach();
            state = State.Approaching;
            return;
        }

        BeginExecution(currentTick);
    }

    private void TickApproaching(uint currentTick)
    {
        if (Owner is not HeroUnit hero || !hero.CanStartMove() || !hero.CanStartCast())
        {
            IsCancelled = true;
            return;
        }

        if (!TryResolveTargetAndRange(out bool needApproach))
        {
            IsCancelled = true;
            return;
        }

        if (needApproach)
            return;

        hero.StopMoveByOrder();
        BeginExecution(currentTick);
    }

    private void BeginApproach()
    {
        if (command.Context.TargetUID.HasValue &&
            UnitManager.Instance.Spawns.TryGetValue(command.Context.TargetUID.Value, out var unit))
        {
            targetUnit = unit;
            ((HeroUnit)Owner).SetTargetByOrder(targetUnit);
            return;
        }

        if (command.Context.TargetPosition.HasValue)
            ((HeroUnit)Owner).SetDestinationByOrder(command.Context.TargetPosition.Value);
    }

    private void BeginExecution(uint currentTick)
    {
        execution = new CastExecution((HeroUnit)Owner, runtime, command.Context, currentTick);
        execution.Start(currentTick);
        state = State.Executing;
    }

    private bool TryResolveTargetAndRange(out bool needApproach)
    {
        needApproach = false;

        var targetMode = runtime.Data.TargetMode;
        var castRange = (fp)runtime.Data.CastRange;
        var selfPos = Owner.LogicPosition;

        if (targetMode == AbilityTargetMode.None)
            return true;

        if (command.Context.TargetUID.HasValue)
        {
            if (!UnitManager.Instance.Spawns.TryGetValue(command.Context.TargetUID.Value, out var unit) || unit == null)
                return false;

            targetUnit = unit;
            fp distSq = fpmath.lengthsq(unit.LogicPosition - selfPos);
            needApproach = distSq > castRange * castRange;
            return true;
        }

        if (command.Context.TargetPosition.HasValue)
        {
            fp distSq = fpmath.lengthsq(command.Context.TargetPosition.Value - selfPos);
            needApproach = distSq > castRange * castRange;
            return true;
        }

        return targetMode == AbilityTargetMode.None;
    }

    [System.Serializable]
    public struct CastOrderStateSnapshot
    {
        public byte StateValue;
        public bool HasPausedSnapshot;
        public CastExecutionSnapshot PausedSnapshot;
        public bool HasExecutionSnapshot;
        public CastExecutionSnapshot ExecutionSnapshot;
    }
}