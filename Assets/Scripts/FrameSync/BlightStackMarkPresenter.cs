using System;
using System.Collections.Generic;
using System.Threading;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Presentation-only persistent Blight stack markers. Shows one mark on
    /// the unit's left for 1 stack, left + right for 2 stacks, and left +
    /// right + front (a triangle around the unit) for 3 stacks. Reads only
    /// the deterministic BuffHandler; never affects Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlightStackMarkPresenter :
        MonoBehaviour
    {
        [SerializeField] private GameObject markPrefab;
        [SerializeField] private int blightBuffConfigId = 9001;
        [SerializeField] private float markRadius = 1.1f;
        [SerializeField] private float markHeight = 0.8f;

        private Func<IReadOnlyList<UnitType>> unitsProvider;
        private readonly Dictionary<UnitUid, List<GameObject>>
            marksByUnit =
                new Dictionary<UnitUid, List<GameObject>>();
        private readonly Dictionary<UnitUid, int>
            lastStacksByUnit =
                new Dictionary<UnitUid, int>();
        private IPresentationAssetLease<GameObject> markPrefabLease;
        private CancellationTokenSource lifetimeCancellation;

        public void Initialize(
            GameObject prefab,
            Func<IReadOnlyList<UnitType>> provider)
        {
            markPrefab = prefab;
            unitsProvider = provider;
        }

        public async void InitializeAddressable(
            string address,
            Func<IReadOnlyList<UnitType>> provider)
        {
            unitsProvider = provider;
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();
            markPrefabLease?.Dispose();
            markPrefabLease = null;
            markPrefab = null;
            try
            {
                IClientPresentationAssetLoader loader =
                    await ClientPresentationServices.GetLoaderAsync();
                markPrefabLease = await loader.AcquirePrefabAsync(
                    address,
                    lifetimeCancellation.Token);
                markPrefab = markPrefabLease.Asset;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[BlightMarks] Addressable load failed: {exception}");
            }
        }

        private void LateUpdate()
        {
            if (unitsProvider == null)
            {
                return;
            }
            IReadOnlyList<UnitType> units =
                unitsProvider();
            var seen = new HashSet<UnitUid>();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType unit = units[i];
                if (unit == null ||
                    unit.UnitUid.IsValid() == false)
                {
                    continue;
                }
                seen.Add(unit.UnitUid);
                int stacks = ReadStacks(unit);
                if (!lastStacksByUnit.TryGetValue(
                        unit.UnitUid,
                        out int lastStacks) ||
                    lastStacks != stacks)
                {
                    lastStacksByUnit[unit.UnitUid] =
                        stacks;
                    UnityEngine.Debug.Log(
                        $"[BlightMarks] unit={unit.UnitUid} " +
                        $"stacks={stacks}");
                }
                EnsureMarks(unit, stacks);
            }

            // Remove marks for units that no longer exist or lost Blight.
            var stale = new List<UnitUid>();
            foreach (var pair in marksByUnit)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                DestroyMarks(stale[i]);
                lastStacksByUnit.Remove(
                    stale[i]);
            }
        }

        private int ReadStacks(UnitType unit)
        {
            if (unit.BuffHandler == null ||
                !unit.BuffHandler.TryGetRuntime(
                    new BuffConfigId(
                        blightBuffConfigId),
                    out BuffRuntime runtime))
            {
                return 0;
            }
            return Mathf.Clamp(
                runtime.CurrentStacks,
                0,
                3);
        }

        private void EnsureMarks(
            UnitType unit,
            int stacks)
        {
            if (markPrefab == null)
                return;
            List<GameObject> marks;
            if (!marksByUnit.TryGetValue(
                    unit.UnitUid,
                    out marks))
            {
                marks = new List<GameObject>();
                marksByUnit[unit.UnitUid] = marks;
            }

            while (marks.Count > stacks)
            {
                GameObject excess =
                    marks[marks.Count - 1];
                marks.RemoveAt(marks.Count - 1);
                if (excess != null)
                {
                    Destroy(excess);
                }
            }
            while (marks.Count < stacks)
            {
                marks.Add(Instantiate(
                    markPrefab,
                    transform));
            }

            if (stacks == 0)
            {
                return;
            }

            Vector3 center = UnitPosition(unit);
            fp2 forward =
                unit.PhysicsEntity != null
                    ? unit.PhysicsEntity
                        .Transform2D.Forward
                    : new fp2(
                        fp.zero,
                        fp.one);
            fp2 left =
                new fp2(
                    forward.y,
                    -forward.x);
            fp2 right =
                new fp2(
                    -forward.y,
                    forward.x);

            Vector3 Offset(fp2 direction) =>
                center +
                new Vector3(
                    (float)direction.x *
                        markRadius,
                    markHeight,
                    (float)direction.y *
                        markRadius);

            if (stacks >= 1)
            {
                marks[0].transform.position =
                    Offset(left);
            }
            if (stacks >= 2)
            {
                marks[1].transform.position =
                    Offset(right);
            }
            if (stacks >= 3)
            {
                marks[2].transform.position =
                    Offset(forward);
            }
        }

        private static Vector3 UnitPosition(
            UnitType unit)
        {
            if (UnitPresentationRegistry.TryGetHost(
                    unit.UnitUid,
                    out UnitPresentationHost host) &&
                host != null)
            {
                return host.transform.position;
            }
            fp2 position =
                unit.PhysicsEntity != null
                    ? unit.PhysicsEntity
                        .Transform2D.Position
                    : fp2.zero;
            return new Vector3(
                (float)position.x,
                0f,
                (float)position.y);
        }

        private void DestroyMarks(UnitUid uid)
        {
            if (!marksByUnit.TryGetValue(
                    uid,
                    out List<GameObject> marks))
            {
                return;
            }
            marksByUnit.Remove(uid);
            for (int i = 0;
                 i < marks.Count;
                 i++)
            {
                if (marks[i] != null)
                {
                    Destroy(marks[i]);
                }
            }
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            foreach (UnitUid uid in new List<UnitUid>(marksByUnit.Keys))
                DestroyMarks(uid);
            markPrefabLease?.Dispose();
            lifetimeCancellation?.Dispose();
        }
    }
}
