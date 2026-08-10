using System.Collections.Generic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Local pointer resolver (Player Input v1.1 GameplayPointerResolver).
    /// Ground points use a mathematical ray/plane intersection (no Unity
    /// physics). Unit picking uses the deterministic logical positions:
    /// nearest alive targetable unit within a pick radius of the clicked
    /// ground point, tie-broken by unit kind and UnitUid. Presentation
    /// colliders are not required.
    /// </summary>
    public sealed class MouseWorldResolver
    {
        private readonly Camera camera;
        private readonly Plane groundPlane;
        private readonly UnitWorld unitWorld;
        private readonly fp pickRadius;

        /// <summary>
        /// Last recorded screen position, updated each frame by the input
        /// controller.
        /// </summary>
        public Vector2 LastScreenPosition { get; set; }

        public MouseWorldResolver(
            Camera camera,
            fp groundY,
            UnitWorld unitWorld = null,
            fp pickRadius = default)
        {
            this.camera = camera != null
                ? camera
                : throw new System.ArgumentNullException(
                    nameof(camera));
            groundPlane = new Plane(
                Vector3.up,
                new Vector3(
                    0f,
                    (float)groundY,
                    0f));
            this.unitWorld = unitWorld;
            this.pickRadius = pickRadius > fp.zero
                ? pickRadius
                : (fp)4;
        }

        public fp2? ResolveGroundPoint(
            Vector2 screenPosition)
        {
            Ray ray = camera.ScreenPointToRay(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    0f));
            if (!groundPlane.Raycast(
                    ray,
                    out float enter))
            {
                return null;
            }
            Vector3 hit = ray.GetPoint(enter);
            return new fp2((fp)hit.x, (fp)hit.z);
        }

        public UnitUid? ResolveUnitTarget(
            Vector2 screenPosition)
        {
            fp2? ground =
                ResolveGroundPoint(screenPosition);
            if (!ground.HasValue ||
                unitWorld == null)
            {
                return null;
            }
            fp2 point = ground.Value;
            fp radiusSq =
                pickRadius * pickRadius;
            IReadOnlyList<UnitType> units =
                unitWorld.GetAllUnits();

            UnitUid best = default;
            fp bestDistanceSq =
                new fp(int.MaxValue);
            int bestKindPriority =
                int.MaxValue;
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType candidate = units[i];
                if (candidate == null ||
                    candidate.PhysicsEntity == null)
                {
                    continue;
                }
                if (candidate.LifeState !=
                        LifeState.Alive ||
                    !candidate.CapabilityState
                        .IsTargetable ||
                    candidate.TeamId ==
                        TeamId.Neutral)
                {
                    continue;
                }
                fp2 position =
                    candidate.PhysicsEntity
                        .Transform2D.Position;
                fp distanceSq =
                    fpmath.lengthsq(
                        position - point);
                if (distanceSq > radiusSq)
                {
                    continue;
                }
                int kindPriority =
                    GetKindPriority(
                        candidate.UnitKind);
                if (!best.IsValid() ||
                    distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq &&
                     (kindPriority <
                          bestKindPriority ||
                      (kindPriority ==
                           bestKindPriority &&
                       candidate.UnitUid
                           .CompareTo(best) < 0))))
                {
                    best = candidate.UnitUid;
                    bestDistanceSq = distanceSq;
                    bestKindPriority =
                        kindPriority;
                }
            }

            return best.IsValid()
                ? best
                : (UnitUid?)null;
        }

        private static int GetKindPriority(
            UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Hero:
                    return 0;
                case UnitKind.Monster:
                    return 1;
                case UnitKind.Minion:
                    return 2;
                case UnitKind.Structure:
                    return 3;
                default:
                    return 4;
            }
        }
    }
}
