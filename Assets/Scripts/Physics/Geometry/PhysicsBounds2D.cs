using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public readonly struct PhysicsBounds2D
    {
        public PhysicsBounds2D(fp2 min, fp2 max)
        {
            if (min.x > max.x || min.y > max.y)
            {
                throw new ArgumentException("Physics bounds minimum must not exceed maximum.");
            }

            Min = min;
            Max = max;
        }

        public fp2 Min { get; }

        public fp2 Max { get; }
    }
}
