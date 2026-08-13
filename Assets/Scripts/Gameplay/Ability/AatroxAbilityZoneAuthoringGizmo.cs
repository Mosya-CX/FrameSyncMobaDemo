using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum AatroxZoneGizmoDisplayMode : byte
    {
        Off = 0,
        All = 1,
        Single = 2,
    }

    public enum AatroxZoneGizmoSelection : byte
    {
        Q1 = 0,
        Q2 = 1,
        Q3 = 2,
        WTether = 3,
    }

    /// <summary>
    /// Editor-only scene gizmo for tuning Aatrox Q impact zones and the W
    /// tether boundary. It reads the same authoring objects that are baked
    /// into deterministic Gameplay; it never participates in simulation or
    /// presentation at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AatroxAbilityZoneAuthoringGizmo : MonoBehaviour
    {
        [SerializeField] private AbilityAsset qAbility;
        [SerializeField]
        private ProjectileContainmentZoneAuthoring wTetherZone;
        [Tooltip("All draws Q1/Q2/Q3/W together. Single draws only Single Zone. Gizmos remain visible without selecting this GameObject.")]
        [SerializeField]
        private AatroxZoneGizmoDisplayMode displayMode =
            AatroxZoneGizmoDisplayMode.All;
        [Tooltip("Zone drawn while Display Mode is Single.")]
        [SerializeField]
        private AatroxZoneGizmoSelection singleZone =
            AatroxZoneGizmoSelection.Q1;
        [SerializeField] private bool drawSeparated = true;
        [Min(0f)] [SerializeField] private float previewSpacing = 8f;
        [SerializeField] private Color primaryColor =
            new Color(0.95f, 0.15f, 0.1f, 1f);
        [SerializeField] private Color sweetSpotColor =
            new Color(1f, 0.75f, 0.05f, 1f);
        [SerializeField] private Color tetherColor =
            new Color(0.1f, 0.85f, 1f, 1f);

        public AatroxZoneGizmoDisplayMode DisplayMode =>
            displayMode;
        public AatroxZoneGizmoSelection SingleZone =>
            singleZone;

#if UNITY_EDITOR
        private const int CircleSegments = 48;

        private void OnDrawGizmos()
        {
            if (displayMode == AatroxZoneGizmoDisplayMode.Off)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 baseOrigin = transform.position + Vector3.up * 0.05f;

            int qIndex = 0;
            StageDefAuthoring[] stages = qAbility != null
                ? qAbility.Stages
                : null;
            if (stages != null)
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    if (!(stages[i] is
                        DirectionalMultiZoneDamageStageDefAuthoring zone))
                    {
                        continue;
                    }

                    if (ShouldDraw(
                        (AatroxZoneGizmoSelection)qIndex))
                    {
                        Vector3 origin = PreviewOrigin(
                            baseOrigin,
                            right,
                            qIndex,
                            4);
                        DrawDirectionalZone(
                            origin,
                            forward,
                            right,
                            zone);
                    }
                    qIndex++;
                }
            }

            if (wTetherZone != null &&
                ShouldDraw(AatroxZoneGizmoSelection.WTether))
            {
                ProjectileContainmentZone tether =
                    wTetherZone.BakeOrThrow();
                Vector3 origin = PreviewOrigin(
                    baseOrigin,
                    right,
                    3,
                    4);
                Gizmos.color = tetherColor;
                DrawTrapezoid(
                    origin,
                    forward,
                    right,
                    (float)tether.ForwardStart,
                    (float)tether.ForwardLength,
                    (float)tether.NearHalfWidth,
                    (float)tether.FarHalfWidth);
            }
        }

        private bool ShouldDraw(
            AatroxZoneGizmoSelection zone)
        {
            return displayMode ==
                    AatroxZoneGizmoDisplayMode.All ||
                (displayMode ==
                    AatroxZoneGizmoDisplayMode.Single &&
                 singleZone == zone);
        }

        private Vector3 PreviewOrigin(
            Vector3 baseOrigin,
            Vector3 right,
            int index,
            int count)
        {
            if (!drawSeparated ||
                displayMode ==
                    AatroxZoneGizmoDisplayMode.Single)
                return baseOrigin;
            float centeredIndex = index - (count - 1) * 0.5f;
            return baseOrigin + right * (centeredIndex * previewSpacing);
        }

        private void DrawDirectionalZone(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            DirectionalMultiZoneDamageStageDefAuthoring zone)
        {
            Gizmos.color = primaryColor;
            if (zone.Shape == DirectionalZoneShape.OffsetCircle)
            {
                DrawCircle(
                    origin + forward * zone.CircleForwardOffset,
                    zone.CircleRadius);
                if (zone.SweetCircleRadius > 0f)
                {
                    Gizmos.color = sweetSpotColor;
                    DrawCircle(
                        origin + forward * zone.CircleForwardOffset,
                        zone.SweetCircleRadius);
                }
                return;
            }

            DrawTrapezoid(
                origin,
                forward,
                right,
                zone.ForwardStart,
                zone.ForwardLength,
                zone.NearHalfWidth,
                zone.Shape == DirectionalZoneShape.Trapezoid
                    ? zone.FarHalfWidth
                    : zone.NearHalfWidth);

            if (zone.SweetForwardEnd <= zone.SweetForwardStart ||
                zone.ForwardLength <= 0f)
            {
                return;
            }

            float nearWidth = WidthAt(zone, zone.SweetForwardStart);
            float farWidth = WidthAt(zone, zone.SweetForwardEnd);
            Gizmos.color = sweetSpotColor;
            DrawTrapezoid(
                origin,
                forward,
                right,
                zone.SweetForwardStart,
                zone.SweetForwardEnd - zone.SweetForwardStart,
                nearWidth,
                farWidth);
        }

        private static float WidthAt(
            DirectionalMultiZoneDamageStageDefAuthoring zone,
            float longitudinal)
        {
            if (zone.Shape != DirectionalZoneShape.Trapezoid ||
                zone.ForwardLength <= 0f)
            {
                return zone.NearHalfWidth;
            }
            float t = Mathf.Clamp01(
                (longitudinal - zone.ForwardStart) /
                zone.ForwardLength);
            return Mathf.Lerp(
                zone.NearHalfWidth,
                zone.FarHalfWidth,
                t);
        }

        private static void DrawTrapezoid(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float forwardStart,
            float forwardLength,
            float nearHalfWidth,
            float farHalfWidth)
        {
            Vector3 nearCenter = origin + forward * forwardStart;
            Vector3 farCenter =
                origin + forward * (forwardStart + forwardLength);
            Vector3 nearLeft = nearCenter - right * nearHalfWidth;
            Vector3 nearRight = nearCenter + right * nearHalfWidth;
            Vector3 farLeft = farCenter - right * farHalfWidth;
            Vector3 farRight = farCenter + right * farHalfWidth;
            Gizmos.DrawLine(nearLeft, nearRight);
            Gizmos.DrawLine(nearRight, farRight);
            Gizmos.DrawLine(farRight, farLeft);
            Gizmos.DrawLine(farLeft, nearLeft);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            if (radius <= 0f)
                return;
            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector3 current = center +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                    radius;
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

#endif
    }
}
