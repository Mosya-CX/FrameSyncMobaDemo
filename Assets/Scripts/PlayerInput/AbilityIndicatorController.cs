using System;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Reads the active formal AbilityDef and drives the local indicator.
    /// No timing, range or AimKind is duplicated in PlayerInput configuration.
    ///
    /// Design: Ability v15.2 section 1.5-1.7.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityIndicatorController : MonoBehaviour
    {
        [SerializeField] private SkillIndicatorDriver _driver;

        private AbilityDef _activeDefinition;
        private bool _isActive;
        private fp2 _casterPosition;
        private fp2 _casterForward;

        public bool IsActive => _isActive;

        private void Awake()
        {
            if (_driver == null)
                _driver = GetComponent<SkillIndicatorDriver>();
        }

        /// <summary>
        /// Begin showing the indicator from the formal Ability definition.
        /// </summary>
        public void Show(
            AbilityDef definition,
            fp2 casterPosition,
            fp2 casterForward)
        {
            if (definition?.CastModel == null ||
                _driver == null)
            {
                return;
            }

            _activeDefinition = definition;
            _casterPosition = casterPosition;
            _casterForward = casterForward;
            _isActive = true;

            if (RequiresAim())
            {
                _driver.Show(
                    definition.AimKind,
                    definition.CastRange,
                    casterPosition,
                    casterForward);
            }
        }

        /// <summary>
        /// Update the indicator position based on cursor world position.
        /// </summary>
        public void UpdateCursor(fp2 cursorWorldPos)
        {
            if (!_isActive || _driver == null) return;
            _driver.UpdateCursor(cursorWorldPos, _casterPosition, _casterForward);
        }

        /// <summary>
        /// Hide the indicator (on ability commit, cancel, or session end).
        /// </summary>
        public void Hide()
        {
            _isActive = false;
            _activeDefinition = null;
            _driver?.Hide();
        }

        /// <summary>
        /// Resolve the formal stage key currently used by the indicator.
        /// </summary>
        public byte? GetIndicatorStageKey(
            byte currentStageKey)
        {
            return _activeDefinition?.CastModel
                ?.ResolveIndicatorStage(currentStageKey);
        }

        /// <summary>
        /// Query whether this indicator requires the player to aim (non-self-target).
        /// </summary>
        public bool RequiresAim()
        {
            AimKind kind =
                _activeDefinition?.AimKind ?? AimKind.None;
            return kind == AimKind.Point ||
                kind == AimKind.Unit ||
                kind == AimKind.Direction;
        }

        /// <summary>
        /// Get the maximum focus duration in ticks (for hold-release abilities).
        /// </summary>
        public int GetMaxFocusTicks()
        {
            return _activeDefinition?.CastModel is
                    HoldReleaseCastModelDef hold
                ? hold.Hold.DurationTicks
                : 0;
        }

        private void OnDestroy()
        {
            _driver?.ForceClear();
        }
    }
}
