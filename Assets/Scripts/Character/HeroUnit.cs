using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class HeroUnit : UnitCore, ICommandReceiver
{
    public UnitUID ReceiverID => UnitID;

    private AbilityCastContext? preparedCastAbility;

    protected override void Awake()
    {
        base.Awake();
        abilityHandler = GetComponent<AbilityHandler>();
    }

    public void ReceiveCommand(ICommand command)
    {
        switch (command.Type)
        {
            case CommandType.Move:
                HandleMoveCommand((MoveCommand)command);
                break;
            case CommandType.Attack:
                HandleAttackCommand((AttackCommand)command);
                break;
            case CommandType.AbilityPress:
            case CommandType.AbilityRelease:
            case CommandType.AbilityCancel:  
                HandleAbilityCommand((AbilityCommand)command);
                break;
            case CommandType.PurchaseItem:
                // 暂时不管
                break;
        }
    }

    private void HandleAbilityCommand(AbilityCommand cmd)
    {
        AbilityCastContext context = new AbilityCastContext
        {
            Caster = UnitID,
            TargetUnit = cmd.HasTargetUnit ? cmd.TargetUnit : null,
            TargetPosition = cmd.HasTargetPosition ? cmd.TargetPosition : default
        };

        // TODO
        // 施法满足施法判断
        // 如果施法需要满足距离判定且未满足时则将施法信息缓存起来
        // 然后根据未满足的距离是TargetUnit还是TargetPosition，切换至Track或Move状态

        switch (cmd.Type)
        {
            case CommandType.AbilityPress:
                abilityHandler.PressSkill(cmd.AbilityId, context);
                break;

            case CommandType.AbilityRelease:
                abilityHandler.ReleaseSkill(cmd.AbilityId, context);
                break;

            case CommandType.AbilityCancel:
                abilityHandler.CancelSkill(cmd.AbilityId);
                break;
        }
    }

    private void HandleMoveCommand(MoveCommand cmd)
    {

    }

    private void HandleAttackCommand(AttackCommand cmd)
    {

    }

    protected override void OnMoveTick(fp dt)
    {
        if (!currentDestination.HasValue)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        if (preparedCastAbility.HasValue)
        {
            // TODO
            // 判定施法距离
            // 达到后施法

        }
        else
        {
            if (IsReach(currentDestination.Value, 0.01m))
            {
                OnReachDestination?.Invoke();
                currentDestination = null;
                ChangeActionState(UnitActionState.Idle);
                return;
            }
        }

        ApplyRotateByDir();
    }

    protected override void OnMoveExit()
    {
        base.OnMoveExit();
        preparedCastAbility = null;
    }

    protected override void OnTrackExit()
    {
        base.OnTrackExit();
        preparedCastAbility = null;
    }

    protected override void OnTrackTick(fp dt)
    {
        if (currentTarget == null)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        if (preparedCastAbility.HasValue)
        {
            // TODO
            // 判定施法距离
            // 达到后施法

        }
        else
        {
            if (IsReach(currentTarget.LogicPosition, stats.RealAttackDistance))
            {
                OnTrackCompleted?.Invoke();
                ChangeActionState(UnitActionState.Attack);
                return;
            }
        }

        ApplyRotateByDir();
    }

    public override void UpdateMoveDirection()
    {
        switch (currentActionState)
        {
            case UnitActionState.Move:
                if (currentDestination.HasValue)
                {
                    // TODO
                    // 根据路径更新方向

                }
                break;
            case UnitActionState.Track:
                if (currentTarget)
                {
                    // TODO
                    // 根据路径更新方向

                }
                break;
        }
    }

    public override void UpdateAStarPath()
    {
        switch (currentActionState)
        {
            case UnitActionState.Move:
                if (currentDestination.HasValue)
                {
                    // TODO
                    // 更新路径

                }
                break;
            case UnitActionState.Track:
                if (currentTarget)
                {
                    // TODO
                    // 更新路径

                }
                break;
        }
    }
}
