using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Scene marker: where the hero test driver spawns the player unit.
    /// Drag this GameObject in the scene to configure the spawn position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint :
        MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        private float gizmoRadius = 0.7f;

        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Gizmos.color =
                new Color(0.25f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(
                position,
                gizmoRadius);
            Gizmos.DrawLine(
                position + Vector3.left *
                    gizmoRadius,
                position + Vector3.right *
                    gizmoRadius);
            Gizmos.DrawLine(
                position + Vector3.forward *
                    gizmoRadius,
                position + Vector3.back *
                    gizmoRadius);
            // Facing arrow.
            Gizmos.DrawLine(
                position,
                position +
                transform.forward * (gizmoRadius * 2f));
        }
    }
}
