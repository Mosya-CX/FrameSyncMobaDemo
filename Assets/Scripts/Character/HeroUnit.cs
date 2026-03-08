using Unity.Mathematics.FixedPoint;

public class HeroUnit : UnitCore, ICommandReceiver
{
    protected HeroInputHandler inputHandler;

    protected override void Awake()
    {
        base.Awake();
        inputHandler = GetComponentInChildren<HeroInputHandler>(true);
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

    public void SetDestination(fp3 targetPosition)
    {
        currentTarget = null;
        currentDestination = targetPosition;
        pathFinder.SetDestination(currentDestination.Value);
        ChangeActionState(UnitActionState.Move);
    }

    public void SetTarget(UnitCore target)
    {
        currentDestination = null;
        currentTarget = target;
        pathFinder.SetTarget(currentTarget);
        ChangeActionState(UnitActionState.Track);
    }

    #region 命令解析和处理
    public UnitUID ReceiverID => UnitID;

    public void ReceiveCommand(CommandBase command)
    {
        switch (command.GetCommandType())
        {
            case CommandType.Move:
                HandleMoveCommand((MoveCommand)command);
                break;
            case CommandType.Attack:
                HandleAttackCommand((AttackCommand)command);
                break;
            case CommandType.TriggerAbility:
                HandleAbilityCommand((AbilityCommand)command);
                break;
            case CommandType.BuyItem:
                break;
            case CommandType.SellItem:
                break;
            case CommandType.UseItem:
                break;
        }
    }

    protected override void OnSiffnessEnter()
    {
        base.OnSiffnessEnter();
        inputHandler?.CancelCurrentIndicator();
    }

    private void HandleAbilityCommand(AbilityCommand cmd)
    {
        AbilityHandler.TriggerAbility(cmd.AbilityId, cmd.context);
    }

    private void HandleMoveCommand(MoveCommand cmd)
    {
        SetDestination(cmd.TargetPosition);
    }

    private void HandleAttackCommand(AttackCommand cmd)
    {
        if (UnitManager.Instance.Spawns.TryGetValue(cmd.TargetUnitId, out var target))
            SetTarget(target);
    }
    #endregion
}
