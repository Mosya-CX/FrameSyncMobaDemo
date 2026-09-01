using System.Collections.Generic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Manages ability aim indicator GameObjects.
    ///
    /// Supports the MOBA skill-indicator rules:
    /// - ordinary Direction aims show a rounded bar toward the cursor;
    /// - directional multi-zone stages draw their exact primary and sweet-
    ///   spot outlines from the formal runtime definition;
    /// - Point/Unit aims show a cursor-following ground circle;
    /// - aiming abilities may also show a cast-range circle.
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
        private GameObject _worldSpaceRoot;

        private Transform _directionBody;
        private Transform _directionHead;
        private Transform _groundDisc;
        private Transform _rangeDisc;
        private LineRenderer _directionalZoneOutline;
        private LineRenderer _directionalSweetSpotOutline;
        private Material _runtimeLineMaterial;
        private readonly List<Material> _runtimeGenericMaterials =
            new List<Material>();

        private AimKind _activeKind;
        private fp _activeRange;
        private bool _visible;
        private bool _showRangeCircle;
        private fp _groundRadius;
        private fp _directionLength;
        private DirectionalMultiZoneDamageStageDef
            _activeDirectionalZone;

        public bool IsVisible => _visible;
        public AimKind ActiveKind => _activeKind;
        public DirectionalMultiZoneDamageStageDef
            ActiveDirectionalZone => _activeDirectionalZone;

        /// <summary>
        /// Assign prefabs before the driver is enabled. Safe to call before
        /// Awake; existing instances are recreated when the prefabs change.
        /// </summary>
        public void Configure(
            GameObject directionPrefab,
            GameObject rangeCirclePrefab,
            GameObject groundTargetPrefab)
        {
            // ClientContentRuntimeHost may release the previous Addressables
            // leases before rebinding a newly acquired generation.  Instances
            // cloned from that generation must not survive the lease release:
            // their Material/Texture dependencies can already be unloaded.
            // Always rebuild the generic presentation from the assets owned by
            // the new leases, even when the address resolves to the same
            // prefab object identity.
            ReleaseGenericInstances();
            directionIndicatorPrefab =
                directionPrefab;
            this.rangeCirclePrefab =
                rangeCirclePrefab;
            this.groundTargetPrefab =
                groundTargetPrefab;
            Debug.Log(
                $"[IndicatorBind] Configure driver={name} " +
                $"direction={DescribePrefab(directionIndicatorPrefab)} " +
                $"range={DescribePrefab(this.rangeCirclePrefab)} " +
                $"ground={DescribePrefab(this.groundTargetPrefab)}");
            EnsureInstances();
        }

        private void Awake()
        {
            EnsureInstances();
        }

        private void OnDestroy()
        {
            ForceClear();
        }

        private void EnsureInstances()
        {
            EnsureWorldSpaceRoot();
            if (_directionInstance == null &&
                directionIndicatorPrefab != null)
            {
                _directionInstance =
                    Instantiate(
                        directionIndicatorPrefab,
                        _worldSpaceRoot.transform);
                BindGenericRuntimeMaterials(_directionInstance);
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
                LogInstanceBinding(
                    "direction",
                    _directionInstance,
                    _directionBody,
                    _directionHead);
            }
            if (_rangeCircleInstance == null &&
                rangeCirclePrefab != null)
            {
                _rangeCircleInstance =
                    Instantiate(
                        rangeCirclePrefab,
                        _worldSpaceRoot.transform);
                BindGenericRuntimeMaterials(_rangeCircleInstance);
                _rangeCircleInstance
                    .SetActive(false);
                _rangeDisc =
                    FindChild(
                        _rangeCircleInstance.transform,
                        "Disc");
                LogInstanceBinding(
                    "range",
                    _rangeCircleInstance,
                    _rangeDisc,
                    null);
            }
            if (_groundTargetInstance == null &&
                groundTargetPrefab != null)
            {
                _groundTargetInstance =
                    Instantiate(
                        groundTargetPrefab,
                        _worldSpaceRoot.transform);
                BindGenericRuntimeMaterials(_groundTargetInstance);
                _groundTargetInstance
                    .SetActive(false);
                _groundDisc =
                    FindChild(
                        _groundTargetInstance.transform,
                        "Dot");
                LogInstanceBinding(
                    "ground",
                    _groundTargetInstance,
                    _groundDisc,
                    null);
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
            fp targetRadius = default,
            DirectionalMultiZoneDamageStageDef
                directionalZone = null)
        {
            EnsureInstances();
            HideAllInstances();

            _activeKind = kind;
            _activeRange = castRange;
            _activeDirectionalZone =
                kind == AimKind.Direction
                    ? directionalZone
                    : null;
            _showRangeCircle =
                showRangeCircle &&
                kind != AimKind.None &&
                _activeDirectionalZone == null;
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

            if (_activeDirectionalZone != null)
            {
                EnsureDirectionalZoneLines();
                SetLineActive(
                    _directionalZoneOutline,
                    true);
                SetLineActive(
                    _directionalSweetSpotOutline,
                    true);
                UpdateDirectionalZoneIndicator(
                    casterPosition,
                    casterForward);
                Debug.Log(
                    $"[IndicatorRender] Show kind={kind} target=directional-zone " +
                    $"outlineActive={_directionalZoneOutline?.gameObject.activeSelf ?? false} " +
                    $"sweetActive={_directionalSweetSpotOutline?.gameObject.activeSelf ?? false} " +
                    $"lineMaterial={_runtimeLineMaterial?.shader?.name ?? "<null>"}");
                return;
            }

            GameObject target = GetIndicatorForAimKind(kind);
            if (target != null)
            {
                target.SetActive(true);
                PositionIndicator(
                    target,
                    kind,
                    casterPosition,
                    casterForward);
                LogRenderState(
                    "Show",
                    kind,
                    target);
            }
            else
            {
                Debug.LogWarning(
                    $"[IndicatorRender] Show kind={kind} has no bound target " +
                    $"direction={DescribePrefab(_directionInstance)} " +
                    $"range={DescribePrefab(_rangeCircleInstance)} " +
                    $"ground={DescribePrefab(_groundTargetInstance)}.");
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

            if (_activeDirectionalZone != null)
            {
                fp2 toTarget =
                    cursorWorldPos - casterPosition;
                fp distSq = fpmath.dot(toTarget, toTarget);
                fp2 forward = casterForward;
                if (distSq > fp.zero)
                {
                    forward =
                        toTarget / fpmath.sqrt(distSq);
                }
                UpdateDirectionalZoneIndicator(
                    casterPosition,
                    forward);
                return;
            }

            GameObject target =
                GetIndicatorForAimKind(
                    _activeKind);
            if (target == null)
            {
                Debug.LogWarning(
                    $"[IndicatorRender] UpdateCursor kind={_activeKind} " +
                    "has no bound target.");
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
            _activeDirectionalZone = null;
            UnityEngine.Debug.Log(
                "[Indicator] Hide");
        }

        /// <summary>
        /// Force-clear all indicator instances without state tracking.
        /// Used during cleanup/destroy.
        /// </summary>
        public void ForceClear()
        {
            ReleaseGenericInstances();
            if (_directionalZoneOutline != null)
                Destroy(_directionalZoneOutline.gameObject);
            if (_directionalSweetSpotOutline != null)
                Destroy(_directionalSweetSpotOutline.gameObject);
            if (_runtimeLineMaterial != null)
                Destroy(_runtimeLineMaterial);
            if (_worldSpaceRoot != null)
                Destroy(_worldSpaceRoot);
            _directionalZoneOutline = null;
            _directionalSweetSpotOutline = null;
            _runtimeLineMaterial = null;
            _worldSpaceRoot = null;
            _activeDirectionalZone = null;
            _visible = false;
        }

        private void ReleaseGenericInstances()
        {
            HideAllInstances();
            if (_directionInstance != null)
                Destroy(_directionInstance);
            if (_rangeCircleInstance != null)
                Destroy(_rangeCircleInstance);
            if (_groundTargetInstance != null)
                Destroy(_groundTargetInstance);

            _directionInstance = null;
            _rangeCircleInstance = null;
            _groundTargetInstance = null;
            _directionBody = null;
            _directionHead = null;
            _groundDisc = null;
            _rangeDisc = null;

            for (int i = 0; i < _runtimeGenericMaterials.Count; i++)
            {
                if (_runtimeGenericMaterials[i] != null)
                    Destroy(_runtimeGenericMaterials[i]);
            }
            _runtimeGenericMaterials.Clear();
            _activeKind = AimKind.None;
            _showRangeCircle = false;
            _activeDirectionalZone = null;
            _visible = false;
        }

        private void BindGenericRuntimeMaterials(GameObject instance)
        {
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(true);
            Debug.Log(
                $"[IndicatorBind] material-bind instance={instance.name} " +
                $"rendererCount={renderers.Length}");
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] sourceMaterials = renderer.sharedMaterials;
                string failure = ValidateSourceMaterials(sourceMaterials);
                if (failure != null)
                {
                    renderer.enabled = false;
                    Debug.LogError(
                        $"[Indicator] Renderer '{renderer.name}' from " +
                        $"'{instance.name}' was disabled: {failure}");
                    continue;
                }

                var runtimeMaterials =
                    new Material[sourceMaterials.Length];
                for (int materialIndex = 0;
                     materialIndex < sourceMaterials.Length;
                     materialIndex++)
                {
                    Material source = sourceMaterials[materialIndex];
                    Shader shader = source.shader;
                    if (shader == null || !shader.isSupported)
                    {
                        renderer.enabled = false;
                        Debug.LogError(
                            $"[Indicator] Renderer '{renderer.name}' from " +
                            $"'{instance.name}' was disabled: Addressables " +
                            $"source Shader '{shader?.name ?? "<null>"}' is unavailable.");
                        break;
                    }
                    // The Addressables-loaded source Material already owns a
                    // Player-resolved Shader object plus the texture-alpha and
                    // render-state data that define the circle/ring/line
                    // silhouette. Clone that complete object instead of doing
                    // a second global Shader.Find in the built Player.
                    var runtime = new Material(source)
                    {
                        name = $"{source.name} (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    runtimeMaterials[materialIndex] = runtime;
                    _runtimeGenericMaterials.Add(runtime);
                }
                renderer.sharedMaterials = runtimeMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Debug.Log(
                    $"[IndicatorBind] renderer={renderer.name} " +
                    $"instance={instance.name} enabled={renderer.enabled} " +
                    $"materials={DescribeMaterials(renderer.sharedMaterials)}");
            }
        }

        private void EnsureWorldSpaceRoot()
        {
            if (_worldSpaceRoot != null)
                return;

            _worldSpaceRoot =
                new GameObject("SkillIndicatorsWorldSpace");
            Scene ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && ownerScene.isLoaded &&
                _worldSpaceRoot.scene != ownerScene)
            {
                SceneManager.MoveGameObjectToScene(
                    _worldSpaceRoot,
                    ownerScene);
            }
            _worldSpaceRoot.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            _worldSpaceRoot.transform.localScale = Vector3.one;
            Debug.Log(
                $"[IndicatorBind] world-root={_worldSpaceRoot.name} " +
                $"parent=<none> owner={name} ownerPosition={transform.position} " +
                $"ownerRotation={transform.rotation.eulerAngles}");
        }

        private static string ValidateSourceMaterials(
            Material[] sourceMaterials)
        {
            if (sourceMaterials == null || sourceMaterials.Length == 0)
                return "no source material was loaded from Addressables.";
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                if (source == null)
                    return $"source material {i} is null.";
                if (source.shader == null)
                    return $"source material '{source.name}' has no Shader.";
            }
            return null;
        }

        private void EnsureDirectionalZoneLines()
        {
            if (_runtimeLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _runtimeLineMaterial = new Material(shader)
                    {
                        name = "RuntimeAbilityIndicatorLine",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                }
                Debug.Log(
                    $"[IndicatorBind] directional-line-shader=" +
                    $"{_runtimeLineMaterial?.shader?.name ?? "<null>"} " +
                    $"supported={_runtimeLineMaterial?.shader?.isSupported ?? false}");
            }
            if (_directionalZoneOutline == null)
            {
                _directionalZoneOutline = CreateZoneLine(
                    "DirectionalZoneOutline",
                    new Color(0.15f, 0.75f, 1f, 0.95f),
                    0.08f);
            }
            if (_directionalSweetSpotOutline == null)
            {
                _directionalSweetSpotOutline = CreateZoneLine(
                    "DirectionalSweetSpotOutline",
                    new Color(1f, 0.78f, 0.08f, 1f),
                    0.1f);
            }
        }

        private LineRenderer CreateZoneLine(
            string objectName,
            Color color,
            float width)
        {
            var holder = new GameObject(objectName);
            EnsureWorldSpaceRoot();
            holder.transform.SetParent(
                _worldSpaceRoot.transform,
                false);
            var line = holder.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (_runtimeLineMaterial != null)
                line.sharedMaterial = _runtimeLineMaterial;
            holder.SetActive(false);
            return line;
        }

        private void UpdateDirectionalZoneIndicator(
            fp2 casterPosition,
            fp2 forward)
        {
            if (_activeDirectionalZone == null)
                return;
            EnsureDirectionalZoneLines();
            Transform root =
                _directionalZoneOutline.transform;
            SetWorldXZPosition(
                root,
                casterPosition,
                DirectionBarHeight);
            Vector3 unityForward =
                Fp2ToVector3Direction(forward);
            if (unityForward.sqrMagnitude > 0.0001f)
                root.forward = unityForward.normalized;
            _directionalSweetSpotOutline.transform
                .SetPositionAndRotation(
                    root.position,
                    root.rotation);

            WritePrimaryZone(
                _directionalZoneOutline,
                _activeDirectionalZone);
            WriteSweetSpotZone(
                _directionalSweetSpotOutline,
                _activeDirectionalZone);
        }

        private static void WritePrimaryZone(
            LineRenderer line,
            DirectionalMultiZoneDamageStageDef zone)
        {
            if (zone.Shape == DirectionalZoneShape.OffsetCircle)
            {
                WriteCircle(
                    line,
                    (float)zone.CircleForwardOffset,
                    (float)zone.CircleRadius);
                return;
            }
            float farWidth =
                zone.Shape == DirectionalZoneShape.Trapezoid
                    ? (float)zone.FarHalfWidth
                    : (float)zone.NearHalfWidth;
            WriteTrapezoid(
                line,
                (float)zone.ForwardStart,
                (float)zone.ForwardLength,
                (float)zone.NearHalfWidth,
                farWidth);
        }

        private static void WriteSweetSpotZone(
            LineRenderer line,
            DirectionalMultiZoneDamageStageDef zone)
        {
            if (zone.Shape == DirectionalZoneShape.OffsetCircle)
            {
                WriteCircle(
                    line,
                    (float)zone.CircleForwardOffset,
                    (float)zone.SweetCircleRadius);
                return;
            }
            if (zone.SweetForwardEnd <= zone.SweetForwardStart)
            {
                SetLineActive(line, false);
                return;
            }
            SetLineActive(line, true);
            float nearWidth = WidthAt(
                zone,
                (float)zone.SweetForwardStart);
            float farWidth = WidthAt(
                zone,
                (float)zone.SweetForwardEnd);
            WriteTrapezoid(
                line,
                (float)zone.SweetForwardStart,
                (float)(zone.SweetForwardEnd -
                    zone.SweetForwardStart),
                nearWidth,
                farWidth);
        }

        private static float WidthAt(
            DirectionalMultiZoneDamageStageDef zone,
            float longitudinal)
        {
            if (zone.Shape != DirectionalZoneShape.Trapezoid ||
                zone.ForwardLength <= fp.zero)
            {
                return (float)zone.NearHalfWidth;
            }
            float t = Mathf.Clamp01(
                (longitudinal - (float)zone.ForwardStart) /
                (float)zone.ForwardLength);
            return Mathf.Lerp(
                (float)zone.NearHalfWidth,
                (float)zone.FarHalfWidth,
                t);
        }

        private static void WriteTrapezoid(
            LineRenderer line,
            float start,
            float length,
            float nearHalfWidth,
            float farHalfWidth)
        {
            SetLineActive(line, true);
            line.loop = true;
            line.positionCount = 4;
            float end = start + length;
            line.SetPosition(0,
                new Vector3(-nearHalfWidth, 0f, start));
            line.SetPosition(1,
                new Vector3(nearHalfWidth, 0f, start));
            line.SetPosition(2,
                new Vector3(farHalfWidth, 0f, end));
            line.SetPosition(3,
                new Vector3(-farHalfWidth, 0f, end));
        }

        private static void WriteCircle(
            LineRenderer line,
            float forwardOffset,
            float radius)
        {
            if (radius <= 0f)
            {
                SetLineActive(line, false);
                return;
            }
            SetLineActive(line, true);
            const int segments = 48;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle =
                    i * Mathf.PI * 2f / segments;
                line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        forwardOffset +
                        Mathf.Sin(angle) * radius));
            }
        }

        private static void SetLineActive(
            LineRenderer line,
            bool active)
        {
            if (line != null &&
                line.gameObject.activeSelf != active)
            {
                line.gameObject.SetActive(active);
            }
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

        private void LogInstanceBinding(
            string kind,
            GameObject instance,
            Transform primaryChild,
            Transform secondaryChild)
        {
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(true);
            int enabledRendererCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    enabledRendererCount++;
            }
            Debug.Log(
                $"[IndicatorBind] instance kind={kind} name={instance.name} " +
                $"activeSelf={instance.activeSelf} activeInHierarchy={instance.activeInHierarchy} " +
                $"primaryChild={primaryChild?.name ?? "<missing>"} " +
                $"secondaryChild={secondaryChild?.name ?? "<none>"} " +
                $"renderers={renderers.Length} enabledRenderers={enabledRendererCount}");
        }

        private void LogRenderState(
            string operation,
            AimKind kind,
            GameObject target)
        {
            Renderer[] renderers =
                target.GetComponentsInChildren<Renderer>(true);
            int enabledRendererCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null &&
                    renderers[i].enabled &&
                    renderers[i].gameObject.activeInHierarchy)
                    enabledRendererCount++;
            }
            Debug.Log(
                $"[IndicatorRender] {operation} kind={kind} " +
                $"target={target.name} activeSelf={target.activeSelf} " +
                $"activeInHierarchy={target.activeInHierarchy} " +
                $"renderers={renderers.Length} enabledActiveRenderers={enabledRendererCount} " +
                $"directionChild={_directionBody?.name ?? "<missing>"} " +
                $"headChild={_directionHead?.name ?? "<missing>"} " +
                $"rangeChild={_rangeDisc?.name ?? "<missing>"} " +
                $"groundChild={_groundDisc?.name ?? "<missing>"}");
        }

        private static string DescribePrefab(GameObject prefab)
        {
            return prefab == null ? "<null>" : prefab.name;
        }

        private static string DescribeMaterials(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
                return "<none>";
            string description = string.Empty;
            for (int i = 0; i < materials.Length; i++)
            {
                if (i > 0)
                    description += ",";
                Material material = materials[i];
                description += material == null
                    ? "<null>"
                    : $"{material.name}:{material.shader?.name ?? "<null>"}";
            }
            return description;
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
            SetLineActive(_directionalZoneOutline, false);
            SetLineActive(_directionalSweetSpotOutline, false);
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
