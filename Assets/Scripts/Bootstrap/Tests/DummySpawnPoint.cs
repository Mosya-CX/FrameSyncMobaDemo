using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Scene marker: a sandbag spawn point for the hero test scene.
    /// The HeroTestDriver detects all of these and spawns a punching-bag
    /// dummy at each position (teams alternate by marker index so the
    /// Corruption Vines spread can chain between them).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DummySpawnPoint :
        MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        private float gizmoRadius = 0.45f;

        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Gizmos.color =
                new Color(1f, 0.65f, 0.15f, 0.9f);
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
        }
    }
}
