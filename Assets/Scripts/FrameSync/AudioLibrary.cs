using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Stable SfxDefId -> AudioClip mapping consumed by AudioManager
    /// (Presentation Design v13.2 section 5.2 SfxDefinition). Lives in its
    /// own file so Unity can resolve the asset's m_Script reference.
    /// </summary>
    [CreateAssetMenu(
        menuName =
            "FrameSyncMoba/Audio Library")]
    public sealed class AudioLibrary :
        ScriptableObject
    {
        [SerializeField]
        private AudioClipEntry[] _entries =
            System.Array.Empty<AudioClipEntry>();

        [System.Serializable]
        public struct AudioClipEntry
        {
            public int SfxDefId;
            public AudioClip Clip;
        }

        public AudioClip GetClip(
            int sfxDefId)
        {
            for (int i = 0;
                 i < _entries.Length;
                 i++)
            {
                if (_entries[i].SfxDefId ==
                    sfxDefId)
                {
                    return _entries[i].Clip;
                }
            }
            return null;
        }
    }
}
