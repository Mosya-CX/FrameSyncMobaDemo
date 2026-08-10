using System.Collections.Generic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Bootstrap bridge from the presentation SFX stream to the global
    /// AudioManager (Presentation Design v13.2 section 5). Registered with
    /// the PresentationEventDispatcher; every SfxEvent is forwarded to the
    /// pooled AudioManager for playback. Units provide only sockets; they
    /// never hold or manage AudioSource instances.
    /// </summary>
    public sealed class AttackSfxHandler : MonoBehaviour, ISfxHandler
    {
        [SerializeField] private AudioManager audioManager;
        private static bool missingManagerWarned;

        public void SetAudioManager(AudioManager manager)
        {
            audioManager = manager;
        }

        public void OnSfxEvent(in SfxEvent evt)
        {
            Debug.Log(
                $"[AttackSfx] bridge id={evt.SfxDefId} " +
                $"mgr={audioManager != null} " +
                $"anchor={evt.SocketKey}");
            if (audioManager == null)
            {
                if (!missingManagerWarned)
                {
                    missingManagerWarned = true;
                    Debug.LogWarning(
                        "[AttackSfxHandler] no AudioManager configured; " +
                        "SFX events are skipped.");
                }
                return;
            }
            audioManager.PlayOrReconcile(evt);
        }
    }
}
