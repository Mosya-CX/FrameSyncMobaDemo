using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Keeps an attached VFX aligned to the world direction carried by its
    /// VfxEvent. Position still follows the unit host; later host rotations
    /// cannot rotate the cast direction a second time.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class VfxWorldDirectionLock : MonoBehaviour
    {
        private Quaternion worldRotation;
        private bool active;

        public void Begin(Vector3 worldDirection)
        {
            Vector3 planar = Vector3.ProjectOnPlane(
                worldDirection,
                Vector3.up);
            active = planar.sqrMagnitude > 0.0001f;
            if (!active)
                return;
            worldRotation = Quaternion.LookRotation(
                planar.normalized,
                Vector3.up);
            ApplyRotation();
        }

        public void ResetForPool()
        {
            active = false;
        }

        private void LateUpdate()
        {
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            if (active)
                transform.rotation = worldRotation;
        }
    }
}
