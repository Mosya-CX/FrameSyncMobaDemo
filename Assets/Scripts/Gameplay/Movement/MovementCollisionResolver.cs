using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public interface IMovementCollisionResolver
    {
        fp2 ClampPosition(
            fp2 desiredPosition,
            fp2 currentPosition,
            fp unitRadius,
            RadiusClass radiusClass,
            UnitUid selfUid);
    }
}
