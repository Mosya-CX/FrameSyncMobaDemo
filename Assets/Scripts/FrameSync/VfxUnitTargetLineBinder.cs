using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Presentation-only adapter that keeps one LineRenderer connected from
    /// a fixed event position to a live unit presentation host.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxUnitTargetLineBinder : MonoBehaviour
    {
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private float heightOffset = .15f;

        private Unit.UnitUid? targetUnit;
        private Vector3 anchor;
        private bool active;

        public void Begin(Vector3 worldAnchor, Unit.UnitUid? target)
        {
            anchor = worldAnchor;
            targetUnit = target;
            active = target.HasValue && target.Value.IsValid();
            if (targetLine == null)
                targetLine = GetComponent<LineRenderer>();
            if (targetLine != null)
            {
                targetLine.useWorldSpace = true;
                targetLine.positionCount = 2;
                targetLine.enabled = active;
            }
            RefreshLine();
        }

        public void ResetForPool()
        {
            active = false;
            targetUnit = null;
            if (targetLine != null)
                targetLine.enabled = false;
        }

        private void LateUpdate()
        {
            RefreshLine();
        }

        private void RefreshLine()
        {
            if (!active || targetLine == null ||
                !targetUnit.HasValue ||
                !UnitPresentationRegistry.TryGetHost(
                    targetUnit.Value,
                    out UnitPresentationHost host) ||
                host == null)
            {
                if (targetLine != null)
                    targetLine.enabled = false;
                return;
            }
            targetLine.enabled = true;
            Vector3 offset = Vector3.up * heightOffset;
            targetLine.SetPosition(0, anchor + offset);
            targetLine.SetPosition(1, host.transform.position + offset);
        }
    }
}
