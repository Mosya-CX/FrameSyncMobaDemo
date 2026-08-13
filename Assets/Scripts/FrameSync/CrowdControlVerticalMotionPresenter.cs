using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Presentation-only vertical arc for knock-up and knock-back controls.
    /// Gameplay remains planar; only a wrapper above the animated model is
    /// moved on Y, so the authoritative unit root is never written here.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class CrowdControlVerticalMotionPresenter :
        MonoBehaviour
    {
        private sealed class Entry
        {
            public Transform OffsetRoot;
            public Vector3 BaseLocalPosition;
        }

        private Func<IReadOnlyList<UnitType>> unitsProvider;
        private Func<int> tickProvider;
        private float ticksPerSecond = 30f;
        private readonly Dictionary<UnitUid, Entry> entries =
            new Dictionary<UnitUid, Entry>();
        private readonly List<CrowdControlInstance> instances =
            new List<CrowdControlInstance>(8);
        private readonly HashSet<UnitUid> seen =
            new HashSet<UnitUid>();
        private readonly List<UnitUid> stale =
            new List<UnitUid>();

        public void Initialize(
            Func<IReadOnlyList<UnitType>> provider,
            Func<int> currentTickProvider,
            float simulationTicksPerSecond)
        {
            unitsProvider = provider ??
                throw new ArgumentNullException(nameof(provider));
            tickProvider = currentTickProvider ??
                throw new ArgumentNullException(
                    nameof(currentTickProvider));
            ticksPerSecond = Mathf.Max(
                1f,
                simulationTicksPerSecond);
        }

        private void LateUpdate()
        {
            if (unitsProvider == null || tickProvider == null)
                return;

            IReadOnlyList<UnitType> units = unitsProvider();
            int currentTick = tickProvider();
            seen.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                if (unit == null || !unit.UnitUid.IsValid())
                    continue;
                seen.Add(unit.UnitUid);
                Entry entry = GetOrCreateEntry(unit);
                if (entry?.OffsetRoot == null)
                    continue;
                float height = ResolveHeight(
                    unit,
                    currentTick,
                    ticksPerSecond);
                entry.OffsetRoot.localPosition =
                    entry.BaseLocalPosition +
                    Vector3.up * height;
            }

            stale.Clear();
            foreach (KeyValuePair<UnitUid, Entry> pair in entries)
            {
                if (!seen.Contains(pair.Key))
                    stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++)
                entries.Remove(stale[i]);
        }

        private Entry GetOrCreateEntry(UnitType unit)
        {
            if (entries.TryGetValue(unit.UnitUid, out Entry cached) &&
                cached.OffsetRoot != null)
            {
                return cached;
            }
            if (!UnitPresentationRegistry.TryGetHost(
                    unit.UnitUid,
                    out UnitPresentationHost host) ||
                host == null)
            {
                return null;
            }
            Animator animator =
                host.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return null;

            Transform model = animator.transform;
            Transform existing =
                model.parent != null &&
                model.parent.name == "PresentationVerticalOffset"
                    ? model.parent
                    : null;
            if (existing == null)
            {
                Transform originalParent = model.parent;
                int siblingIndex = model.GetSiblingIndex();
                var wrapper = new GameObject(
                    "PresentationVerticalOffset");
                existing = wrapper.transform;
                existing.SetParent(originalParent, false);
                existing.SetSiblingIndex(siblingIndex);
                existing.localPosition = model.localPosition;
                existing.localRotation = model.localRotation;
                existing.localScale = model.localScale;
                model.SetParent(existing, false);
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one;
            }

            var entry = new Entry
            {
                OffsetRoot = existing,
                BaseLocalPosition = existing.localPosition,
            };
            entries[unit.UnitUid] = entry;
            return entry;
        }

        private float ResolveHeight(
            UnitType unit,
            int currentTick,
            float tickRate)
        {
            CrowdControlHandler crowdControl = unit.CrowdControl;
            if (crowdControl == null || crowdControl.Count == 0)
                return 0f;
            crowdControl.FillInstances(instances);
            float height = 0f;
            for (int i = 0; i < instances.Count; i++)
            {
                CrowdControlInstance instance = instances[i];
                bool isKnockUp =
                    instance.ControlId == CrowdControlIds.KnockUp;
                bool isKnockBack =
                    instance.ControlId == CrowdControlIds.KnockBack;
                if (!isKnockUp && !isKnockBack)
                    continue;
                height = Mathf.Max(
                    height,
                    EvaluateArcHeight(
                        instance.StartTick,
                        instance.ExpireTick,
                        currentTick,
                        tickRate,
                        isKnockBack));
            }
            return height;
        }

        public static float EvaluateArcHeight(
            int startTick,
            int expireTick,
            int currentTick,
            float tickRate,
            bool isKnockBack)
        {
            int durationTicks = expireTick - startTick;
            if (durationTicks <= 0 ||
                currentTick < startTick ||
                currentTick >= expireTick)
            {
                return 0f;
            }
            float progress = Mathf.Clamp01(
                (currentTick - startTick + 0.5f) /
                durationTicks);
            float durationSeconds =
                durationTicks / Mathf.Max(1f, tickRate);
            float peakHeight = Mathf.Clamp(
                durationSeconds * 2f,
                0.35f,
                2.5f);
            if (isKnockBack)
                peakHeight *= 0.5f;
            return 4f * peakHeight *
                progress * (1f - progress);
        }
    }
}
