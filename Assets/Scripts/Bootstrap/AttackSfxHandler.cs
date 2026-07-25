using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Plays AudioClips for attack commit sound effects.
    /// Listens for SfxEvent with Anchor=UnitRoot and routes to the unit's AudioSource.
    /// </summary>
    public sealed class AttackSfxHandler : MonoBehaviour, ISfxHandler
    {
        [SerializeField] private AudioClip defaultAttackSfx;
        private readonly Dictionary<UnitUid, AudioSource> _sourceCache = new Dictionary<UnitUid, AudioSource>();

        public void OnSfxEvent(in SfxEvent evt)
        {
            if (evt.Anchor != SfxAnchor.UnitRoot) return;
            if (!evt.AttachToUnit.HasValue) return;

            UnitUid uid = evt.AttachToUnit.Value;
            AudioSource source = GetOrCreateSource(uid);
            if (source == null) return;

            source.pitch = (float)evt.PitchScale;
            source.volume = (float)evt.VolumeScale;

            if (defaultAttackSfx != null)
                source.PlayOneShot(defaultAttackSfx);
        }

        private AudioSource GetOrCreateSource(UnitUid uid)
        {
            if (_sourceCache.TryGetValue(uid, out var existing) && existing != null)
                return existing;

            // Search scene for the unit's AudioSource
            var units = FindObjectsOfType<FrameSyncMoba.Unit.Unit>();
            foreach (var unit in units)
            {
                if (unit.UnitUid == uid)
                {
                    var source = unit.GetComponentInChildren<AudioSource>();
                    if (source != null)
                    {
                        _sourceCache[uid] = source;
                        return source;
                    }
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            _sourceCache.Clear();
        }
    }
}
