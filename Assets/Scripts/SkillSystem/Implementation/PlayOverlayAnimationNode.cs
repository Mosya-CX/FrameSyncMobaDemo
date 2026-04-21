using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "PlayOverlayAnimationNode", menuName = "SkillSystem/Animation/Play Overlay Animation Node")]
public sealed class PlayOverlayAnimationNode : SkillEffectNode
{
    [LabelText("全身覆盖")]
    public bool FullBodyOverride = true;

    [LabelText("替换同标签")]
    public bool ReplaceSameTag = true;

    [LabelText("动画请求")]
    public OverlayAnimRequest Request;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null || Request.ClipRef == null)
            return;

        var controller = context.Caster.GetComponent<UnitAnimationController>();
        if (controller == null)
            return;

        if (FullBodyOverride)
            controller.PlayFullBodyOverride(Request, context.CurrentTick);
        else
            controller.PlayStateLayer(Request, context.CurrentTick, ReplaceSameTag);
    }
}
