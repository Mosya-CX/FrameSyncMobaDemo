using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "RemoveBuffIfOutsideStoredLadderModule", menuName = "SkillSystem/Buff/Remove If Outside Stored Ladder")]
public sealed class RemoveBuffIfOutsideStoredLadderModule : BuffBaseModule
{
    [LabelText("原点X键")] public string OriginXKey = "Trap.OriginX";
    [LabelText("原点Z键")] public string OriginZKey = "Trap.OriginZ";
    [LabelText("朝向X键")] public string DirXKey = "Trap.DirX";
    [LabelText("朝向Z键")] public string DirZKey = "Trap.DirZ";
    [LabelText("底宽键")] public string BottomKey = "Trap.Bottom";
    [LabelText("顶宽键")] public string TopKey = "Trap.Top";
    [LabelText("高度键")] public string HeightKey = "Trap.Height";
    [LabelText("区域内标记键")] public string InsideKey = "Trap.Inside";

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null || context.Handler == null)
            return;

        var buff = context.Buff;
        var target = buff.target;

        if (!TryGetFp(buff, OriginXKey, out var ox) ||
            !TryGetFp(buff, OriginZKey, out var oz) ||
            !TryGetFp(buff, DirXKey, out var dx) ||
            !TryGetFp(buff, DirZKey, out var dz) ||
            !TryGetFp(buff, BottomKey, out var bottom) ||
            !TryGetFp(buff, TopKey, out var top) ||
            !TryGetFp(buff, HeightKey, out var height))
            return;

        bool inside = CheckInside(
            new fp2(target.LogicPosition.x, target.LogicPosition.z),
            new fp2(ox, oz),
            new fp2(dx, dz),
            bottom, top, height);

        buff.blackBoard[InsideKey] = inside;

        if (!inside)
            context.Handler.TryRemoveBuff(buff.buffData.Id);
    }

    private static bool CheckInside(fp2 pos, fp2 origin, fp2 forward, fp bottom, fp top, fp height)
    {
        if (fpmath.lengthsq(forward) <= fp.zero)
            return false;

        forward = fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);

        fp2 v = pos - origin;
        fp f = fpmath.dot(v, forward);
        fp r = fpmath.dot(v, right);

        if (f < fp.zero || f > height)
            return false;

        fp halfBottom = bottom / 2;
        fp halfTop = top / 2;
        fp t = height > fp.zero ? f / height : fp.zero;
        fp halfWidth = halfBottom + (halfTop - halfBottom) * t;

        return fpmath.abs(r) <= halfWidth;
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
}
