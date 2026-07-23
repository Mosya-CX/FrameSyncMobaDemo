using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.PlayerInput
{
    public sealed class MouseWorldResolver
    {
        private readonly Camera camera;
        private readonly Plane groundPlane;
        private readonly Func<Collider, UnitUid?> colliderToUnitUid;
        private readonly List<UnitHitCandidate> candidates = new List<UnitHitCandidate>();
        private readonly HashSet<UnitUid> deduplication = new HashSet<UnitUid>();

        public MouseWorldResolver(
            Camera camera,
            fp groundY,
            Func<Collider, UnitUid?> colliderToUnitUid = null)
        {
            this.camera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            groundPlane = new Plane(Vector3.up, new Vector3(0f, (float)groundY, 0f));
            this.colliderToUnitUid = colliderToUnitUid;
        }

        public fp2? ResolveGroundPoint(Vector2 screenPosition)
        {
            Ray ray = camera.ScreenPointToRay(
                new Vector3(screenPosition.x, screenPosition.y, 0f));
            if (!groundPlane.Raycast(ray, out float enter)) return null;
            Vector3 hit = ray.GetPoint(enter);
            return new fp2((fp)hit.x, (fp)hit.z);
        }

        public UnitUid? ResolveUnitTarget(Vector2 screenPosition)
        {
            Ray ray = camera.ScreenPointToRay(
                new Vector3(screenPosition.x, screenPosition.y, 0f));
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(
                ray, 1000f, LayerMask.GetMask("Unit"));

            candidates.Clear();
            deduplication.Clear();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                UnitSelectionProxy proxy = collider.GetComponentInParent<UnitSelectionProxy>();
                UnitUid? uid = proxy != null
                    ? proxy.UnitUid
                    : colliderToUnitUid?.Invoke(collider);
                if (!uid.HasValue || !uid.Value.IsValid() || !deduplication.Add(uid.Value))
                {
                    continue;
                }

                candidates.Add(new UnitHitCandidate(
                    uid.Value,
                    hits[i].distance,
                    proxy != null ? proxy.SelectionPriority : 0));
            }

            candidates.Sort(UnitHitCandidateComparer.Instance);
            return candidates.Count > 0 ? candidates[0].UnitUid : (UnitUid?)null;
        }

        private readonly struct UnitHitCandidate
        {
            public readonly UnitUid UnitUid;
            public readonly float RayDistance;
            public readonly int SelectionPriority;

            public UnitHitCandidate(UnitUid unitUid, float rayDistance, int selectionPriority)
            {
                UnitUid = unitUid;
                RayDistance = rayDistance;
                SelectionPriority = selectionPriority;
            }
        }

        private sealed class UnitHitCandidateComparer : IComparer<UnitHitCandidate>
        {
            public static readonly UnitHitCandidateComparer Instance =
                new UnitHitCandidateComparer();

            public int Compare(UnitHitCandidate x, UnitHitCandidate y)
            {
                int comparison = x.RayDistance.CompareTo(y.RayDistance);
                if (comparison != 0) return comparison;
                comparison = y.SelectionPriority.CompareTo(x.SelectionPriority);
                if (comparison != 0) return comparison;
                return x.UnitUid.CompareTo(y.UnitUid);
            }
        }
    }
}
