using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public readonly struct MoveIntent
    {
        public readonly fp2 Direction;
        public readonly bool HasInput;
        public static readonly MoveIntent None = default;

        public MoveIntent(fp2 direction)
        {
            Direction = direction;
            HasInput = true;
        }

        public static MoveIntent FromDirection(fp2 direction)
        {
            if (fpmath.dot(direction, direction) <= fp.zero)
            {
                return None;
            }
            return new MoveIntent(fpmath.normalize(direction));
        }
    }
}
