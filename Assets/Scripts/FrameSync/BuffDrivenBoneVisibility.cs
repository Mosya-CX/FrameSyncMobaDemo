using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Presentation-only bone visibility controlled by one formal Buff.
    /// Useful when a skinned feature is not split into its own Renderer.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class BuffDrivenBoneVisibility : MonoBehaviour
    {
        [SerializeField] private int visibleBuffConfigId;
        [SerializeField] private Transform[] visibilityRoots =
            System.Array.Empty<Transform>();

        private UnitType owner;
        private Vector3[] visibleScales =
            System.Array.Empty<Vector3>();
        private bool? lastVisible;

        public int VisibleBuffConfigId => visibleBuffConfigId;
        public int VisibilityRootCount =>
            visibilityRoots?.Length ?? 0;

        public void Configure(
            int buffConfigId,
            Transform[] roots)
        {
            visibleBuffConfigId = buffConfigId;
            visibilityRoots = roots ??
                System.Array.Empty<Transform>();
            CaptureVisibleScales();
            lastVisible = null;
        }

        private void Awake()
        {
            owner = GetComponent<UnitType>();
            CaptureVisibleScales();
        }

        private void LateUpdate()
        {
            if (owner == null)
                owner = GetComponent<UnitType>();
            bool visible =
                owner?.BuffHandler != null &&
                visibleBuffConfigId > 0 &&
                owner.BuffHandler.HasBuff(
                    new BuffConfigId(visibleBuffConfigId));
            if (lastVisible.HasValue &&
                lastVisible.Value == visible)
            {
                if (!visible)
                    ApplyHidden();
                return;
            }

            if (visible)
                RestoreVisibleScales();
            else
                ApplyHidden();
            lastVisible = visible;
        }

        private void CaptureVisibleScales()
        {
            visibleScales =
                new Vector3[visibilityRoots?.Length ?? 0];
            for (int i = 0; i < visibleScales.Length; i++)
            {
                visibleScales[i] = visibilityRoots[i] != null
                    ? visibilityRoots[i].localScale
                    : Vector3.one;
            }
        }

        private void ApplyHidden()
        {
            for (int i = 0; i < visibilityRoots.Length; i++)
            {
                if (visibilityRoots[i] != null)
                    visibilityRoots[i].localScale = Vector3.zero;
            }
        }

        private void RestoreVisibleScales()
        {
            int count = Mathf.Min(
                visibilityRoots.Length,
                visibleScales.Length);
            for (int i = 0; i < count; i++)
            {
                if (visibilityRoots[i] != null)
                    visibilityRoots[i].localScale = visibleScales[i];
            }
        }

        private void OnDisable()
        {
            RestoreVisibleScales();
            lastVisible = null;
        }
    }
}
