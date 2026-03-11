using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public static class SpatialQueryUtility
{
    public static bool UnitIntersectsUnit(UnitCore a, UnitCore b)
    {
        fp radius = a.unitSizeRadius + b.unitSizeRadius;
        return fpmath.lengthsq(a.LogicPosition - b.LogicPosition) <= radius * radius;
    }

    public static bool MissleIntersectsUnit(BaseMissle missle, UnitCore unit)
    {
        fp3 misslePos = missle.LogicPosition;
        fp2 rot = missle.LogicRotation;

        fp sinHalf = rot.x;
        fp cosHalf = rot.y;

        fp2 forward = new fp2(2 * sinHalf * cosHalf, cosHalf * cosHalf - sinHalf * sinHalf);
        fp2 right = new fp2(-forward.y, forward.x);

        fp halfLength = missle.LogicSize.x / 2;
        fp halfWidth = missle.LogicSize.z / 2;

        fp2 delta = new fp2(unit.LogicPosition.x - misslePos.x, unit.LogicPosition.z - misslePos.z);
        fp f = fpmath.dot(delta, forward);
        fp r = fpmath.dot(delta, right);

        fp df = fpmath.max(fpmath.abs(f) - halfLength, 0);
        fp dr = fpmath.max(fpmath.abs(r) - halfWidth, 0);
        fp dist = fpmath.sqrt(df * df + dr * dr);

        return dist <= unit.unitSizeRadius;
    }

    public static IReadOnlyList<UnitCore> SearchRectRangeUnits(IEnumerable<UnitCore> units, fp3 origin, fp3 toward, fp l, fp w, SimulationFilter filter)
    {
        var result = ListPool<UnitCore>.Get();

        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);

        if (fpmath.lengthsq(forward) < fp.precision)
            return result;

        forward = fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);
        fp halfW = w / 2;

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead || !CheckUnit(unit, filter))
                continue;

            fp2 posXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp radius = unit.unitSizeRadius;
            fp halfR = radius / 2;
            int countInside = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dy = j * halfR;
                    fp2 samplePos = posXZ + dx * forward + dy * right;
                    fp2 v = samplePos - originXZ;
                    fp f = fpmath.dot(v, forward);
                    fp r = fpmath.dot(v, right);

                    if (f >= 0 && f <= l && fpmath.abs(r) <= halfW)
                        countInside++;
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    public static IReadOnlyList<UnitCore> SearchLadderRangeUnits(IEnumerable<UnitCore> units, fp3 origin, fp3 toward, fp bottomLength, fp topLength, fp height, SimulationFilter filter)
    {
        var result = ListPool<UnitCore>.Get();

        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);

        if (fpmath.lengthsq(forward) < fp.precision)
            return result;

        forward = fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);
        fp halfBottom = bottomLength / 2;
        fp halfTop = topLength / 2;

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead || !CheckUnit(unit, filter))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dy = j * halfR;
                    fp2 samplePos = unitPosXZ + dx * right + dy * forward;
                    fp2 v = samplePos - originXZ;
                    fp f = fpmath.dot(v, forward);
                    fp r = fpmath.dot(v, right);

                    if (f >= 0 && f <= height)
                    {
                        fp maxR = halfBottom + (halfTop - halfBottom) * (f / height);
                        if (fpmath.abs(r) <= maxR)
                            countInside++;
                    }
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    public static IReadOnlyList<UnitCore> SearchRoundRangeUnits(IEnumerable<UnitCore> units, fp3 origin, fp radius, SimulationFilter filter)
    {
        var result = ListPool<UnitCore>.Get();
        fp2 originXZ = new fp2(origin.x, origin.z);

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead || !CheckUnit(unit, filter))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            fp2 dirX = new fp2(1, 0);
            fp2 dirZ = new fp2(0, 1);

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dz = j * halfR;
                    fp2 samplePos = unitPosXZ + dx * dirX + dz * dirZ;
                    fp2 v = samplePos - originXZ;
                    if (fpmath.length(v) <= radius)
                        countInside++;
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    public static IReadOnlyList<UnitCore> SearchFanShapedRangeUnits(IEnumerable<UnitCore> units, fp3 origin, fp3 toward, fp radius, fp angle, SimulationFilter filter)
    {
        var result = ListPool<UnitCore>.Get();

        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);

        if (fpmath.lengthsq(forward) < fp.precision)
            return result;

        forward = fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);
        fp cosHalfAngle = fpmath.cos(fpmath.clamp(angle, 0, 360) / 2);

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead || !CheckUnit(unit, filter))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dy = j * halfR;
                    fp2 samplePos = unitPosXZ + dx * right + dy * forward;
                    fp2 v = samplePos - originXZ;
                    fp dist = fpmath.length(v);

                    if (dist <= radius)
                    {
                        if (dist > 0)
                        {
                            fp dot = fpmath.dot(v, forward);
                            if (dot >= dist * cosHalfAngle)
                                countInside++;
                        }
                        else
                        {
                            countInside++;
                        }
                    }
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    private static bool CheckUnit(UnitCore unit, SimulationFilter filter)
    {
        if ((filter.TeamMask & unit.SimulationTeamMask) == 0)
            return false;

        if ((filter.EntityMask & unit.SimulationEntityType) == 0)
            return false;

        return true;
    }
}