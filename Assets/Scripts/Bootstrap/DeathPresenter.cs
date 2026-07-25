using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Triggers death animation and SFX when a unit dies.
    /// Listens for VfxEvent at the unit's position (death VFX submitted by CombatSystem).
    /// </summary>
    public sealed class DeathPresenter : MonoBehaviour, IVfxHandler
    {
        [SerializeField] private AudioClip deathSfx;
        private readonly Dictionary<UnitUid, Animator> _animatorCache = new Dictionary<UnitUid, Animator>();

        public void OnVfxEvent(in VfxEvent evt)
        {
            // Death events are VfxEvents submitted at the dying unit's position.
            // They have no attach target and carry the dying unit as SourceRuntimeUid.
            if (!evt.AttachToUnit.HasValue && evt.Id.SourceKind == PresentationSourceKind.Unit)
            {
                TryTriggerDeath(in evt);
            }
        }

        private void TryTriggerDeath(in VfxEvent evt)
        {
            UnitUid sourceUid = evt.Id.SourceRuntimeUid;
            var units = FindObjectsOfType<FrameSyncMoba.Unit.Unit>();
            foreach (var unit in units)
            {
                if (unit.UnitUid == sourceUid)
                {
                    TriggerDeathForUnit(unit, in evt);
                    return;
                }
            }
        }

        private void TriggerDeathForUnit(FrameSyncMoba.Unit.Unit unit, in VfxEvent evt)
        {
            // Trigger death animation
            if (!_animatorCache.TryGetValue(unit.UnitUid, out var animator))
            {
                animator = unit.GetComponentInChildren<Animator>();
                if (animator != null)
                    _animatorCache[unit.UnitUid] = animator;
            }

            if (animator != null)
            {
                animator.SetTrigger("Death");
            }

            // Play death SFX at position
            if (deathSfx != null)
            {
                Vector3 pos = new Vector3(
                    (float)evt.WorldPosition.x,
                    0f,
                    (float)evt.WorldPosition.y);
                AudioSource.PlayClipAtPoint(deathSfx, pos);
            }
        }

        private void OnDestroy()
        {
            _animatorCache.Clear();
        }
    }
}
