using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioLibrary _library;
        [SerializeField] private int _defaultPoolSize = 8;
        [SerializeField] private int _maxPerDefId = 4;

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<int, int> _activeCounts = new Dictionary<int, int>();

        private void Awake()
        {
            for (int i = 0; i < _defaultPoolSize; i++)
            {
                var go = new GameObject("AudioSource_" + i.ToString());
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                _pool.Add(source);
            }
        }

        public void PlayOrReconcile(in Unit.SfxEvent evt)
        {
            if (_library == null)
            {
                Debug.Log(string.Format("[AudioManager] SFX {0} (no AudioLibrary configured)", evt.SfxDefId));
                return;
            }

            AudioClip clip = _library.GetClip(evt.SfxDefId);
            if (clip == null)
            {
                Debug.LogWarning(string.Format("[AudioManager] SFX {0}: clip not found", evt.SfxDefId));
                return;
            }

            int active;
            _activeCounts.TryGetValue(evt.SfxDefId, out active);
            if (active >= _maxPerDefId)
                return;

            AudioSource source = GetAvailableSource();
            if (source == null)
                return;

            source.clip = clip;
            if (evt.WorldPosition.x != default || evt.WorldPosition.y != default)
            {
                source.transform.position = new Vector3(
                    (float)evt.WorldPosition.x, 0f, (float)evt.WorldPosition.y);
            }
            source.Play();

            _activeCounts[evt.SfxDefId] = active + 1;
            StartCoroutine(ReleaseAfterPlay(source, evt.SfxDefId, clip.length));
        }

        private AudioSource GetAvailableSource()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].isPlaying)
                    return _pool[i];
            }
            var go = new GameObject("AudioSource_" + _pool.Count.ToString());
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            _pool.Add(source);
            return source;
        }

        private System.Collections.IEnumerator ReleaseAfterPlay(
            AudioSource source, int defId, float duration)
        {
            yield return new WaitForSeconds(duration + 0.1f);
            int active;
            _activeCounts.TryGetValue(defId, out active);
            if (active > 0)
                _activeCounts[defId] = active - 1;
        }
    }

    [CreateAssetMenu(menuName = "FrameSyncMoba/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [SerializeField] private AudioClipEntry[] _entries = System.Array.Empty<AudioClipEntry>();

        [System.Serializable]
        public struct AudioClipEntry
        {
            public int SfxDefId;
            public AudioClip Clip;
        }

        public AudioClip GetClip(int sfxDefId)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].SfxDefId == sfxDefId)
                    return _entries[i].Clip;
            }
            return null;
        }
    }
}
