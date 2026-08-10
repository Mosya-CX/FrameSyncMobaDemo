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
        [Tooltip("Show the deterministic collision shape in the Scene view "
            + "and in Prefab Mode (editor only).")]
        [SerializeField] private bool showGizmo = true;

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawShapeGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawShapeGizmo(true);
        }

        /// <summary>
        /// Offline visualization of the deterministic collision shape in the
        /// Scene view / Prefab Mode. Maps the logic 2D plane onto the Unity
        /// XZ ground plane and respects the transform's rotation, so the
        /// preview matches the baked logical shape without entering Play.
        /// </summary>
        private void DrawShapeGizmo(bool selected)
        {
            if (!showGizmo)
            {
                return;
            }
            Gizmos.color = selected
                ? new Color(1f, 0.45f, 0f, 1f)
                : new Color(1f, 0.92f, 0.2f, 0.75f);

            Vector3 center =
                transform.position +
                transform.rotation *
                new Vector3(
                    localOffset.x,
                    0f,
                    localOffset.y);

            switch (shapeKind)
            {
                case PhysicsShapeKind.Point:
                    Gizmos.DrawWireSphere(
                        center,
                        0.1f);
                    break;

                case PhysicsShapeKind.Circle:
                    Gizmos.DrawWireSphere(
                        center,
                        radius);
                    break;

                case PhysicsShapeKind.Segment:
                    Vector3 forward =
                        transform.forward;
                    Vector3 half =
                        forward *
                        (length * 0.5f);
                    Vector3 start =
                        center - half;
                    Vector3 end =
                        center + half;
                    Gizmos.DrawLine(
                        start,
                        end);
                    if (width > 0f)
                    {
                        Gizmos.DrawWireSphere(
                            start,
                            width * 0.5f);
                        Gizmos.DrawWireSphere(
                            end,
                            width * 0.5f);
                    }
                    break;

                case PhysicsShapeKind.Rect:
                    Vector3 right =
                        transform.right;
                    Vector3 forward2 =
                        transform.forward;
                    Vector3 extentX =
                        right * halfExtents.x;
                    Vector3 extentZ =
                        forward2 * halfExtents.y;
                    Vector3 c00 =
                        center - extentX - extentZ;
                    Vector3 c10 =
                        center + extentX - extentZ;
                    Vector3 c11 =
                        center + extentX + extentZ;
                    Vector3 c01 =
                        center - extentX + extentZ;
                    Gizmos.DrawLine(c00, c10);
                    Gizmos.DrawLine(c10, c11);
                    Gizmos.DrawLine(c11, c01);
                    Gizmos.DrawLine(c01, c00);
                    break;
            }
        }
#endif
    }
}
