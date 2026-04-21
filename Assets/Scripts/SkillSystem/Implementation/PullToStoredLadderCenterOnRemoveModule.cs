using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "PullToStoredLadderCenterOnRemoveModule", menuName = "SkillSystem/Buff/Pull To Stored Ladder Center OnRemove")]
public sealed class PullToStoredLadderCenterOnRemoveModule : BuffBaseModule
{
    [LabelText("区域内标记键")] public string InsideKey = "Trap.Inside";
    [LabelText("中心X键")] public string CenterXKey = "Trap.CenterX";
    [LabelText("中心Y键")] public string CenterYKey = "Trap.CenterY";
    [LabelText("中心Z键")] public string CenterZKey = "Trap.CenterZ";
    [LabelText("二段伤害键")] public string DamageKey = "Trap.SecondDamage";

    [LabelText("击飞控制")]
    public CrowdControlData KnockupControl;

    [LabelText("击飞时间")]
    public float KnockupDuration = 0.25f;

    [LabelText("额外标签")]
    public string[] AdditionalTags;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null || context.Buff.source == null)
            return;

        var buff = context.Buff;
        if (!TryGetBool(buff, InsideKey, out bool inside) || !inside)
            return;

        if (!TryGetFp(buff, CenterXKey, out var cx) ||
            !TryGetFp(buff, CenterYKey, out var cy) ||
            !TryGetFp(buff, CenterZKey, out var cz))
            return;

        var target = buff.target;
        var source = buff.source;
        var center = new fp3(cx, cy, cz);

        target.LogicPosition = center;

        if (KnockupControl != null)
            target.CrowdControlHandler?.AddControl(KnockupControl, (fp)KnockupDuration, source);

        if (TryGetFp(buff, DamageKey, out var damage) && damage > fp.zero)
        {
            DamageManager.Instance.CreateAbilityDamageRequest(
                source,
                target,
                damage,
                fp.zero,
                AdditionalTags);
        }
    }

    private static bool TryGetFp(BuffInfo buff, string key, out fp value)
    {
        if (buff != null && buff.blackBoard != null && buff.blackBoard.TryGetValue(key, out var obj) && obj is fp fpv)
        {
            value = fpv;
            return true;
        }

        value = fp.zero;
        return false;
    }

    private static bool TryGetBool(BuffInfo buff, string key, out bool value)
    {
        if (buff != null && buff.blackBoard != null && buff.blackBoard.TryGetValue(key, out var obj) && obj is bool b)
        {
            value = b;
            return true;
        }

        value = false;
        return false;
    }
}
