using Unity.Mathematics.FixedPoint;

public static class SkillApproachResolver
{
    public static bool ResolveRange(UnitCore caster, SkillDef skill, ref SkillResolvedCast resolvedCast)
    {
        if (caster == null || skill == null || skill.CastRange <= 0f)
            return true;

        fp range = (fp)skill.CastRange;
        fp3 origin = caster.LogicPosition;

        fp3 targetPoint = resolvedCast.TargetPoint ??
                          (resolvedCast.TargetUnit != null ? resolvedCast.TargetUnit.LogicPosition : origin);

        fp3 delta = targetPoint - origin;
        delta.y = fp.zero;

        fp distanceSq = fpmath.lengthsq(delta);
        fp rangeSq = range * range;

        if (distanceSq <= rangeSq)
            return true;

        switch (skill.RangePolicy)
        {
            case SkillRangePolicy.MustInRange:
                return false;

            case SkillRangePolicy.ClampToRange:
                if (distanceSq > fp.zero)
                {
                    var dir = fpmath.normalize(delta);
                    resolvedCast.TargetPoint = origin + dir * range;
                    resolvedCast.AimDirection = dir;
                }
                return true;

            case SkillRangePolicy.AutoApproach:
                if (distanceSq > fp.zero)
                {
                    var dir = fpmath.normalize(delta);
                    resolvedCast.NeedApproach = true;
                    resolvedCast.ApproachPoint = targetPoint - dir * range;
                    resolvedCast.AimDirection = dir;
                }
                return true;
        }

        return true;
    }
}
