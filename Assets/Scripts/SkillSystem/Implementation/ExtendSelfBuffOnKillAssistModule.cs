using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "ExtendSelfBuffOnKillAssistModule", menuName = "SkillSystem/Buff/Extend Self Buff On Kill Assist")]
public sealed class ExtendSelfBuffOnKillAssistModule : BuffBaseModule
{
    [LabelText("固定延长秒数")]
    public float FixedExtraSeconds = 5f;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null || context.Handler == null)
            return;

        var owner = context.Buff.target;
        int buffId = context.Buff.buffData.Id;

        void OnKill(KillEvent evt)
        {
            if (evt.Killer != owner)
                return;

            context.Handler.TryExtendBuff(buffId, (fp)FixedExtraSeconds);
        }

        void OnAssist(AssistEvent evt)
        {
            if (evt.Assistant != owner)
                return;

            context.Handler.TryExtendBuff(buffId, (fp)FixedExtraSeconds);
        }

        owner.KillPerformed += OnKill;
        owner.AssistPerformed += OnAssist;

        context.Buff.RegisterUndoAction(() =>
        {
            owner.KillPerformed -= OnKill;
            owner.AssistPerformed -= OnAssist;
        });
    }
}
