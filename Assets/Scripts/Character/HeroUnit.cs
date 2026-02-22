using UnityEngine;

public class HeroUnit : UnitCore, ICommandReceiver
{
    public UnitUID ReceiverID => UnitID;

    public void ReceiveCommand(ICommand command)
    {
        switch (command.Type)
        {
            case CommandType.AbilityPress:
            case CommandType.AbilityRelease:
            case CommandType.AbilityCancel:
                HandleAbilityCommand((AbilityCommand)command);
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
                AbilityHandler.PressSkill(cmd.AbilityId, context);
                break;

            case CommandType.AbilityRelease:
                AbilityHandler.ReleaseSkill(cmd.AbilityId, context);
                break;

            case CommandType.AbilityCancel:
                AbilityHandler.CancelSkill(cmd.AbilityId);
                break;
        }
    }
}
