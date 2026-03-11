using Unity.Mathematics.FixedPoint;

public class HeroUnit : CombatUnitBase, ICommandReceiver, ITurretTargetInfo
{
    protected HeroInputHandler inputHandler;

    public OrderController OrderController { get; private set; }
    public DashMotor DashMotor { get; private set; }
    public AbilityLinkController AbilityLinkController { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        inputHandler = GetComponentInChildren<HeroInputHandler>(true);
        OrderController = new OrderController(this);
        DashMotor = new DashMotor(this);
        AbilityLinkController = new AbilityLinkController(this);
    }

    public UnitUID ReceiverID => UnitID;

    public override void OnSpawn(UnitUID instanceUid, int startLevel = 1)
    {
        base.OnSpawn(instanceUid, startLevel);
        FrameSyncCoreSystem.Instance?.RegisterReceiver(this);
    }

    public override void OnDespawn()
    {
        FrameSyncCoreSystem.Instance?.UnregisterReceiver(UnitID);
        base.OnDespawn();
    }

    public override void Tick(fp dt, uint currentTick)
    {
        base.Tick(dt, currentTick);

        DashMotor.Tick(dt);
        OrderController.Tick(dt, currentTick);
    }

    public override bool IsActionChannelBlocked(ActionChannelMask channel)
    {
        if (base.IsActionChannelBlocked(channel))
            return true;

        var dashLocks = DashMotor.BuildActionLockSnapshot();
        return dashLocks.IsBlocked(channel);
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

    public void SetDestinationByOrder(fp3 targetPosition)
    {
        BeginMoveTo(targetPosition);
    }

    public void SetTargetByOrder(UnitCore target)
    {
        BeginTrackTarget(target);
    }

    public void StopMoveByOrder()
    {
        StopCurrentAction();
    }

    protected override bool DashBlocked()
    {
        return DashMotor.IsDashing;
    }

    protected override void TickNonCombat(fp dt)
    {
        if (!IsDead && LocomotionState != UnitLocomotionState.Idle)
            SetLocomotionState(UnitLocomotionState.Idle);
    }

    protected override void OnDeadEnter()
    {
        base.OnDeadEnter();

        StopCurrentAction();
        DashMotor.Cancel();
        OrderController.ClearBufferedOrders();
        OrderController.ClearSuspendedOrder();
        inputHandler?.CancelCurrentIndicator();
    }

    public bool IsHero => true;
    public bool IsSummonedUnit => false;
    public bool IsSiegeOrSuperMinion => false;
    public bool IsLaneMinion => false;
    public bool IsMonster => false;

    public bool IsAttackingTarget(UnitCore target)
    {
        if (target == null)
            return false;

        return CurrentCombatMode == CombatMode.AttackTarget && CurrentTarget == target;
    }

    public override SimulationEntityType SimulationEntityType => SimulationEntityType.Hero;

    public override object CaptureState()
    {
        var baseState = (CombatUnitSnapshot)base.CaptureState();

        return new HeroUnitSnapshot
        {
            Base = baseState,
            DashState = DashMotor.CaptureState(),
            OrderState = OrderController.CaptureState(),
            LinkState = AbilityLinkController.CaptureState(),
        };
    }

    public override void RestoreState(object state)
    {
        var snap = (HeroUnitSnapshot)state;

        base.RestoreState(snap.Base);
        DashMotor.RestoreState(snap.DashState);
        OrderController.RestoreState(snap.OrderState);
        AbilityLinkController.RestoreState(snap.LinkState);
    }

    [System.Serializable]
    public struct HeroUnitSnapshot
    {
        public CombatUnitSnapshot Base;
        public object DashState;
        public object OrderState;
        public object LinkState;
    }
}