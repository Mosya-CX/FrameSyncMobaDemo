using Unity.Mathematics.FixedPoint;

public static class SkillTargetResolver
{
    public static bool TryResolve(UnitCore caster, SkillDef skill, in SkillCastRequest request, out SkillResolvedCast resolvedCast)
    {
        resolvedCast = new SkillResolvedCast
        {
            Caster = caster,
        };

        if (caster == null || skill == null)
            return false;

        switch (skill.TargetMode)
        {
            case SkillTargetMode.None:
                resolvedCast.AimDirection = request.AimDirection;
                return true;

            case SkillTargetMode.Unit:
                if (request.TargetUnitUid.HasValue &&
                    UnitManager.Instance.Spawns.TryGetValue(request.TargetUnitUid.Value, out var unitTarget))
                {
                    resolvedCast.TargetUnit = unitTarget;
                    resolvedCast.TargetPoint = unitTarget.LogicPosition;
                    resolvedCast.AimDirection = BuildDirection(caster.LogicPosition, unitTarget.LogicPosition);
                    return true;
                }
                return false;

            case SkillTargetMode.Point:
                if (request.TargetPoint.HasValue)
                {
                    resolvedCast.TargetPoint = request.TargetPoint.Value;
                    resolvedCast.AimDirection = BuildDirection(caster.LogicPosition, request.TargetPoint.Value);
                    return true;
                }
                return false;

            case SkillTargetMode.Direction:
                if (request.AimDirection.HasValue)
                {
                    var dir = request.AimDirection.Value;
                    if (fpmath.lengthsq(dir) > fp.zero)
                    {
                        resolvedCast.AimDirection = fpmath.normalize(dir);
                        return true;
                    }
                }

                if (request.TargetPoint.HasValue)
                {
                    resolvedCast.TargetPoint = request.TargetPoint.Value;
                    resolvedCast.AimDirection = BuildDirection(caster.LogicPosition, request.TargetPoint.Value);
                    return true;
                }
                return false;

            case SkillTargetMode.PointOrUnit:
                if (request.TargetUnitUid.HasValue &&
                    UnitManager.Instance.Spawns.TryGetValue(request.TargetUnitUid.Value, out var target))
                {
                    resolvedCast.TargetUnit = target;
                    resolvedCast.TargetPoint = target.LogicPosition;
                    resolvedCast.AimDirection = BuildDirection(caster.LogicPosition, target.LogicPosition);
                    return true;
                }

                if (request.TargetPoint.HasValue)
                {
                    resolvedCast.TargetPoint = request.TargetPoint.Value;
                    resolvedCast.AimDirection = BuildDirection(caster.LogicPosition, request.TargetPoint.Value);
                    return true;
                }
                return false;
        }

        return false;
    }

    private static fp3 BuildDirection(fp3 from, fp3 to)
    {
        var delta = to - from;
        if (fpmath.lengthsq(delta) <= fp.zero)
            return fp3.zero;

        return fpmath.normalize(delta);
    }
}
