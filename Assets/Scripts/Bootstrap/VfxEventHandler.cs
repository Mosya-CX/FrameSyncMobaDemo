using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class VfxEventHandler :
        MonoBehaviour,
        IVfxHandler
    {
        [SerializeField] private VfxManager vfxManager;

        public void SetManager(VfxManager manager)
        {
            vfxManager = manager;
        }

        public void OnVfxEvent(in VfxEvent evt)
        {
            if (vfxManager != null)
                vfxManager.PlayOrReconcile(evt);
        }
    }
}
