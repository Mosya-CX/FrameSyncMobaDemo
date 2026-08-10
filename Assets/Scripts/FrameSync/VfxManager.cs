using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class VfxManager : MonoBehaviour
    {
        [SerializeField] private VfxLibrary _library;
        [SerializeField] private int _defaultPoolSize = 16;

        private readonly Dictionary<int, Queue<GameObject>> _poolByDefId =
            new Dictionary<int, Queue<GameObject>>();
        private readonly Dictionary<int, GameObject> _prefabCache =
            new Dictionary<int, GameObject>();

        public void SetLibrary(VfxLibrary library)
        {
            _library = library;
            _prefabCache.Clear();
            _poolByDefId.Clear();
        }

        public void PlayOrReconcile(in Unit.VfxEvent evt)
        {
            if (_library == null)
            {
                Debug.Log(string.Format("[VfxManager] VFX {0} (no VfxLibrary configured)", evt.VfxDefId));
                return;
            }

            GameObject prefab = GetPrefab(evt.VfxDefId);
            if (prefab == null)
            {
                Debug.LogWarning(string.Format("[VfxManager] VFX {0}: prefab not found", evt.VfxDefId));
                return;
            }

            GameObject instance = GetFromPool(evt.VfxDefId, prefab);
            if (instance == null)
                return;

            Vector3 targetPosition = new Vector3(
                (float)evt.WorldPosition.x, 0f, (float)evt.WorldPosition.y);
            Vector3 sourcePosition = targetPosition;
            bool attachToUnit = false;
            UnitPresentationHost attachHost = null;
            if (evt.AttachToUnit.HasValue &&
                evt.AttachToUnit.Value.IsValid() &&
                UnitPresentationRegistry.TryGetHost(
                    evt.AttachToUnit.Value,
                    out attachHost) &&
                attachHost != null)
            {
                attachToUnit = true;
            }
            if (evt.Id.SourceRuntimeUid.IsValid() &&
                UnitPresentationRegistry.TryGetHost(
                    evt.Id.SourceRuntimeUid,
                    out UnitPresentationHost sourceHost) &&
                sourceHost != null)
            {
                sourcePosition = sourceHost.transform.position;
            }

            if (attachToUnit)
            {
                instance.transform.SetParent(
                    attachHost.transform,
                    false);
                instance.transform.localPosition =
                    Vector3.zero;
            }
            else
            {
                instance.transform.SetParent(
                    transform,
                    false);
                instance.transform.position =
                    targetPosition;
            }
            instance.SetActive(true);

            VfxPlaybackHost playbackHost =
                instance.GetComponent<VfxPlaybackHost>();
            float duration;
            if (playbackHost != null)
            {
                duration = playbackHost.BeginPlayback(
                    sourcePosition,
                    targetPosition,
                    Mathf.Max(0.01f, (float)evt.DurationScale));
            }
            else
            {
                duration = PlayParticleFallback(instance);
            }
            if (attachToUnit)
            {
                instance.transform.localPosition =
                    Vector3.zero;
            }

            StartCoroutine(ReturnAfterPlay(
                instance,
                evt.VfxDefId,
                duration));
        }

        private GameObject GetPrefab(int vfxDefId)
        {
            if (_prefabCache.TryGetValue(vfxDefId, out GameObject cached))
                return cached;

            GameObject prefab = _library.GetPrefab(vfxDefId);
            if (prefab != null)
                _prefabCache[vfxDefId] = prefab;
            return prefab;
        }

        private GameObject GetFromPool(int vfxDefId, GameObject prefab)
        {
            if (!_poolByDefId.TryGetValue(vfxDefId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _poolByDefId[vfxDefId] = queue;
            }

            if (queue.Count > 0)
                return queue.Dequeue();

            GameObject instance = Object.Instantiate(prefab, transform);
            instance.name = string.Format("VFX_{0}_{1}", vfxDefId, _poolByDefId.Count);
            return instance;
        }

        private void ReturnToPool(int vfxDefId, GameObject instance)
        {
            VfxPlaybackHost playbackHost =
                instance.GetComponent<VfxPlaybackHost>();
            if (playbackHost != null)
                playbackHost.ResetForPool();
            instance.transform.SetParent(
                transform,
                false);
            instance.transform.localPosition =
                Vector3.zero;
            instance.transform.localRotation =
                Quaternion.identity;
            instance.transform.localScale =
                Vector3.one;
            instance.SetActive(false);
            if (!_poolByDefId.TryGetValue(vfxDefId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _poolByDefId[vfxDefId] = queue;
            }
            queue.Enqueue(instance);
        }

        private static float PlayParticleFallback(
            GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>();
            float maxDuration = 1f;
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Play();
                float dur = systems[i].main.duration + systems[i].main.startLifetime.constantMax;
                if (dur > maxDuration) maxDuration = dur;
            }
            return maxDuration;
        }

        private System.Collections.IEnumerator ReturnAfterPlay(
            GameObject instance,
            int vfxDefId,
            float duration)
        {
            yield return new WaitForSeconds(
                Mathf.Max(0.01f, duration) + 0.1f);
            ReturnToPool(vfxDefId, instance);
        }
    }

}
