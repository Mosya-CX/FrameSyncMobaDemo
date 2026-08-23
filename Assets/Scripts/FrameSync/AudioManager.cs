using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Dictionary<int, IPresentationAssetLease<AudioClip>>
            _clipLeases =
                new Dictionary<int, IPresentationAssetLease<AudioClip>>();
        private readonly Dictionary<int, Task<IPresentationAssetLease<AudioClip>>>
            _pendingLoads =
                new Dictionary<int, Task<IPresentationAssetLease<AudioClip>>>();
        private IClientPresentationAssetLoader _assetLoader;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            _lifetimeCancellation = new CancellationTokenSource();
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

        public void SetLibrary(AudioLibrary library)
        {
            _library = library;
        }

        public void SetAssetLoader(IClientPresentationAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public async void PlayOrReconcile(Unit.SfxEvent evt)
        {
            Debug.Log(
                $"[AudioManager] enter id={evt.SfxDefId} " +
                $"library={_library != null}");
            if (_library == null)
            {
                Debug.Log(string.Format("[AudioManager] SFX {0} (no AudioLibrary configured)", evt.SfxDefId));
                return;
            }

            AudioClip clip;
            try
            {
                clip = await GetClipAsync(evt.SfxDefId);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[AudioManager] SFX {evt.SfxDefId} load failed: {exception}");
                return;
            }
            string clipName =
                clip != null
                    ? clip.name
                    : "null";
            Debug.Log(
                $"[AudioManager] clip={clipName} " +
                $"listener={FindObjectOfType<AudioListener>() != null}");
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
            source.transform.position = ResolvePosition(evt);
            source.Play();

            _activeCounts[evt.SfxDefId] = active + 1;
            StartCoroutine(ReleaseAfterPlay(source, evt.SfxDefId, clip.length));
        }

        private async Task<AudioClip> GetClipAsync(int sfxDefId)
        {
            if (_clipLeases.TryGetValue(
                    sfxDefId,
                    out IPresentationAssetLease<AudioClip> cached))
                return cached.Asset;
            if (_assetLoader == null)
                return null;
            string address = _library.GetAddress(sfxDefId);
            if (string.IsNullOrEmpty(address))
                return null;
            if (!_pendingLoads.TryGetValue(
                    sfxDefId,
                    out Task<IPresentationAssetLease<AudioClip>> pending))
            {
                pending = _assetLoader.AcquireAudioClipAsync(
                    address,
                    _lifetimeCancellation.Token);
                _pendingLoads.Add(sfxDefId, pending);
            }
            IPresentationAssetLease<AudioClip> lease;
            try
            {
                lease = await pending;
            }
            finally
            {
                _pendingLoads.Remove(sfxDefId);
            }
            if (!_clipLeases.ContainsKey(sfxDefId))
                _clipLeases.Add(sfxDefId, lease);
            else if (!ReferenceEquals(_clipLeases[sfxDefId], lease))
                lease.Dispose();
            return _clipLeases[sfxDefId].Asset;
        }

        private void OnDestroy()
        {
            _lifetimeCancellation?.Cancel();
            foreach (IPresentationAssetLease<AudioClip> lease in
                     _clipLeases.Values)
                lease.Dispose();
            _clipLeases.Clear();
            _pendingLoads.Clear();
            _lifetimeCancellation?.Dispose();
        }

        /// <summary>
        /// Playback position: prefer the explicit world position, otherwise
        /// the presentation host of the attached unit (Presentation Design
        /// v13.2 section 5: managers query hosts, never PhysicsEntity).
        /// </summary>
        private Vector3 ResolvePosition(
            in Unit.SfxEvent evt)
        {
            if (evt.WorldPosition.x != default ||
                evt.WorldPosition.y != default)
            {
                return new Vector3(
                    (float)evt.WorldPosition.x,
                    0f,
                    (float)evt.WorldPosition.y);
            }
            if (evt.AttachToUnit.HasValue &&
                UnitPresentationRegistry.TryGetHost(
                    evt.AttachToUnit.Value,
                    out UnitPresentationHost host) &&
                host != null)
            {
                Transform socket =
                    ResolveSocket(
                        host,
                        evt.SocketKey);
                return socket != null
                    ? socket.position
                    : host.transform.position;
            }
            return transform.position;
        }

        /// <summary>
        /// Maps a PresentationAnchor socket key to the host's socket
        /// Transform (Attack Design v6.2 2.2). Returns null when the unit
        /// has no socket set or the socket is not assigned.
        /// </summary>
        private static Transform ResolveSocket(
            UnitPresentationHost host,
            int socketKey)
        {
            PresentationSocketSet sockets =
                host.Sockets;
            if (sockets == null)
            {
                return null;
            }
            string socketName =
                socketKey switch
                {
                    (int)Unit.PresentationAnchor.Head =>
                        "head",
                    (int)Unit.PresentationAnchor.Chest =>
                        "chest",
                    (int)Unit.PresentationAnchor.HandR =>
                        "hand_r",
                    (int)Unit.PresentationAnchor.HandL =>
                        "hand_l",
                    (int)Unit.PresentationAnchor.FootR =>
                        "foot_r",
                    (int)Unit.PresentationAnchor.FootL =>
                        "foot_l",
                    (int)Unit.PresentationAnchor
                        .ProjectileOrigin =>
                        "projectileorigin",
                    _ => "root",
                };
            return sockets.TryGetSocket(
                    socketName,
                    out Transform socket)
                ? socket
                : null;
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

}
