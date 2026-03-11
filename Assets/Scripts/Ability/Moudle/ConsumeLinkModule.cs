using UnityEngine;

[CreateAssetMenu(fileName = "Ability_ConsumeLink", menuName = "技能系统/模块/移除Link")]
public class ConsumeLinkModule : AbilityBaseMoudle
{
    public string LinkKey = "DefaultLink";

    public override void Apply(AbilityExecutionContext context)
    {
        if (context?.Caster == null)
            return;

        context.Caster.AbilityLinkController.RemoveLink(LinkKey);
    }
}