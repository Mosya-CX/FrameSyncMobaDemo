using System;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Optional deterministic containment boundary carried by a stationary
    /// projectile. It is separate from the projectile hit-query shape: the
    /// latter answers overlap/hit queries, while this shape answers whether a
    /// unit remains inside an authored zone for the whole projectile lifetime.
    /// </summary>
    public readonly struct ProjectileContainmentZone
    {
        public readonly fp ForwardStart;
        public readonly fp ForwardLength;
        public readonly fp NearHalfWidth;
        public readonly fp FarHalfWidth;

        public ProjectileContainmentZone(
            fp forwardStart,
            fp forwardLength,
            fp nearHalfWidth,
            fp farHalfWidth)
        {
            ForwardStart = forwardStart;
            ForwardLength = forwardLength;
            NearHalfWidth = nearHalfWidth;
            FarHalfWidth = farHalfWidth;
        }

        public bool IsValid =>
            ForwardLength > fp.zero &&
            NearHalfWidth >= fp.zero &&
            FarHalfWidth >= fp.zero;

        public bool Contains(
            fp2 origin,
            fp2 forward,
            fp2 point,
            fp pointRadius)
        {
            if (!IsValid ||
                !PhysicsGeometry2D.TryCreateFacing(
                    forward,
                    out fp2 normalized,
                    out fp2 right))
            {
                return false;
            }

            fp2 delta = point - origin;
            fp longitudinal = fpmath.dot(delta, normalized);
            fp lateral = fpmath.abs(fpmath.dot(delta, right));
            fp end = ForwardStart + ForwardLength;
            fp progress = fpmath.clamp(
                (longitudinal - ForwardStart) / ForwardLength,
                fp.zero,
                fp.one);
            fp halfWidth = NearHalfWidth +
                (FarHalfWidth - NearHalfWidth) * progress;
            return longitudinal >= ForwardStart - pointRadius &&
                longitudinal <= end + pointRadius &&
                lateral <= halfWidth + pointRadius;
        }
    }

}
