using Unity.Mathematics.FixedPoint;

public class HeroUnit : UnitCore, ICommandReceiver
{
    protected HeroInputHandler inputHandler;
    public OrderController OrderController { get; private set; }
    public DashMotor DashMotor { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        inputHandler = GetComponentInChildren<HeroInputHandler>(true);
        OrderController = new OrderController(this);
        DashMotor = new DashMotor(this);
    }

    public UnitUID ReceiverID => UnitID;

    public override void Tick(fp dt)
    {
        base.Tick(dt);
        DashMotor.Tick(dt);
        OrderController.Tick(dt);
    }

    public void ReceiveCommand(CommandBase command)
    {
        switch (command.GetCommandType())
        {
            case CommandType.Move:
                OrderController.Submit(new MoveOrder(this, ((MoveCommand)command).TargetPosition));
                break;

            case CommandType.Attack:
                OrderController.Submit(new AttackOrder(this, ((AttackCommand)command).TargetUnitId));
                break;

            case CommandType.TriggerAbility:
                var abilityCmd = (AbilityCommand)command;
                OrderController.Submit(new CastOrder(this, abilityCmd), abilityCmd.QueueIfBusy);
                break;
        }
    }

    protected override void OnSiffnessEnter()
    {
        base.OnSiffnessEnter();
        inputHandler?.CancelCurrentIndicator();
    }

    public void SetDestinationByOrder(fp3 targetPosition)
    {
        currentTarget = null;
        currentDestination = targetPosition;
        pathFinder.SetDestination(targetPosition);
        ChangeActionState(UnitActionState.Move);
    }

    public void SetTargetByOrder(UnitCore target)
    {
        currentDestination = null;
        currentTarget = target;
        pathFinder.SetTarget(target);
        ChangeActionState(UnitActionState.Track);
    }

    public void StopMoveByOrder()
    {
        currentDestination = null;
        pathFinder.Stop();
        ChangeActionState(UnitActionState.Idle);
    }

    protected override void OnMoveTick(fp dt)
    {
        if (!currentDestination.HasValue)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        if (IsReach(currentDestination.Value, 0.01m))
        {
            currentDestination = null;
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        UpdateRotation();
    }
}