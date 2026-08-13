using System;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Prefab-owned authoring for a stationary projectile containment zone.
    /// The projectile catalog bakes this component into ProjectileDef, so the
    /// Scene-view preview and deterministic Gameplay consume one data source.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PhysicsEntity2D))]
    public sealed class ProjectileContainmentZoneAuthoring : MonoBehaviour
    {
        [SerializeField] private float forwardStart = -1.5f;
        [SerializeField, Min(0.001f)] private float forwardLength = 6f;
        [SerializeField, Min(0f)] private float nearHalfWidth = 1f;
        [SerializeField, Min(0f)] private float farHalfWidth = 3f;
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor =
            new Color(0.1f, 0.85f, 1f, 1f);

        public ProjectileContainmentZone BakeOrThrow()
        {
            var zone = new ProjectileContainmentZone(
                (fp)forwardStart,
                (fp)forwardLength,
                (fp)nearHalfWidth,
                (fp)farHalfWidth);
            if (!zone.IsValid)
                throw new InvalidOperationException(
                    $"Projectile containment zone on {name} is invalid.");
            return zone;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (forwardLength < 0.001f)
                forwardLength = 0.001f;
            if (nearHalfWidth < 0f)
                nearHalfWidth = 0f;
            if (farHalfWidth < 0f)
                farHalfWidth = 0f;
        }

        private void OnDrawGizmos()
        {
            if (showGizmo)
                DrawGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (showGizmo)
                DrawGizmo(true);
        }

        private void DrawGizmo(bool selected)
        {
            Color color = gizmoColor;
            if (!selected)
                color.a *= 0.65f;
            Gizmos.color = color;

            Vector3 origin = transform.position + Vector3.up * 0.05f;
            Vector3 forward = Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(
                Vector3.up,
                forward).normalized;
            Vector3 nearCenter = origin + forward * forwardStart;
            Vector3 farCenter = origin +
                forward * (forwardStart + forwardLength);
            Vector3 nearLeft = nearCenter - right * nearHalfWidth;
            Vector3 nearRight = nearCenter + right * nearHalfWidth;
            Vector3 farLeft = farCenter - right * farHalfWidth;
            Vector3 farRight = farCenter + right * farHalfWidth;
            Gizmos.DrawLine(nearLeft, nearRight);
            Gizmos.DrawLine(nearRight, farRight);
            Gizmos.DrawLine(farRight, farLeft);
            Gizmos.DrawLine(farLeft, nearLeft);
        }
#endif
    }
}
