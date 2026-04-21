using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "PlayOverlayAnimationBuffModule", menuName = "SkillSystem/Buff/Play Overlay Animation")]
public sealed class PlayOverlayAnimationBuffModule : BuffBaseModule
{
    [LabelText("全身覆盖")]
    public bool FullBodyOverride = false;

    [LabelText("替换同标签")]
    public bool ReplaceSameTag = true;

    [LabelText("动画请求")]
    public OverlayAnimRequest Request;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null || Request.ClipRef == null)
            return;

        var controller = context.Buff.target.GetComponent<UnitAnimationController>();
        if (controller == null)
            return;

        uint tick = UnitManager.Instance != null ? UnitManager.Instance.CurrentTick : 0;

        if (FullBodyOverride)
        {
            controller.PlayFullBodyOverride(Request, tick);
            context.Buff.RegisterUndoAction(() => controller.StopFullBodyOverride());
        }
        else
        {
            controller.PlayStateLayer(Request, tick, ReplaceSameTag);
            if (!string.IsNullOrEmpty(Request.Tag))
                context.Buff.RegisterUndoAction(() => controller.RemoveStateLayersByTag(Request.Tag));
        }
    }
}
