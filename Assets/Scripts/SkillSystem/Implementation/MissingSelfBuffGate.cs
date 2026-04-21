using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "MissingSelfBuffGate", menuName = "SkillSystem/Gates/Missing Self Buff")]
public sealed class MissingSelfBuffGate : SkillGateBase
{
    [LabelText("BuffId")]
    public int BuffId;

    public override SkillGateResult CheckStep(UnitCore caster, SkillDef def, SkillStepDef step, in SkillCastRequest request, in SkillResolvedCast resolvedCast)
    {
        if (caster == null || caster.BuffHandler == null || BuffId == 0)
            return SkillGateResult.Success;

        return caster.BuffHandler.TryGetBuff(BuffId, out _)
            ? SkillGateResult.Fail(SkillGateFailReason.Custom)
            : SkillGateResult.Success;
    }
}
