using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public readonly struct PhysicsTransform2D
    {
        internal PhysicsTransform2D(
            fp2 position,
            fp2 prevPosition,
            fp2 forward,
            fp2 right)
        {
            Position = position;
            PrevPosition = prevPosition;
            Forward = forward;
            Right = right;
        }

        public fp2 Position { get; }

        public fp2 PrevPosition { get; }

        public fp2 Forward { get; }

        public fp2 Right { get; }
    }
}
