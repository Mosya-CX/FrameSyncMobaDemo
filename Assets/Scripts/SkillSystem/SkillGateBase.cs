using UnityEngine;

/// <summary>
/// 特殊施法条件 / Step 条件基类。
/// 基础条件（冷却、蓝耗、射程、控制阻断）不走这里。
/// </summary>
public abstract class SkillGateBase : ScriptableObject
{
    public virtual SkillGateResult CheckPreview(UnitCore caster, SkillDef def, in SkillCastRequest request)
    {
        return SkillGateResult.Success;
    }

    public virtual SkillGateResult CheckCommit(UnitCore caster, SkillDef def, in SkillCastRequest request, in SkillResolvedCast resolvedCast)
    {
        return SkillGateResult.Success;
    }

    public virtual SkillGateResult CheckStep(UnitCore caster, SkillDef def, SkillStepDef step, in SkillCastRequest request, in SkillResolvedCast resolvedCast)
    {
        return SkillGateResult.Success;
    }

    public virtual SkillGateResult CheckRunning(SkillExecution execution)
    {
        return SkillGateResult.Success;
    }
}

public enum SkillGateFailReason : byte
{
    None = 0,
    Cooldown = 1,
    Resource = 2,
    OutOfRange = 3,
    ControlBlocked = 4,
    PreviewForbidden = 5,
    CommitForbidden = 6,
    InvalidTarget = 7,
    MissingContext = 8,
    Custom = 100,
}

public readonly struct SkillGateResult
{
    public readonly bool Passed;
    public readonly SkillGateFailReason Reason;

    public SkillGateResult(bool passed, SkillGateFailReason reason)
    {
        Passed = passed;
        Reason = reason;
    }

    public static SkillGateResult Success => new SkillGateResult(true, SkillGateFailReason.None);

    public static SkillGateResult Fail(SkillGateFailReason reason)
    {
        return new SkillGateResult(false, reason);
    }
}
