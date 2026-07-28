using System.Collections.Generic;
using FrameSyncMoba.Presentation;
using FrameSyncMoba.Unit;
using UnitType = FrameSyncMoba.Unit.Unit;
using FrameSyncMoba.FrameSync;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Triggers respawn animation when a hero respawns.
    /// Reads animation parameter hashes from UnitAnimationProfile for
    /// deterministic Animator trigger dispatch.
    ///
    /// Design: Presentation v13.2 section 3.9.
    /// </summary>
    public sealed class RespawnPresenter : MonoBehaviour
    {
        private readonly Dictionary<UnitUid, Animator> _animatorCache = new Dictionary<UnitUid, Animator>();

        /// <summary>
        /// Trigger respawn animation on the given unit.
        /// Called by GameBootstrap after UnitWorld.CompleteRespawn.
        /// </summary>
        public void TriggerRespawn(UnitType unit)
        {
            if (unit == null) return;

            if (!_animatorCache.TryGetValue(unit.UnitUid, out var animator))
            {
                animator = unit.GetComponentInChildren<Animator>();
                if (animator != null)
                    _animatorCache[unit.UnitUid] = animator;
            }

            if (animator != null)
            {
                // Use UnitAnimationProfile hash or fall back to string name
                var profile = unit.GetComponent<UnitPresentationHost>()?.Profile;
                if (profile != null && profile.RespawnTriggerHash != 0)
                {
                    animator.SetTrigger(Animator.StringToHash(
                        profile.RespawnTriggerHash.ToString()));
                }
                else
                {
                    animator.SetTrigger("Respawn");
                }
            }
        }

        private void OnDestroy()
        {
            _animatorCache.Clear();
        }
    }
}
