using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Authoring visualization for a stationary projectile area (e.g. Varus
    /// E "Desecrated Ground"). Draws the configured radius as a circle in
    /// the Scene/prefab view (OnDrawGizmos) and optionally as a runtime
    /// LineRenderer ring in the Game view. Presentation-only; never affects
    /// deterministic Gameplay. The radius here should mirror the projectile
    /// def's HitRadius in the runtime catalog.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileAreaRadiusVisualizer :
        MonoBehaviour
    {
        [Tooltip("Visualized radius; keep in sync with the projectile " +
            "def HitRadius used by the runtime.")]
        [SerializeField, Min(0.01f)]
        private float radius = 3f;

        [Tooltip("Draw a LineRenderer ring in the Game view too.")]
        [SerializeField]
        private bool showRuntimeRing = true;

        [SerializeField]
        private Color ringColor =
            new Color(0.9f, 0.3f, 0.6f, 0.85f);

        [SerializeField]
        private float height = 0.15f;

        private const int Segments = 64;
        private LineRenderer _ring;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.01f, value);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = ringColor;
            Vector3 center =
                transform.position + Vector3.up * height;
            Vector3 previous =
                center + new Vector3(radius, 0f, 0f);
            for (int i = 1;
                 i <= Segments;
                 i++)
            {
                float angle =
                    i * Mathf.PI * 2f / Segments;
                Vector3 next =
                    center +
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void OnEnable()
        {
            if (showRuntimeRing)
            {
                EnsureRing();
            }
        }

        private void LateUpdate()
        {
            if (!showRuntimeRing)
            {
                return;
            }
            EnsureRing();
            WriteRing(radius);
        }

        private void EnsureRing()
        {
            if (_ring != null)
            {
                return;
            }
            var holder =
                new GameObject("AreaRadiusRing");
            holder.transform.SetParent(
                transform,
                false);
            holder.hideFlags =
                HideFlags.HideAndDontSave;
            _ring =
                holder.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = Segments + 1;
            _ring.startWidth = 0.05f;
            _ring.endWidth = 0.05f;
            _ring.startColor = ringColor;
            _ring.endColor = ringColor;
            Shader shader =
                Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _ring.sharedMaterial =
                    new Material(shader)
                    {
                        hideFlags =
                            HideFlags.HideAndDontSave,
                    };
            }
            _ring.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            WriteRing(radius);
        }

        private void WriteRing(float r)
        {
            Vector3 center =
                Vector3.up * height;
            for (int i = 0;
                 i <= Segments;
                 i++)
            {
                float angle =
                    i * Mathf.PI * 2f / Segments;
                _ring.SetPosition(
                    i,
                    center +
                    new Vector3(
                        Mathf.Cos(angle) * r,
                        0f,
                        Mathf.Sin(angle) * r));
            }
        }

        private void OnDisable()
        {
            if (_ring != null)
            {
                Destroy(_ring.gameObject);
                _ring = null;
            }
        }
    }
}
