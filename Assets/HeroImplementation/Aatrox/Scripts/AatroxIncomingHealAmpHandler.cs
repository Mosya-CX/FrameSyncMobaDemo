using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

public sealed class AatroxIncomingHealAmpHandler : UnitBaseHandler, IHealModifierProvider
{
    [LabelText("R技能ID")]
    public int RSkillId = 1005;

    [LabelText("世界终结Buff ID")]
    public int WorldEnderBuffId = 2001;

    [LabelText("治疗效果提升(1~3级)")]
    public float[] HealAmpByRLevel = new float[3] { 0.30f, 0.45f, 0.60f };

    public override void Tick(fp deltaTime) { }

    public void ModifyOutgoingHeal(HealContext context) { }

    public void ModifyIncomingHeal(HealContext context)
    {
        if (owner == null || context == null)
            return;

        if (context.Target != owner || context.Source != owner)
            return;

        if (owner.BuffHandler == null || !owner.BuffHandler.TryGetBuff(WorldEnderBuffId, out _))
            return;

        var book = owner.GetComponent<SkillBook>();
        if (book == null || !book.TryGetRuntime(RSkillId, out var runtime))
            return;

        int index = HealAmpByRLevel != null && HealAmpByRLevel.Length > 0
            ? Mathf.Clamp(runtime.Level, 1, HealAmpByRLevel.Length) - 1
            : 0;

        fp amp = HealAmpByRLevel != null && HealAmpByRLevel.Length > 0 ? (fp)HealAmpByRLevel[index] : fp.zero;
        if (amp > fp.zero)
            context.HealMultiplier *= (fp.one + amp);
    }

    public override object CaptureState() => null;
    public override void RestoreState(object state) { }
}
