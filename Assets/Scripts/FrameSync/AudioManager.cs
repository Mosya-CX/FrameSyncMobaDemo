using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class AudioManager : MonoBehaviour
    {
        public void PlayOrReconcile(in Unit.SfxEvent evt)
        {
            Debug.Log($"[AudioManager] SFX {evt.SfxDefId} anchor={evt.Anchor} (defId={evt.SfxDefId})");
        }
    }
}
