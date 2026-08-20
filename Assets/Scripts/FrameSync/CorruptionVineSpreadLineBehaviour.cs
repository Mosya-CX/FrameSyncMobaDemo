using FrameSyncMoba.Unit;
using UnityEngine;
using UnityEngine.Serialization;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Lives on the VarusRBuffVFX prefab. When the buff VFX is attached to a
    /// unit that was newly infected by the Corruption Vines spread, reads the
    /// deterministic BuffHandler to find the infecting source unit and draws
    /// a fading LineRenderer from the source to this unit using the shared
    /// VFX material. Presentation only; never affects Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorruptionVineSpreadLineBehaviour :
        MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int vineBuffConfigId = 9113;
        [SerializeField] private float lineWidth = 0.12f;
        [SerializeField, Min(1)]
        private int lineLifetimeMilliseconds;
        [FormerlySerializedAs("lineLifetimeSeconds")]
        [SerializeField, HideInInspector]
        private float legacyLineLifetimeSeconds;

        private void OnEnable()
        {
            TryDrawSpreadLine();
        }

        private void TryDrawSpreadLine()
        {
            UnitPresentationHost host =
                GetComponentInParent<
                    UnitPresentationHost>();
            UnitType owner =
                host != null
                    ? host.OwnerUnit
                    : null;
            if (owner == null ||
                owner.BuffHandler == null)
            {
                return;
            }
            var vineId =
                new BuffConfigId(
                    vineBuffConfigId);
            if (!owner.BuffHandler.TryGetRuntime(
                    vineId,
                    out BuffRuntime runtime))
            {
                return;
            }
            UnitUid sourceUid =
                runtime.SourceUnitUid;
            if (!sourceUid.IsValid() ||
                sourceUid == owner.UnitUid)
            {
                return;
            }
            if (!UnitPresentationRegistry
                    .TryGetHost(
                        sourceUid,
                        out UnitPresentationHost
                            sourceHost) ||
                sourceHost == null ||
                sourceHost.OwnerUnit == null ||
                sourceHost.OwnerUnit
                    .BuffHandler == null ||
                !sourceHost.OwnerUnit.BuffHandler
                    .HasBuff(vineId))
            {
                return;
            }
            SpawnLine(
                sourceHost.transform.position,
                host.transform.position);
        }

        private void SpawnLine(
            Vector3 from,
            Vector3 to)
        {
            var lineGo =
                new GameObject(
                    "CorruptionSpreadLine");
            lineGo.transform.SetParent(
                transform,
                false);
            var line =
                lineGo.AddComponent<
                    LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = lineMaterial;
            line.startColor = Color.white;
            line.endColor = Color.white;
            lineGo.AddComponent<SpreadLineFade>()
                .Initialize(
                    line,
                    (lineLifetimeMilliseconds > 0
                        ? lineLifetimeMilliseconds
                        : legacyLineLifetimeSeconds > 0f
                            ? (int)System.Math.Round(
                                legacyLineLifetimeSeconds * 1000f)
                            : 800));
        }

        private sealed class SpreadLineFade :
            MonoBehaviour
        {
            private LineRenderer line;
            private int lifetimeMilliseconds = 1000;
            private long startedAtMilliseconds;

            public void Initialize(
                LineRenderer renderer,
                int milliseconds)
            {
                line = renderer;
                lifetimeMilliseconds = Mathf.Max(
                    50,
                    milliseconds);
                startedAtMilliseconds =
                    GetMonotonicMilliseconds();
            }

            private void Update()
            {
                float ratio = Mathf.Clamp01(
                    (GetMonotonicMilliseconds() -
                     startedAtMilliseconds) /
                    (float)lifetimeMilliseconds);
                if (line != null)
                {
                    Color c = Color.white;
                    c.a = 1f - ratio;
                    line.startColor = c;
                    line.endColor = c;
                }
                if (ratio >= 1f)
                {
                    Destroy(gameObject);
                }
            }

            private static long GetMonotonicMilliseconds()
            {
                return (long)System.Math.Round(
                    Time.realtimeSinceStartupAsDouble * 1000d,
                    System.MidpointRounding.AwayFromZero);
            }
        }
    }
}
