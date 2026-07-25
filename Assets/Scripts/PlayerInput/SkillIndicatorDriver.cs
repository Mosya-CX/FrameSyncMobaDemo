using System;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Manages ability aim indicator GameObjects.
    /// Shows/hides/updates indicator visuals based on AimKind and cursor position.
    /// Owns a small pool of indicator instances, one per supported shape.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorDriver : MonoBehaviour
    {
        [Header("Indicator prefabs (assigned in Inspector or code)")]
        [SerializeField] private GameObject directionIndicatorPrefab;
        [SerializeField] private GameObject rangeCirclePrefab;
        [SerializeField] private GameObject groundTargetPrefab;

        private GameObject _directionInstance;
        private GameObject _rangeCircleInstance;
        private GameObject _groundTargetInstance;

        private AimKind _activeKind;
        private fp _activeRange;
        private bool _visible;
        private fp _activeRadius;

        public bool IsVisible => _visible;
        public AimKind ActiveKind => _activeKind;

        private void Awake()
        {
            // Instantiate indicators as children, hidden by default
            if (directionIndicatorPrefab != null)
            {
                _directionInstance = Instantiate(directionIndicatorPrefab, transform);
                _directionInstance.SetActive(false);
            }
            if (rangeCirclePrefab != null)
            {
                _rangeCircleInstance = Instantiate(rangeCirclePrefab, transform);
                _rangeCircleInstance.SetActive(false);
            }
            if (groundTargetPrefab != null)
            {
                _groundTargetInstance = Instantiate(groundTargetPrefab, transform);
                _groundTargetInstance.SetActive(false);
            }
        }

        /// <summary>
        /// Show the appropriate indicator for the given AimKind.
        /// </summary>
        public void Show(AimKind kind, fp castRange, fp2 casterPosition, fp2 casterForward)
        {
            HideAllInstances();
            _activeKind = kind;
            _activeRange = castRange;
            _activeRadius = castRange;
            _visible = true;

            GameObject target = GetIndicatorForAimKind(kind);
            if (target != null)
            {
                target.SetActive(true);
                PositionIndicator(target, kind, casterPosition, casterForward);
            }
        }

        /// <summary>
        /// Update the indicator position/direction based on cursor world position.
        /// </summary>
        public void UpdateCursor(fp2 cursorWorldPos, fp2 casterPosition, fp2 casterForward)
        {
            if (!_visible) return;

            GameObject target = GetIndicatorForAimKind(_activeKind);
            if (target == null) return;

            switch (_activeKind)
            {
                case AimKind.Direction:
                    UpdateDirectionIndicator(target, cursorWorldPos, casterPosition);
                    break;
                case AimKind.Point:
                case AimKind.Unit:
                    UpdateGroundTargetIndicator(target, cursorWorldPos, casterPosition);
                    break;
                case AimKind.Self:
                    UpdateRangeCircleIndicator(target, casterPosition);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Hide all indicators and reset state.
        /// </summary>
        public void Hide()
        {
            HideAllInstances();
            _visible = false;
            _activeKind = AimKind.None;
        }

        /// <summary>
        /// Force-clear all indicators without changing state tracking.
        /// Used during cleanup/destroy.
        /// </summary>
        public void ForceClear()
        {
            if (_directionInstance != null) Destroy(_directionInstance);
            if (_rangeCircleInstance != null) Destroy(_rangeCircleInstance);
            if (_groundTargetInstance != null) Destroy(_groundTargetInstance);
            _directionInstance = null;
            _rangeCircleInstance = null;
            _groundTargetInstance = null;
            _visible = false;
        }

        private GameObject GetIndicatorForAimKind(AimKind kind)
        {
            switch (kind)
            {
                case AimKind.Direction:
                    return _directionInstance;
                case AimKind.Point:
                case AimKind.Unit:
                    return _groundTargetInstance;
                case AimKind.Self:
                    return _rangeCircleInstance;
                default:
                    return null;
            }
        }

        private void PositionIndicator(GameObject target, AimKind kind, fp2 casterPos, fp2 casterForward)
        {
            Vector3 unityCaster = Fp2ToVector3(casterPos);
            switch (kind)
            {
                case AimKind.Direction:
                    target.transform.position = unityCaster;
                    target.transform.forward = Fp2ToVector3Direction(casterForward);
                    ScaleArrow(target, _activeRange);
                    break;
                case AimKind.Point:
                case AimKind.Unit:
                    target.transform.position = unityCaster;
                    break;
                case AimKind.Self:
                    target.transform.position = unityCaster;
                    ScaleCircle(target, _activeRange);
                    break;
            }
        }

        private void UpdateDirectionIndicator(GameObject target, fp2 cursorWorldPos, fp2 casterPosition)
        {
            fp2 toTarget = cursorWorldPos - casterPosition;
            fp distSq = fpmath.dot(toTarget, toTarget);
            if (distSq <= fp.zero) return;

            fp dist = fpmath.sqrt(distSq);
            // Clamp to cast range
            fp effectiveDist = dist;
            if (effectiveDist > _activeRange) effectiveDist = _activeRange;

            fp2 dir = toTarget / dist;
            Vector3 unityDir = new Vector3((float)dir.x, 0f, (float)dir.y);

            target.transform.position = Fp2ToVector3(casterPosition);
            target.transform.forward = unityDir;
            ScaleArrow(target, effectiveDist);
        }

        private void UpdateGroundTargetIndicator(GameObject target, fp2 cursorWorldPos, fp2 casterPosition)
        {
            // Clamp to cast range from caster position
            fp2 toTarget = cursorWorldPos - casterPosition;
            fp distSq = fpmath.dot(toTarget, toTarget);
            fp2 clamped = cursorWorldPos;
            if (distSq > fp.zero)
            {
                fp dist = fpmath.sqrt(distSq);
                if (dist > _activeRange)
                    clamped = casterPosition + (toTarget / dist) * _activeRange;
            }
            target.transform.position = Fp2ToVector3(clamped);
        }

        private void UpdateRangeCircleIndicator(GameObject target, fp2 casterPosition)
        {
            target.transform.position = Fp2ToVector3(casterPosition);
        }

        private void ScaleArrow(GameObject arrow, fp length)
        {
            if (arrow == null) return;
            Vector3 scale = arrow.transform.localScale;
            scale.z = (float)length;
            arrow.transform.localScale = scale;
        }

        private void ScaleCircle(GameObject circle, fp radius)
        {
            if (circle == null) return;
            float r = (float)radius;
            circle.transform.localScale = new Vector3(r, 1f, r);
        }

        private void HideAllInstances()
        {
            if (_directionInstance != null) _directionInstance.SetActive(false);
            if (_rangeCircleInstance != null) _rangeCircleInstance.SetActive(false);
            if (_groundTargetInstance != null) _groundTargetInstance.SetActive(false);
        }

        private static Vector3 Fp2ToVector3(fp2 v) => new Vector3((float)v.x, 0f, (float)v.y);
        private static Vector3 Fp2ToVector3Direction(fp2 v) => new Vector3((float)v.x, 0f, (float)v.y);
    }
}
