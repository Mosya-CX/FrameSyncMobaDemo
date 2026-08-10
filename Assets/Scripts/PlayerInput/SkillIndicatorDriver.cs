using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Manages ability aim indicator GameObjects.
    ///
    /// Supports the MOBA skill-indicator rules:
    /// - Q/E/R show a translucent cast-range circle around the caster;
    /// - Q/R (Direction) show a rounded bar that points at the cursor and can
    ///   grow as the ability charges (UpdateDirectionLength);
    /// - E (Point) shows a cursor-following ground circle whose radius is
    ///   configurable (UpdateGroundRadius);
    /// - W has no indicator.
    ///
    /// Presentation only; never affects Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorDriver : MonoBehaviour
    {
        [Header("Indicator prefabs (assigned in Inspector or code)")]
        [SerializeField] private GameObject directionIndicatorPrefab;
        [SerializeField] private GameObject rangeCirclePrefab;
        [SerializeField] private GameObject groundTargetPrefab;

        [Header("Defaults")]
        [Tooltip("Ground circle radius used when the ability config does not "
            + "provide one.")]
        [SerializeField] private float groundTargetDefaultRadius = 0.5f;

        private GameObject _directionInstance;
        private GameObject _rangeCircleInstance;
        private GameObject _groundTargetInstance;

        private Transform _directionBody;
        private Transform _directionHead;
        private Transform _groundDisc;
        private Transform _rangeDisc;

        private AimKind _activeKind;
        private fp _activeRange;
        private bool _visible;
        private bool _showRangeCircle;
        private fp _groundRadius;
        private fp _directionLength;

        public bool IsVisible => _visible;
        public AimKind ActiveKind => _activeKind;

        /// <summary>
        /// Assign prefabs before the driver is enabled. Safe to call before
        /// Awake; existing instances are recreated when the prefabs change.
        /// </summary>
        public void Configure(
            GameObject directionPrefab,
            GameObject rangeCirclePrefab,
            GameObject groundTargetPrefab)
        {
            directionIndicatorPrefab =
                directionPrefab;
            this.rangeCirclePrefab =
                rangeCirclePrefab;
            this.groundTargetPrefab =
                groundTargetPrefab;
            EnsureInstances();
        }

        private void Awake()
        {
            EnsureInstances();
        }

        private void EnsureInstances()
        {
            if (_directionInstance == null &&
                directionIndicatorPrefab != null)
            {
                _directionInstance =
                    Instantiate(
                        directionIndicatorPrefab,
                        transform);
                _directionInstance
                    .SetActive(false);
                _directionBody =
                    FindChild(
                        _directionInstance.transform,
                        "Body");
                _directionHead =
                    FindChild(
                        _directionInstance.transform,
                        "Head");
            }
            if (_rangeCircleInstance == null &&
                rangeCirclePrefab != null)
            {
                _rangeCircleInstance =
                    Instantiate(
                        rangeCirclePrefab,
                        transform);
                _rangeCircleInstance
                    .SetActive(false);
                _rangeDisc =
                    FindChild(
                        _rangeCircleInstance.transform,
                        "Disc");
            }
            if (_groundTargetInstance == null &&
                groundTargetPrefab != null)
            {
                _groundTargetInstance =
                    Instantiate(
                        groundTargetPrefab,
                        transform);
                _groundTargetInstance
                    .SetActive(false);
                _groundDisc =
                    FindChild(
                        _groundTargetInstance.transform,
                        "Dot");
            }
        }

        /// <summary>
        /// Show the indicator for the given AimKind. A translucent cast-range
        /// circle is shown for aiming abilities (Q/E/R) unless suppressed.
        /// </summary>
        public void Show(
            AimKind kind,
            fp castRange,
            fp2 casterPosition,
            fp2 casterForward,
            bool showRangeCircle = true,
            fp targetRadius = default)
        {
            EnsureInstances();
            HideAllInstances();

            _activeKind = kind;
            _activeRange = castRange;
            _showRangeCircle =
                showRangeCircle &&
                kind != AimKind.None;
            _groundRadius =
                targetRadius > fp.zero
                    ? targetRadius
                    : (fp)groundTargetDefaultRadius;
            _directionLength =
                castRange > fp.zero
                    ? castRange
                    : (fp)1;
            _visible = true;
            UnityEngine.Debug.Log(
                $"[Indicator] Show kind={kind} " +
                $"range={castRange} groundRadius={_groundRadius} " +
                $"showRangeCircle={_showRangeCircle} " +
                $"caster=({casterPosition.x},{casterPosition.y})");

            if (_showRangeCircle &&
                _rangeCircleInstance != null)
            {
                _rangeCircleInstance
                    .SetActive(true);
                SetCircleScale(
                    _rangeDisc,
                    castRange);
                SetWorldXZPosition(
                    _rangeCircleInstance
                        .transform,
                    casterPosition,
                    DirectionBarHeight);
                UnityEngine.Debug.Log(
                    $"[Indicator] rangeDisc worldRot=" +
                    $"{(_rangeDisc != null ? _rangeDisc.rotation.eulerAngles.ToString() : "NULL")} " +
                    $"scale={(_rangeDisc != null ? _rangeDisc.lossyScale.ToString() : "NULL")}");
            }

            GameObject target =
                GetIndicatorForAimKind(kind);
            if (target != null)
            {
                target.SetActive(true);
                PositionIndicator(
                    target,
                    kind,
                    casterPosition,
                    casterForward);
            }
        }

        /// <summary>
        /// Update the indicator position/direction based on cursor world
        /// position. The range circle stays glued to the caster.
        /// </summary>
        public void UpdateCursor(
            fp2 cursorWorldPos,
            fp2 casterPosition,
            fp2 casterForward)
        {
            if (!_visible)
            {
                return;
            }
            if (_showRangeCircle &&
                _rangeCircleInstance != null)
            {
                SetWorldXZPosition(
                    _rangeCircleInstance
                        .transform,
                    casterPosition,
                    DirectionBarHeight);
            }

            GameObject target =
                GetIndicatorForAimKind(
                    _activeKind);
            if (target == null)
            {
                return;
            }

            switch (_activeKind)
            {
                case AimKind.Direction:
                    UpdateDirectionIndicator(
                        target,
                        cursorWorldPos,
                        casterPosition);
                    break;
                case AimKind.Point:
                case AimKind.Unit:
                    UpdateGroundTargetIndicator(
                        target,
                        cursorWorldPos,
                        casterPosition);
                    break;
                case AimKind.Self:
                    UpdateRangeCircleIndicator(
                        target,
                        casterPosition);
                    break;
            }
        }

        /// <summary>
        /// Set the length of the Direction bar (Q charges: grows from the
        /// minimum to the maximum range). Clamped to the cast range.
        /// </summary>
        public void UpdateDirectionLength(
            fp length)
        {
            if (!_visible ||
                _activeKind != AimKind.Direction)
            {
                return;
            }
            _directionLength =
                length > fp.zero
                    ? length
                    : (fp)0.1m;
            if (_directionInstance == null)
            {
                return;
            }
            SetDirectionBarLength(
                _directionBody,
                _directionHead,
                _directionLength);
        }

        /// <summary>
        /// Set the radius of the cursor-following ground circle (E).
        /// </summary>
        public void UpdateGroundRadius(
            fp radius)
        {
            if (!_visible ||
                (_activeKind != AimKind.Point &&
                 _activeKind != AimKind.Unit))
            {
                return;
            }
            _groundRadius =
                radius > fp.zero
                    ? radius
                    : (fp)groundTargetDefaultRadius;
            if (_groundDisc != null)
            {
                SetCircleScale(
                    _groundDisc,
                    _groundRadius);
            }
        }

        /// <summary>
        /// Hide all indicators and reset state.
        /// </summary>
        public void Hide()
        {
            HideAllInstances();
            _visible = false;
            _activeKind = AimKind.None;
            _showRangeCircle = false;
            UnityEngine.Debug.Log(
                "[Indicator] Hide");
        }

        /// <summary>
        /// Force-clear all indicator instances without state tracking.
        /// Used during cleanup/destroy.
        /// </summary>
        public void ForceClear()
        {
            if (_directionInstance != null)
            {
                Destroy(_directionInstance);
            }
            if (_rangeCircleInstance != null)
            {
                Destroy(_rangeCircleInstance);
            }
            if (_groundTargetInstance != null)
            {
                Destroy(_groundTargetInstance);
            }
            _directionInstance = null;
            _rangeCircleInstance = null;
            _groundTargetInstance = null;
            _directionBody = null;
            _directionHead = null;
            _groundDisc = null;
            _rangeDisc = null;
            _visible = false;
        }

        private GameObject GetIndicatorForAimKind(
            AimKind kind)
        {
            switch (kind)
            {
                case AimKind.Direction:
                    return _directionInstance;
                case AimKind.Point:
                case AimKind.Unit:
                    return _groundTargetInstance;
                case AimKind.Self:
                    return _rangeCircleInstance;
                default:
                    return null;
            }
        }

        private void PositionIndicator(
            GameObject target,
            AimKind kind,
            fp2 casterPos,
            fp2 casterForward)
        {
            Vector3 unityCaster =
                Fp2ToVector3(casterPos);
            switch (kind)
            {
                case AimKind.Direction:
                    target.transform.position =
                        unityCaster;
                    target.transform.forward =
                        Fp2ToVector3Direction(
                            casterForward);
                    SetDirectionBarLength(
                        _directionBody,
                        _directionHead,
                        _directionLength);
                    break;
                case AimKind.Point:
                case AimKind.Unit:
                    target.transform.position =
                        unityCaster;
                    SetCircleScale(
                        _groundDisc,
                        _groundRadius);
                    UnityEngine.Debug.Log(
                        $"[Indicator] groundDot worldRot=" +
                        $"{(_groundDisc != null ? _groundDisc.rotation.eulerAngles.ToString() : "NULL")} " +
                        $"scale={(_groundDisc != null ? _groundDisc.lossyScale.ToString() : "NULL")}");
                    break;
                case AimKind.Self:
                    target.transform.position =
                        unityCaster;
                    break;
            }
        }

        private void UpdateDirectionIndicator(
            GameObject target,
            fp2 cursorWorldPos,
            fp2 casterPosition)
        {
            fp2 toTarget =
                cursorWorldPos - casterPosition;
            fp distSq =
                fpmath.dot(
                    toTarget,
                    toTarget);
            if (distSq <= fp.zero)
            {
                return;
            }

            fp dist = fpmath.sqrt(distSq);
            fp2 dir = toTarget / dist;
            Vector3 unityDir =
                new Vector3(
                    (float)dir.x,
                    0f,
                    (float)dir.y);

            target.transform.position =
                Fp2ToVector3(casterPosition);
            target.transform.forward =
                unityDir;
            SetDirectionBarLength(
                _directionBody,
                _directionHead,
                _directionLength);
        }

        private void UpdateGroundTargetIndicator(
            GameObject target,
            fp2 cursorWorldPos,
            fp2 casterPosition)
        {
            // Clamp the cursor circle to the cast range.
            fp2 toTarget =
                cursorWorldPos - casterPosition;
            fp distSq =
                fpmath.dot(
                    toTarget,
                    toTarget);
            fp2 clamped = cursorWorldPos;
            if (distSq > fp.zero)
            {
                fp dist = fpmath.sqrt(distSq);
                if (dist > _activeRange)
                {
                    clamped =
                        casterPosition +
                        (toTarget / dist) *
                        _activeRange;
                }
            }
            target.transform.position =
                Fp2ToVector3(clamped);
        }

        private void UpdateRangeCircleIndicator(
            GameObject target,
            fp2 casterPosition)
        {
            target.transform.position =
                Fp2ToVector3(casterPosition);
        }

        /// <summary>
        /// The Direction bar is a 2D Quad structure (Body + rounded Head)
        /// that extends from the caster toward +Z for
        /// <paramref name="length"/> world units. The bar is raised slightly
        /// above the ground to avoid z-fighting with the map floor. The
        /// caster is the rotation center: the bar is always positioned in
        /// the root's local Z (the caster's forward) direction.
        /// </summary>
        private static void SetDirectionBarLength(
            Transform body,
            Transform head,
            fp length)
        {
            float l = (float)length;
            if (body != null)
            {
                Vector3 p = body.localPosition;
                p.y = DirectionBarHeight;
                p.z = l * 0.5f;
                body.localPosition = p;
                Vector3 s = body.localScale;
                s.y = l;
                body.localScale = s;
            }
            if (head != null)
            {
                Vector3 p = head.localPosition;
                p.y = DirectionBarHeight;
                p.z = l;
                head.localPosition = p;
            }
        }

        /// <summary>
        /// Height of the direction bar above the ground plane.
        /// </summary>
        public const float DirectionBarHeight = 0.10f;

        /// <summary>
        /// Scans the local transform hierarchy for a child with the given
        /// name (case-insensitive).
        /// </summary>
        private static Transform FindChild(
            Transform root,
            string name)
        {
            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform child =
                    root.GetChild(i);
                if (child.name
                        .Equals(
                            name,
                            System.StringComparison
                                .OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            return null;
        }

        private static void SetCircleScale(
            Transform disc,
            fp radius)
        {
            if (disc == null)
            {
                return;
            }
            // Keep the circle flat on the world XZ plane regardless of any
            // parent rotation (the indicator instances live under the
            // gameplay camera rig, which is pitched ~40 degrees). The
            // built-in Quad faces -Z, so +90 degrees around X turns its
            // front face up (+Y) in world space and renders correctly under
            // back-face culling from above.
            disc.rotation =
                UnityEngine.Quaternion.Euler(
                    90f,
                    0f,
                    0f);
            float r =
                (float)radius * 2f;
            Vector3 s = disc.localScale;
            s.x = r;
            s.y = r;
            disc.localScale = s;
        }

        private static void SetWorldXZPosition(
            Transform target,
            fp2 position,
            float y)
        {
            target.position =
                new Vector3(
                    (float)position.x,
                    y,
                    (float)position.y);
        }

        private void HideAllInstances()
        {
            if (_directionInstance != null)
            {
                _directionInstance
                    .SetActive(false);
            }
            if (_rangeCircleInstance != null)
            {
                _rangeCircleInstance
                    .SetActive(false);
            }
            if (_groundTargetInstance != null)
            {
                _groundTargetInstance
                    .SetActive(false);
            }
        }

        private static Vector3 Fp2ToVector3(
            fp2 v) =>
            new Vector3(
                (float)v.x,
                0f,
                (float)v.y);

        private static Vector3
            Fp2ToVector3Direction(
                fp2 v) =>
            new Vector3(
                (float)v.x,
                0f,
                (float)v.y);
    }
}
