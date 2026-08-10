using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Scene-authored test obstacle. The wall is a normal draggable
    /// GameObject (visible in both Scene and Game views); its Transform
    /// position/rotation and this footprint size are baked into the path
    /// grid when the driver rebakes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTestObstacle : MonoBehaviour
    {
        [Tooltip("Obstacle footprint in logic units (X = local right, Y = local forward).")]
        public Vector2 Size =
            new Vector2(2f, 10f);

        [Min(0.1f)]
        public float Height = 5f;

        private void OnDrawGizmos()
        {
            Vector3 center =
                transform.position;
            Quaternion rotation =
                transform.rotation;
            Gizmos.color =
                new Color(1f, 0.25f, 0.1f, 0.55f);
            Gizmos.matrix =
                Matrix4x4.TRS(
                    center,
                    rotation,
                    new Vector3(
                        Size.x,
                        Height,
                        Size.y));
            Gizmos.DrawCube(
                Vector3.zero,
                Vector3.one);
            Gizmos.DrawWireCube(
                Vector3.zero,
                Vector3.one);
            Gizmos.matrix =
                Matrix4x4.identity;
        }
    }
}
