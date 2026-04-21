using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

public static class AatroxTrapKeys
{
    public const string OriginX = "Trap.OriginX";
    public const string OriginZ = "Trap.OriginZ";
    public const string DirX = "Trap.DirX";
    public const string DirZ = "Trap.DirZ";
    public const string Bottom = "Trap.Bottom";
    public const string Top = "Trap.Top";
    public const string Height = "Trap.Height";
    public const string CenterX = "Trap.CenterX";
    public const string CenterY = "Trap.CenterY";
    public const string CenterZ = "Trap.CenterZ";
    public const string SecondDamage = "Trap.SecondDamage";
    public const string Inside = "Trap.Inside";
}

public sealed class AatroxInfernalChainsMissle : FixedPathFlyingMissleBase
{
    [Title("技能ID")]
    public int WSkillId = 1003;

    [Title("一段效果")]
    public float[] FirstHitBaseBySkillLevel;
    public float FirstHitAttackDamageRatio = 0f;
    public float FirstHitAbilityPowerRatio = 0f;
    public CrowdControlData SlowControl;
    public float SlowDuration = 1.5f;
    public BuffData TrapBuff;

    [Title("二段区域参数")]
    public float TrapBottomWidth = 1.8f;
    public float TrapTopWidth = 3.4f;
    public float TrapHeight = 4f;
    public float TrapCenterForwardOffset = 2f;

    [Title("二段伤害")]
    public float[] SecondHitBaseBySkillLevel;
    public float SecondHitAttackDamageRatio = 0f;
    public float SecondHitAbilityPowerRatio = 0f;

    [Title("额外标签")]
    public string[] FirstHitTags = new[] { AatroxTagConst.W };

    protected override PathData BakePath(in DirectionalMissleInitialData data)
    {
        var result = new PathData();
        if (!UnitManager.Instance.Spawns.TryGetValue(data.OwnerUid, out var owner) || owner == null)
            return result;

        result.PathPoints.Add(owner.LogicPosition);
        result.PathPoints.Add(data.KeyPoint);
        return result;
    }

    protected override void Apply(UnitCore target)
    {
        if (Owner == null || target == null || target.IsDead || target.TeamID == Owner.TeamID || TrapBuff == null)
            return;

        SkillRuntime runtime = null;
        var book = Owner.GetComponent<SkillBook>();
        if (book != null)
            book.TryGetRuntime(WSkillId, out runtime);

        fp first = Evaluate(Owner, runtime, FirstHitBaseBySkillLevel, FirstHitAttackDamageRatio, FirstHitAbilityPowerRatio);
        if (first > fp.zero)
            DamageManager.Instance.CreateAbilityDamageRequest(Owner, target, first, fp.zero, FirstHitTags);

        if (SlowControl != null)
            target.CrowdControlHandler?.AddControl(SlowControl, (fp)SlowDuration, Owner);

        target.BuffHandler?.AddBuff(TrapBuff, Owner);

        if (target.BuffHandler != null && target.BuffHandler.TryGetBuff(TrapBuff.Id, out var info))
        {
            fp3 origin = target.LogicPosition;
            fp3 toward = direction;
            toward.y = fp.zero;
            if (fpmath.lengthsq(toward) <= fp.zero)
                toward = Owner.Direction;
            toward = fpmath.normalize(toward);

            fp3 center = origin + toward * (fp)TrapCenterForwardOffset;
            fp second = Evaluate(Owner, runtime, SecondHitBaseBySkillLevel, SecondHitAttackDamageRatio, SecondHitAbilityPowerRatio);

            info.blackBoard[AatroxTrapKeys.OriginX] = origin.x;
            info.blackBoard[AatroxTrapKeys.OriginZ] = origin.z;
            info.blackBoard[AatroxTrapKeys.DirX] = toward.x;
            info.blackBoard[AatroxTrapKeys.DirZ] = toward.z;
            info.blackBoard[AatroxTrapKeys.Bottom] = (fp)TrapBottomWidth;
            info.blackBoard[AatroxTrapKeys.Top] = (fp)TrapTopWidth;
            info.blackBoard[AatroxTrapKeys.Height] = (fp)TrapHeight;
            info.blackBoard[AatroxTrapKeys.CenterX] = center.x;
            info.blackBoard[AatroxTrapKeys.CenterY] = center.y;
            info.blackBoard[AatroxTrapKeys.CenterZ] = center.z;
            info.blackBoard[AatroxTrapKeys.SecondDamage] = second;
            info.blackBoard[AatroxTrapKeys.Inside] = true;
        }
    }

    private static fp Evaluate(UnitCore caster, SkillRuntime runtime, float[] baseBySkillLevel, float adRatio, float apRatio)
    {
        fp value = fp.zero;

        if (baseBySkillLevel != null && baseBySkillLevel.Length > 0)
        {
            int skillLevel = runtime != null ? Mathf.Clamp(runtime.Level, 1, baseBySkillLevel.Length) : 1;
            value += (fp)baseBySkillLevel[Mathf.Clamp(skillLevel - 1, 0, baseBySkillLevel.Length - 1)];
        }

        if (caster != null)
        {
            value += caster.Stats.Get(UnitStatType.AttackDamage) * (fp)adRatio;
            value += caster.Stats.Get(UnitStatType.AbilityPower) * (fp)apRatio;
        }

        return value;
    }
}
