using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class HeroUnit : UnitCore, ICommandReceiver
{
    public UnitUID ReceiverID => UnitID;
    private AbilityHandler abilityHandler;

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
                // ÔÝÊ±²»¹Ü
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
}
