using FrameSyncMoba.Unit;
using UnityEngine;
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
        [SerializeField] private float lineLifetimeSeconds = 0.8f;

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
                    lineLifetimeSeconds);
        }

        private sealed class SpreadLineFade :
            MonoBehaviour
        {
            private LineRenderer line;
            private float lifetime = 1f;
            private float elapsed;

            public void Initialize(
                LineRenderer renderer,
                float seconds)
            {
                line = renderer;
                lifetime = Mathf.Max(
                    0.05f,
                    seconds);
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(
                    elapsed / lifetime);
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
        }
    }
}
