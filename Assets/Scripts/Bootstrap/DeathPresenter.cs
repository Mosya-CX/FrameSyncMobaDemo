using System.Collections.Generic;
using FrameSyncMoba.Presentation;
using FrameSyncMoba.Unit;
using FrameSyncMoba.FrameSync;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Triggers death animation and SFX when a unit dies.
    /// Listens for VfxEvent at the unit's position (death VFX submitted by CombatSystem).
    /// </summary>
    public sealed class DeathPresenter :
        MonoBehaviour,
        IVfxHandler,
        ISfxHandler
    {
        [SerializeField] private AudioClip deathSfx;
        private readonly Dictionary<UnitUid, Animator> _animatorCache = new Dictionary<UnitUid, Animator>();

        public void OnVfxEvent(in VfxEvent evt)
        {
            // Death events are VfxEvents submitted at the dying unit's position.
            // They have no attach target and carry the dying unit as SourceRuntimeUid.
            if (!evt.AttachToUnit.HasValue &&
                evt.Id.SourceKind ==
                    PresentationSourceKind.Unit &&
                evt.Id.EventKey ==
                    PresentationEventKeys
                        .CombatDeath)
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
                    TriggerDeathForUnit(unit);
                    return;
                }
            }
        }

        private void TriggerDeathForUnit(
            FrameSyncMoba.Unit.Unit unit)
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
                var profile = unit
                    .GetComponent<
                        UnitPresentationHost>()
                    ?.Profile;
                if (profile != null && profile.DeathTriggerHash != 0)
                    animator.SetTrigger(profile.DeathTriggerHash);
                else
                    animator.SetTrigger("Death");
            }
        }

        public void OnSfxEvent(
            in SfxEvent evt)
        {
            if (evt.Id.EventKey !=
                    PresentationEventKeys
                        .CombatDeath ||
                deathSfx == null)
                return;
            Vector3 position = new Vector3(
                (float)evt.WorldPosition.x,
                0f,
                (float)evt.WorldPosition.y);
            AudioSource.PlayClipAtPoint(
                deathSfx,
                position);
        }

        private void OnDestroy()
        {
            _animatorCache.Clear();
        }
    }
}
