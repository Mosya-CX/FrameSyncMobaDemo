using UnityEngine;

[CreateAssetMenu(fileName = "Ability_StoreValueModule", menuName = "技能系统/模块/写入Blackboard")]
public class StoreValueInBlackboardModule : AbilityBaseMoudle
{
    public string Key = "LastTargetPosition";
    public BlackboardStoreMode Mode = BlackboardStoreMode.TargetPosition;

    public override void Apply(AbilityExecutionContext context)
    {
        if (context == null)
            return;

        switch (Mode)
        {
            case BlackboardStoreMode.TargetPosition:
                if (context.TargetPosition.HasValue)
                    context.Blackboard.Set(Key, context.TargetPosition.Value);
                break;

            case BlackboardStoreMode.CasterPosition:
                context.Blackboard.Set(Key, context.Caster.LogicPosition);
                break;

            case BlackboardStoreMode.TargetUnit:
                if (context.TargetUnit != null)
                    context.Blackboard.Set(Key, context.TargetUnit.UnitID);
                break;
        }
    }
}

public enum BlackboardStoreMode : byte
{
    TargetPosition,
    CasterPosition,
    TargetUnit,
}