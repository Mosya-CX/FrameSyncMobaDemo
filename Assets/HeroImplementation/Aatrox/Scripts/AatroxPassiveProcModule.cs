using System;
using System.Linq;
using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "AatroxPassiveProcModule", menuName = "Aatrox/Buff/Passive Proc Module")]
public sealed class AatroxPassiveProcModule : BuffBaseModule
{
    [LabelText("被动技能ID")]
    public int PassiveSkillId = 1001;

    [LabelText("移除自身BuffId")]
    public int PassiveReadyBuffId;

    [LabelText("按英雄等级的最大生命值百分比(1~18)")]
    public float[] PercentByChampionLevel = new float[18]
    {
        0.04f, 0.044f, 0.048f, 0.052f, 0.056f, 0.060f,
        0.064f, 0.068f, 0.072f, 0.076f, 0.080f, 0.084f,
        0.088f, 0.092f, 0.096f, 0.100f, 0.110f, 0.120f
    };

    [LabelText("回血比例")]
    public float HealRatio = 0.8f;

    [LabelText("命中后额外缩减CD(秒)")]
    public float CooldownRefundOnProcHit = 0f;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null)
            return;

        var owner = context.Buff.target;

        Action<DamageDealtEvent> handler = evt =>
        {
            if (evt.Source != owner || evt.Target == null)
                return;

            var tags = evt.Result.Tags;
            if (tags == null)
                return;

            if (tags.Contains(DamageTagConst.FromAttack) && !tags.Contains(AatroxTagConst.Passive))
            {
                fp bonus = EvaluateProcDamage(owner, evt.Result.Target);
                if (bonus > fp.zero)
                    DamageManager.Instance.CreateAbilityDamageRequest(owner, evt.Result.Target, bonus, fp.zero, AatroxTagConst.Passive, DamageTagConst.ProcDamage);

                var book = owner.GetComponent<SkillBook>();
                if (book != null && book.TryGetRuntime(PassiveSkillId, out var runtime))
                    runtime.StartCooldown();

                if (PassiveReadyBuffId != 0 && owner.BuffHandler != null)
                    owner.BuffHandler.TryRemoveBuff(PassiveReadyBuffId);

                return;
            }

            if (tags.Contains(AatroxTagConst.Passive))
            {
                if (evt.Result.TotalDamage > fp.zero && HealRatio > 0f)
                    HealManager.Instance.CreateHealRequest(owner, owner, evt.Result.TotalDamage * (fp)HealRatio, AatroxTagConst.Passive);

                if (CooldownRefundOnProcHit > 0f)
                {
                    var book = owner.GetComponent<SkillBook>();
                    if (book != null && book.TryGetRuntime(PassiveSkillId, out var runtime))
                        runtime.ModifyCooldown(-(fp)CooldownRefundOnProcHit);
                }
            }
        };

        owner.DamageDealt += handler;
        context.Buff.RegisterUndoAction(() => owner.DamageDealt -= handler);
    }

    private fp EvaluateProcDamage(UnitCore caster, UnitCore target)
    {
        if (caster == null || target == null)
            return fp.zero;

        int level = Mathf.Clamp(caster.Level, 1, Mathf.Max(1, PercentByChampionLevel != null ? PercentByChampionLevel.Length : 1)) - 1;
        fp pct = PercentByChampionLevel != null && PercentByChampionLevel.Length > 0
            ? (fp)PercentByChampionLevel[Mathf.Clamp(level, 0, PercentByChampionLevel.Length - 1)]
            : fp.zero;

        return target.MaxHealth * pct;
    }
}
