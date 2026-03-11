using UnityEngine;

[CreateAssetMenu(fileName = "Ability_SetLinkContext", menuName = "技能系统/模块/设置Link")]
public class SetLinkContextModule : AbilityBaseMoudle
{
    public string LinkKey = "DefaultLink";
    public bool StoreTargetUnit = true;
    public bool StoreTargetPosition = true;

    public override void Apply(AbilityExecutionContext context)
    {
        if (context?.Caster == null)
            return;

        var link = new AbilityLinkContext
        {
            SourceAbilityId = context.Data.Id,
            SourceUnit = context.Caster,
            LinkedUnit = StoreTargetUnit ? context.TargetUnit : null,
            LinkedPosition = StoreTargetPosition ? context.TargetPosition : null,
            UserData = null,
            CreatedTick = context.CurrentTick,
        };

        context.Caster.AbilityLinkController.SetLink(LinkKey, link);
    }
}