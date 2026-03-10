using Unity.Mathematics.FixedPoint;

public sealed class CastOrder : UnitOrder
{
    private readonly AbilityCommand command;
    private AbilityRuntime runtime;
    private CastExecution execution;
    private UnitCore targetUnit;

    private enum State
    {
        Prepare,
        Approaching,
        Executing,
    }

    private State state;

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

        if (Owner.CrowdControlHandler.CurrentSnapshot.BlockCast)
        {
            IsCancelled = true;
            return;
        }

        state = State.Prepare;
    }

    public override void Tick(fp dt)
    {
        switch (state)
        {
            case State.Prepare:
                TickPrepare();
                break;
            case State.Approaching:
                TickApproaching();
                break;
            case State.Executing:
                execution.Tick(dt);
                if (execution.IsFinished)
                    IsFinished = true;
                else if (execution.IsCancelled)
                    IsCancelled = true;
                break;
        }
    }

    public override bool CanBeInterruptedBy(UnitOrder newOrder)
    {
        if (state == State.Approaching)
            return true;

        return execution == null || execution.CanBeInterruptedBy(newOrder);
    }

    public override void Cancel()
    {
        execution?.Interrupt(false);
        base.Cancel();
    }

    private void TickPrepare()
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

        BeginExecution();
    }

    private void TickApproaching()
    {
        var snapshot = Owner.CrowdControlHandler.CurrentSnapshot;
        if (snapshot.BlockMove || snapshot.BlockCast)
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

        ((HeroUnit)Owner).StopMoveByOrder();
        BeginExecution();
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

    private void BeginExecution()
    {
        execution = new CastExecution((HeroUnit)Owner, runtime, command.Context);
        execution.Start();
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
}