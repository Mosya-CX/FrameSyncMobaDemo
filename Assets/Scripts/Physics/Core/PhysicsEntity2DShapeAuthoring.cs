using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PhysicsEntity2D))]
    public sealed class PhysicsEntity2DShapeAuthoring :
        MonoBehaviour
    {
        [SerializeField] private PhysicsShapeKind shapeKind =
            PhysicsShapeKind.Point;
        [SerializeField] private Vector2 localOffset;
        [SerializeField, Min(0f)] private float radius;
        [SerializeField, Min(0f)] private float length;
        [SerializeField, Min(0f)] private float width;
        [SerializeField] private Vector2 halfExtents;
        [SerializeField] private bool sweepFromPrevious;

        private void Awake()
        {
            GetComponent<PhysicsEntity2D>()
                .SetLogicShape(BakeOrThrow());
        }

        public PhysicsShape2D BakeOrThrow()
        {
            fp2 offset = new fp2(
                (fp)localOffset.x,
                (fp)localOffset.y);
            switch (shapeKind)
            {
                case PhysicsShapeKind.Point:
                    return PhysicsShape2D.CreatePoint(
                        offset,
                        sweepFromPrevious);
                case PhysicsShapeKind.Circle:
                    return PhysicsShape2D.CreateCircle(
                        offset,
                        (fp)radius,
                        sweepFromPrevious);
                case PhysicsShapeKind.Segment:
                    return PhysicsShape2D.CreateSegment(
                        offset,
                        (fp)length,
                        (fp)width,
                        sweepFromPrevious);
                case PhysicsShapeKind.Rect:
                    return PhysicsShape2D.CreateRect(
                        offset,
                        new fp2(
                            (fp)halfExtents.x,
                            (fp)halfExtents.y),
                        sweepFromPrevious);
                default:
                    throw new System.InvalidOperationException(
                        $"Unsupported PhysicsShapeKind {shapeKind}.");
            }
        }

        private void OnValidate()
        {
            if (radius < 0f) radius = 0f;
            if (length < 0f) length = 0f;
            if (width < 0f) width = 0f;
            if (halfExtents.x < 0f)
                halfExtents.x = 0f;
            if (halfExtents.y < 0f)
                halfExtents.y = 0f;
        }
    }
}
