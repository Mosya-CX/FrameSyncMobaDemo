using System.Diagnostics;
using FrameSyncMoba.PlayerInput;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// CameraDebugScene pointer accuracy/performance harness. Detection uses
    /// the formal ray-to-mathematical-plane resolver, then scans lightweight
    /// proxies with the same center-radius / nearest / stable-id rule.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDebugPointerProbe : MonoBehaviour
    {
        private const int CircleSegments = 64;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraDebugWorkbench workbench;
        [SerializeField] private bool showRuntimeMetrics = true;
        [SerializeField] private bool showDetectionRange = true;

        private MouseWorldResolver resolver;
        private float resolverGroundY = float.NaN;
        private CameraDebugSelectableProxy hovered;
        private LineRenderer rangeRenderer;
        private Material rangeMaterial;
        private Vector3 lastGroundPoint;
        private bool hasGroundPoint;
        private double averageDetectionMicroseconds;
        private double peakDetectionMicroseconds;
        private double lastDetectionMicroseconds;
        private double lastOutlineMicroseconds;
        private int sampleCount;
        private int candidateCount;
        private float hitGroundDistance;
        private float hitScreenDistance;

        public double AverageDetectionMicroseconds =>
            averageDetectionMicroseconds;
        public double PeakDetectionMicroseconds => peakDetectionMicroseconds;
        public int CandidateCount => candidateCount;
        public CameraDebugSelectableProxy Hovered => hovered;

        public void Configure(
            Camera camera,
            CameraDebugWorkbench source)
        {
            targetCamera = camera;
            workbench = source;
            InvalidateResolver();
        }

        public void InvalidateResolver()
        {
            resolver = null;
            resolverGroundY = float.NaN;
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null || workbench == null)
                return;
            EnsureResolver();

            long detectionStart = Stopwatch.GetTimestamp();
            fp2? ground = resolver.ResolveGroundPoint(Input.mousePosition);
            CameraDebugSelectableProxy next = null;
            candidateCount = 0;
            hitGroundDistance = 0f;
            hitScreenDistance = 0f;
            if (ground.HasValue)
            {
                lastGroundPoint = new Vector3(
                    (float)ground.Value.x,
                    workbench.PointerGroundY,
                    (float)ground.Value.y);
                hasGroundPoint = true;
                next = SelectNearest(
                    ground.Value,
                    (fp)workbench.PointerPickRadius,
                    out candidateCount,
                    out hitGroundDistance);
                if (next != null)
                {
                    Vector3 screen = targetCamera.WorldToScreenPoint(
                        next.SelectionPoint);
                    hitScreenDistance = Vector2.Distance(
                        Input.mousePosition,
                        new Vector2(screen.x, screen.y));
                }
            }
            else
            {
                hasGroundPoint = false;
            }
            lastDetectionMicroseconds = ElapsedMicroseconds(detectionStart);
            sampleCount++;
            averageDetectionMicroseconds +=
                (lastDetectionMicroseconds - averageDetectionMicroseconds) /
                sampleCount;
            peakDetectionMicroseconds = System.Math.Max(
                peakDetectionMicroseconds,
                lastDetectionMicroseconds);

            long outlineStart = Stopwatch.GetTimestamp();
            ApplyHovered(next);
            lastOutlineMicroseconds = ElapsedMicroseconds(outlineStart);
            UpdateRangeRenderer();
        }

        public static CameraDebugSelectableProxy SelectNearest(
            Vector3 groundPoint,
            float radius,
            out int candidates,
            out float bestDistance)
        {
            return SelectNearest(
                new fp2((fp)groundPoint.x, (fp)groundPoint.z),
                (fp)radius,
                out candidates,
                out bestDistance);
        }

        private static CameraDebugSelectableProxy SelectNearest(
            fp2 groundPoint,
            fp radius,
            out int candidates,
            out float bestDistance)
        {
            fp radiusSq = radius * radius;
            fp bestDistanceSq = new fp(int.MaxValue);
            int bestStableId = int.MaxValue;
            CameraDebugSelectableProxy best = null;
            candidates = 0;
            var proxies = CameraDebugSelectableProxy.ActiveProxies;
            for (int i = 0; i < proxies.Count; i++)
            {
                CameraDebugSelectableProxy proxy = proxies[i];
                if (proxy == null || !proxy.isActiveAndEnabled)
                    continue;
                candidates++;
                Vector3 position = proxy.SelectionPoint;
                fp2 delta = new fp2(
                    (fp)position.x,
                    (fp)position.z) - groundPoint;
                fp distanceSq = fpmath.lengthsq(delta);
                if (distanceSq > radiusSq)
                    continue;
                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq &&
                     proxy.StableId < bestStableId))
                {
                    best = proxy;
                    bestDistanceSq = distanceSq;
                    bestStableId = proxy.StableId;
                }
            }
            bestDistance = best != null
                ? Mathf.Sqrt((float)bestDistanceSq)
                : 0f;
            return best;
        }

        private void EnsureResolver()
        {
            if (resolver != null &&
                Mathf.Approximately(
                    resolverGroundY,
                    workbench.PointerGroundY))
            {
                return;
            }
            resolverGroundY = workbench.PointerGroundY;
            resolver = new MouseWorldResolver(
                targetCamera,
                (fp)resolverGroundY);
        }

        private void ApplyHovered(CameraDebugSelectableProxy next)
        {
            if (hovered == next)
                return;
            if (hovered != null && hovered.Outline != null)
            {
                hovered.Outline.SetHighlighted(false, Color.white);
            }
            hovered = next;
            if (hovered == null || hovered.Outline == null)
                return;
            hovered.Outline.SetOutlineWidth(workbench.OutlineWidth);
            hovered.Outline.SetHighlighted(
                true,
                workbench.PreviewTeamId == hovered.TeamId
                    ? workbench.FriendlyOutlineColor
                    : workbench.EnemyOutlineColor);
        }

        private void UpdateRangeRenderer()
        {
            EnsureRangeRenderer();
            bool visible = showDetectionRange && hasGroundPoint;
            rangeRenderer.enabled = visible;
            if (!visible)
                return;
            float radius = workbench.PointerPickRadius;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / CircleSegments;
                rangeRenderer.SetPosition(
                    i,
                    lastGroundPoint + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0.03f,
                        Mathf.Sin(angle) * radius));
            }
        }

        private void EnsureRangeRenderer()
        {
            if (rangeRenderer != null)
                return;
            rangeRenderer = gameObject.AddComponent<LineRenderer>();
            rangeRenderer.useWorldSpace = true;
            rangeRenderer.loop = false;
            rangeRenderer.positionCount = CircleSegments + 1;
            rangeRenderer.startWidth = 0.035f;
            rangeRenderer.endWidth = 0.035f;
            rangeRenderer.startColor = new Color(0f, 1f, 1f, 0.9f);
            rangeRenderer.endColor = rangeRenderer.startColor;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                rangeMaterial = new Material(shader)
                {
                    name = "CameraDebugRangeRuntime",
                    hideFlags = HideFlags.DontSave,
                };
                rangeRenderer.sharedMaterial = rangeMaterial;
            }
        }

        private void OnGUI()
        {
            if (!showRuntimeMetrics || workbench == null)
                return;
            string hit = hovered != null
                ? $"Proxy #{hovered.StableId} Team {hovered.TeamId}"
                : "None";
            GUI.Box(
                new Rect(12f, 12f, 390f, 154f),
                "Pointer Accuracy / Performance");
            GUI.Label(new Rect(24f, 40f, 370f, 22f),
                $"Side: {workbench.PreviewTeamId}   Pick radius: {workbench.PointerPickRadius:F2}");
            GUI.Label(new Rect(24f, 62f, 370f, 22f),
                $"Candidates: {candidateCount}   Hit: {hit}");
            GUI.Label(new Rect(24f, 84f, 370f, 22f),
                $"Ground error: {hitGroundDistance:F3}   Screen error: {hitScreenDistance:F2}px");
            GUI.Label(new Rect(24f, 106f, 370f, 22f),
                $"Detect: {lastDetectionMicroseconds:F2}us   Avg: {averageDetectionMicroseconds:F2}us   Peak: {peakDetectionMicroseconds:F2}us");
            GUI.Label(new Rect(24f, 128f, 370f, 22f),
                $"Outline switch: {lastOutlineMicroseconds:F2}us   TAB switches side");
        }

        private void OnDestroy()
        {
            if (hovered != null && hovered.Outline != null)
                hovered.Outline.SetHighlighted(false, Color.white);
            if (rangeMaterial != null)
                Destroy(rangeMaterial);
        }

        private static double ElapsedMicroseconds(long start)
        {
            return (Stopwatch.GetTimestamp() - start) *
                   (1000000.0 / Stopwatch.Frequency);
        }
    }
}
