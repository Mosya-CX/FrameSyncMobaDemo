using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Battle camera (presentation only, never touches deterministic state).
    /// Side-angle top-down view; Y toggles follow-lock on the local hero;
    /// when unlocked the camera pans with the mouse at screen edges. Scroll
    /// altitude zoom is intentionally not implemented yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Optional direct follow target (debug/standalone scenes). " +
            "When assigned, this target is used instead of resolving the " +
            "local controlled unit through GameSessionContext.")]
        [SerializeField] private Transform debugTarget;

        [Header("Camera tuning")]
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private float panSpeed = 40f;
        [SerializeField] private float edgeSize = 24f;
        [SerializeField] private float sideAngle = 40f;
        [SerializeField] private Vector3 followOffset =
            new Vector3(0f, 0f, -10f);

        [Header("Input")]
        [SerializeField] private KeyCode lockKey =
            KeyCode.Y;
        [SerializeField] private bool startLocked = true;

        [Header("Bounds (optional)")]
        [Tooltip("Clamp the camera world position to this XZ rectangle " +
            "(x = min/max X, y = min/max Z). Off by default so the battle " +
            "camera behaviour is unchanged.")]
        [SerializeField] private bool clampToBounds;
        [SerializeField] private Vector2 boundsMin;
        [SerializeField] private Vector2 boundsMax;

        [Header("Map-fit (optional)")]
        [Tooltip("Clamp the camera so its ground view stays inside the map " +
            "rectangle (mapMin/mapMax). This accounts for the side-angle " +
            "perspective, so the visible map borders stay balanced on screen. " +
            "Takes precedence over the plain bounds clamp.")]
        [SerializeField] private bool fitToMapBounds;
        [SerializeField] private Vector2 mapMin =
            new Vector2(-20f, -20f);
        [SerializeField] private Vector2 mapMax =
            new Vector2(20f, 20f);
        [Tooltip("Inset applied to the map rectangle before clamping the " +
            "view center. Larger values keep the visible map border closer " +
            "to the screen edge at the cost of a tighter camera range.")]
        [SerializeField] private Vector2 mapInset = Vector2.zero;

        private Transform target;
        private bool targetResolved;
        private bool followLocked;
        private Camera mainCamera;
        private Vector2 currentClampMin;
        private Vector2 currentClampMax;

        private void Awake()
        {
            followLocked = startLocked;
            mainCamera = GetComponent<Camera>();
            transform.rotation =
                Quaternion.Euler(sideAngle, 0f, 0f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(lockKey))
                followLocked = !followLocked;

            if (followLocked)
                FollowLocalHero();
            else
                EdgePan();
            ClampToBounds();
        }

        /// <summary>Enable an XZ camera clamp (debug scenes).</summary>
        public void SetBounds(
            Vector2 min,
            Vector2 max)
        {
            clampToBounds = true;
            boundsMin = min;
            boundsMax = max;
        }

        public void ClearBounds()
        {
            clampToBounds = false;
        }

        public bool ClampEnabled => clampToBounds;
        public Vector2 BoundsMin => boundsMin;
        public Vector2 BoundsMax => boundsMax;
        public float EdgeSize => edgeSize;
        public bool MapFitEnabled => fitToMapBounds;
        public Vector2 MapMin => mapMin;
        public Vector2 MapMax => mapMax;
        public Vector2 MapInset => mapInset;
        public Vector2 CurrentClampMin => currentClampMin;
        public Vector2 CurrentClampMax => currentClampMax;

        /// <summary>
        /// Point the camera at an explicit transform (debug/standalone
        /// scenes). Clears any previously resolved target so the new target
        /// is used on the next update.
        /// </summary>
        public void SetDebugTarget(Transform target)
        {
            debugTarget = target;
            this.target = target;
            targetResolved = target != null;
        }

        /// <summary>
        /// Switch to a free side-angle camera: never locks onto a hero and
        /// only pans by screen-edge mouse movement (no WASD, no follow).
        /// Used by the minion/tower long-run test scene where the camera is
        /// present for inspection but there is no local hero.
        /// </summary>
        public void ConfigureFreeCamera(
            float panSpeedValue = 40f,
            float edgeSizeValue = 24f,
            KeyCode lockKeyValue = KeyCode.Y)
        {
            startLocked = false;
            followLocked = false;
            panSpeed = panSpeedValue;
            edgeSize = edgeSizeValue;
            lockKey = lockKeyValue;
            transform.rotation =
                Quaternion.Euler(sideAngle, 0f, 0f);
        }

        /// <summary>
        /// Replicates the CameraDebugScene tuning exactly (side angle 47,
        /// follow offset (0,10,-10), WASD rig follow with Y toggle). The
        /// caller supplies the WASD rig transform; no hero is involved.
        /// </summary>
        public void ConfigureDebugSceneCamera(
            Transform followTarget,
            float followSpeedValue = 8f,
            float panSpeedValue = 40f,
            float edgeSizeValue = 24f,
            float sideAngleValue = 47f,
            Vector3 followOffsetValue =
                default,
            KeyCode lockKeyValue =
                KeyCode.Y,
            bool startLockedValue = true)
        {
            followSpeed = followSpeedValue;
            panSpeed = panSpeedValue;
            edgeSize = edgeSizeValue;
            sideAngle = sideAngleValue;
            followOffset = followOffsetValue.sqrMagnitude > 0f
                ? followOffsetValue
                : new Vector3(0f, 10f, -10f);
            lockKey = lockKeyValue;
            startLocked = startLockedValue;
            followLocked = startLockedValue;
            SetDebugTarget(followTarget);
            transform.rotation =
                Quaternion.Euler(sideAngle, 0f, 0f);
        }

        private void FollowLocalHero()
        {
            if (!targetResolved)
                ResolveTarget();
            if (target == null)
                return;
            Vector3 desired = target.position +
                followOffset;
            // The camera only moves on the XZ plane; height stays fixed.
            desired.y = transform.position.y;
            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                followSpeed * Time.deltaTime);
        }

        private void EdgePan()
        {
            Vector2 mouse = Input.mousePosition;
            Vector2 screenDirection = Vector2.zero;
            if (mouse.x < edgeSize)
                screenDirection.x = -1f;
            else if (mouse.x >
                     Screen.width - edgeSize)
                screenDirection.x = 1f;
            if (mouse.y >
                Screen.height - edgeSize)
                screenDirection.y = 1f;
            else if (mouse.y < edgeSize)
                screenDirection.y = -1f;

            if (screenDirection.sqrMagnitude <= 0f)
            {
                return;
            }

            // Screen right maps to the camera right (world X here), screen up
            // maps to the camera up projected onto the XZ plane. Movement is
            // applied to X/Z only: the camera never changes its height.
            Vector3 right = transform.right;
            Vector3 upOnXz = Vector3
                .ProjectOnPlane(
                    transform.up,
                    Vector3.up)
                .normalized;
            Vector3 move =
                (right * screenDirection.x +
                 upOnXz * screenDirection.y) *
                (panSpeed * Time.deltaTime);
            Vector3 position = transform.position;
            position.x += move.x;
            position.z += move.z;
            transform.position = position;
        }

        private void ClampToBounds()
        {
            if (fitToMapBounds)
            {
                FitToMapBounds();
                return;
            }
            if (!clampToBounds)
            {
                return;
            }
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(
                position.x,
                boundsMin.x,
                boundsMax.x);
            position.z = Mathf.Clamp(
                position.z,
                boundsMin.y,
                boundsMax.y);
            transform.position = position;
        }

        /// <summary>
        /// Clamp the camera so the ground point under the screen center stays
        /// inside the map rectangle (minus an optional inset). Unlike a full
        /// view-frustum clamp this leaves the camera free to follow the
        /// target anywhere inside the map, which suits the side-angle
        /// top-down view where the ground view is wider than the map and
        /// asymmetric.
        /// </summary>
        private void FitToMapBounds()
        {
            if (mainCamera == null)
            {
                mainCamera = GetComponent<Camera>();
            }
            if (mainCamera == null)
            {
                return;
            }

            Vector3 position = transform.position;
            float height = position.y;
            if (height <= 0.001f)
            {
                return;
            }

            Ray centerRay =
                mainCamera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f));
            if (centerRay.direction.y >=
                -0.0001f)
            {
                return;
            }
            float distanceToGround =
                -height / centerRay.direction.y;
            Vector3 centerGround =
                position +
                centerRay.direction *
                distanceToGround;
            Vector3 centerOffset =
                centerGround - position;

            float minX =
                mapMin.x + mapInset.x;
            float maxX =
                mapMax.x - mapInset.x;
            float minZ =
                mapMin.y + mapInset.y;
            float maxZ =
                mapMax.y - mapInset.y;

            currentClampMin =
                new Vector2(
                    minX - centerOffset.x,
                    minZ - centerOffset.z);
            currentClampMax =
                new Vector2(
                    maxX - centerOffset.x,
                    maxZ - centerOffset.z);
            position.x = Mathf.Clamp(
                position.x,
                currentClampMin.x,
                currentClampMax.x);
            position.z = Mathf.Clamp(
                position.z,
                currentClampMin.y,
                currentClampMax.y);
            transform.position = position;
        }

        /// <summary>
        /// Editor-only visualization of the camera XZ clamp rectangle, useful
        /// for tuning the pan/follow limits in the debug scene.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!clampToBounds)
            {
                return;
            }
            Vector3 center =
                new Vector3(
                    (boundsMin.x + boundsMax.x) * 0.5f,
                    transform.position.y,
                    (boundsMin.y + boundsMax.y) * 0.5f);
            Vector3 size =
                new Vector3(
                    boundsMax.x - boundsMin.x,
                    0.2f,
                    boundsMax.y - boundsMin.y);
            Gizmos.color =
                new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawCube(center, size);
            Gizmos.color =
                new Color(0f, 1f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }

        private void ResolveTarget()
        {
            targetResolved = true;
            if (debugTarget != null)
            {
                target = debugTarget;
                return;
            }
            GameBootstrap bootstrap =
                GameSessionContext.Bootstrap;
            UnitType unit =
                bootstrap != null &&
                bootstrap.Runtime != null
                    ? bootstrap.Runtime
                        .GetLocalControlledUnit()
                    : null;
            if (unit == null)
            {
                target = null;
                targetResolved = false;
                return;
            }
            target = unit.transform;
        }
    }
}
