using System.Collections;
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Triggers brief hit-flash material effect when a unit takes damage.
    /// Listens for VfxEvent with AttachToUnit set (hit VFX).
    /// </summary>
    public sealed class HitReactionPresenter : MonoBehaviour, IVfxHandler
    {
        [Header("Hit flash settings")]
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float flashDuration = 0.1f;

        private readonly Dictionary<UnitUid, Coroutine> _activeFlashes = new Dictionary<UnitUid, Coroutine>();

        public void OnVfxEvent(in VfxEvent evt)
        {
            if (!evt.AttachToUnit.HasValue) return;
            UnitUid uid = evt.AttachToUnit.Value;
            TriggerHitFlash(uid);
        }

        private void TriggerHitFlash(UnitUid uid)
        {
            // Stop existing flash for this unit
            if (_activeFlashes.TryGetValue(uid, out var existing) && existing != null)
            {
                StopCoroutine(existing);
                _activeFlashes.Remove(uid);
            }

            var renderer = FindUnitRenderer(uid);
            if (renderer == null) return;

            var coroutine = StartCoroutine(FlashRoutine(renderer, uid));
            _activeFlashes[uid] = coroutine;
        }

        private IEnumerator FlashRoutine(Renderer renderer, UnitUid uid)
        {
            Material mat = renderer.material;
            Color original = mat.color;
            mat.color = hitFlashColor;
            yield return new WaitForSeconds(flashDuration);
            mat.color = original;
            _activeFlashes.Remove(uid);
        }

        private Renderer FindUnitRenderer(UnitUid uid)
        {
            var units = FindObjectsOfType<FrameSyncMoba.Unit.Unit>();
            foreach (var unit in units)
            {
                if (unit.UnitUid == uid)
                {
                    var renderer = unit.GetComponentInChildren<Renderer>();
                    if (renderer != null) return renderer;
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            _activeFlashes.Clear();
        }
    }
}
