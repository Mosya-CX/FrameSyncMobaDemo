using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class HeroUnit : CombatUnitBase, ICommandReceiver, ITurretTargetInfo
{
    public OrderController OrderController { get; private set; }

    private SkillGroupController skillGroupController;
    private PlayerSkillInputController playerSkillInputController;
    private SkillIndicatorDriver skillIndicatorDriver;

    public SkillGroupController SkillGroupController => skillGroupController;
    public PlayerSkillInputController PlayerSkillInputController => playerSkillInputController;
    public SkillIndicatorDriver SkillIndicatorDriver => skillIndicatorDriver;


    protected override void Awake()
    {
        base.Awake();

        OrderController = new OrderController(this);
        skillGroupController = GetComponent<SkillGroupController>();
        playerSkillInputController = GetComponent<PlayerSkillInputController>();
        skillIndicatorDriver = GetComponent<SkillIndicatorDriver>();
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
        OrderController.Tick(dt);
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
            case CommandType.TriggerSkill:
                var skillCmd = (SkillCastCommand)command;

                var request = new SkillCastRequest
                {
                    CasterUid = UnitID,
                    SkillId = skillCmd.SkillId,
                    IsPreview = false,
                    SmartCast = skillCmd.SmartCast,
                    TargetUnitUid = skillCmd.Context.TargetUID,
                    TargetPoint = skillCmd.Context.TargetPosition,
                    AimDirection = skillCmd.Context.TargetPosition.HasValue ? fpmath.normalize(skillCmd.Context.TargetPosition.Value - LogicPosition) : fp3.zero,
                    RequestTick = skillCmd.RequestTick,
                };

                SkillCommandResolver.TrySubmit(this, request);
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

    protected override void TickNonCombat(fp dt)
    {
        if (!IsDead && LocomotionState != UnitLocomotionState.Idle)
            SetLocomotionState(UnitLocomotionState.Idle);
    }

    protected override void OnDeadEnter()
    {
        base.OnDeadEnter();

        StopCurrentAction();
        OrderController.ClearBufferedOrders();
        OrderController.ClearSuspendedOrder();
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

    public void IssueMoveOrder(fp3 destination)
    {
        OrderController?.Submit(new MoveOrder(this, destination));
    }

    public void IssueAttackOrder(UnitCore target)
    {
        if (target == null)
            return;

        OrderController?.Submit(new AttackOrder(this, target.UnitID));
    }

    public override object CaptureState()
    {
        var baseState = (CombatUnitSnapshot)base.CaptureState();

        return new HeroUnitSnapshot
        {
            Base = baseState,
            OrderState = OrderController.CaptureState(),
            SkillGroupState = skillGroupController != null ? skillGroupController.CaptureState() : null,
        };
    }

    public override void RestoreState(object state)
    {
        var snap = (HeroUnitSnapshot)state;

        base.RestoreState(snap.Base);

        OrderController.RestoreState(snap.OrderState);

        if (skillGroupController != null && snap.SkillGroupState != null)
            skillGroupController.RestoreState(snap.SkillGroupState);
    }

    [System.Serializable]
    public struct HeroUnitSnapshot
    {
        public CombatUnitSnapshot Base;
        public object OrderState;
        public object SkillGroupState; 
    }
}