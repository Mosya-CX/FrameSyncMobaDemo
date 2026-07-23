using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class PresentationSyncManager
    {
        private readonly VfxManager _vfxManager;
        private readonly AudioManager _audioManager;

        public PresentationSyncManager(VfxManager vfxManager, AudioManager audioManager)
        {
            _vfxManager = vfxManager;
            _audioManager = audioManager;
        }

        public void ConsumeAllEvents()
        {
            var vfxEvents = Unit.VisualEventOutput.ConsumeVfxEvents();
            for (int i = 0; i < vfxEvents.Count; i++)
            {
                _vfxManager?.PlayOrReconcile(vfxEvents[i]);
            }

            var sfxEvents = Unit.VisualEventOutput.ConsumeSfxEvents();
            for (int i = 0; i < sfxEvents.Count; i++)
            {
                _audioManager?.PlayOrReconcile(sfxEvents[i]);
            }
        }

        public void ClearAll()
        {
            Unit.VisualEventOutput.Clear();
        }
    }
}
