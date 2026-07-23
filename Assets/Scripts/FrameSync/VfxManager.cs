using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class VfxManager : MonoBehaviour
    {
        public void PlayOrReconcile(in Unit.VfxEvent evt)
        {
            Debug.Log($"[VfxManager] VFX {evt.VfxDefId} at {evt.WorldPosition} (defId={evt.VfxDefId})");
        }
    }
}
