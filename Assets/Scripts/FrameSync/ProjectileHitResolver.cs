using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public struct ProjectileHitResult
    {
        public ProjectileUid ProjectileUid;
        public UnitUid TargetUnitUid;
        public fp2 HitPosition;
        public fp HitDistance;
        public int CandidateOrder;
        public int HitLogicTick;
    }

    public sealed class ProjectileHitResolver
    {
        private readonly PhysicsWorld physicsWorld;
        private readonly UnitWorld unitWorld;
        private readonly List<PhysicsEntity2D> candidates =
            new List<PhysicsEntity2D>();
        private readonly List<ProjectileHitResult> projectileHits =
            new List<ProjectileHitResult>();
        private readonly List<ProjectileHitResult> pendingHits =
            new List<ProjectileHitResult>();

        public ProjectileHitResolver(
            PhysicsWorld physicsWorld,
            UnitWorld unitWorld)
        {
            this.physicsWorld = physicsWorld ??
                throw new System.ArgumentNullException(
                    nameof(physicsWorld));
            this.unitWorld = unitWorld ??
                throw new System.ArgumentNullException(
                    nameof(unitWorld));
        }

        public IReadOnlyList<ProjectileHitResult>
            PendingHits => pendingHits;

        public void ResolveAllHits(
            ProjectileWorld projectileWorld)
        {
            pendingHits.Clear();
            if (projectileWorld == null) return;
            PhysicsSpatialGrid2D grid =
                physicsWorld.UnitFinalGrid;
            if (grid == null) return;

            IReadOnlyList<ProjectileRuntime> projectiles =
                projectileWorld.GetAllOrdered();
            int tick = SimulationTickContext.Current.Tick;
            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileRuntime projectile =
                    projectiles[i];
                if (!projectile.ShouldQuery(tick))
                    continue;

                projectile.MarkQueried(tick);
                ResolveProjectileHits(
                    projectile,
                    grid,
                    tick);
                for (int hitIndex = 0;
                     hitIndex < projectileHits.Count;
                     hitIndex++)
                {
                    pendingHits.Add(
                        projectileHits[hitIndex]);
                }
            }
        }

        public void EmitEffects(
            ProjectileWorld projectileWorld)
        {
            if (projectileWorld == null)
            {
                pendingHits.Clear();
                return;
            }

            for (int i = 0; i < pendingHits.Count; i++)
            {
                ProjectileHitResult hit =
                    pendingHits[i];
                if (!projectileWorld.TryGet(
                        hit.ProjectileUid,
                        out ProjectileRuntime projectile))
                    continue;
                if (!projectile.RegisterHit(
                        hit.TargetUnitUid,
                        hit.HitLogicTick))
                    continue;

                ProjectileEffectDispatcher.DispatchOnHit(
                    projectile,
                    hit.TargetUnitUid,
                    unitWorld);
                if (projectile.Def.AoE.HasAoE &&
                    projectile.Def.AoE.Trigger ==
                    AoETrigger.OnImpact)
                {
                    ProjectileEffectDispatcher.DispatchAoE(
                        projectile,
                        hit.HitPosition,
                        projectile.Def.AoE.AoERadius,
                        unitWorld,
                        physicsWorld);
                }
            }

            pendingHits.Clear();
        }

        public void ProcessAllHits(
            ProjectileWorld projectileWorld)
        {
            ResolveAllHits(projectileWorld);
            EmitEffects(projectileWorld);
        }

        private void ResolveProjectileHits(
            ProjectileRuntime projectile,
            PhysicsSpatialGrid2D grid,
            int tick)
        {
            fp radius = projectile.Def.HitRadius;
            fp2 previous = projectile.PrevPosition;
            fp2 current = projectile.Position;
            fp2 extent = new fp2(radius, radius);
            var queryBounds = new PhysicsBounds2D(
                fpmath.min(previous, current) - extent,
                fpmath.max(previous, current) + extent);

            candidates.Clear();
            grid.CollectCandidates(
                queryBounds,
                candidates);
            projectileHits.Clear();

            // Single-target homing attacks (e.g. basic attacks) must only
            // resolve against the locked tracked target. Units that happen
            // to stand between the projectile and the tracked target are
            // ignored; only the tracked target may be hit.
            bool restrictToTracked =
                projectile.Def.HitPolicy
                    .RestrictToTrackedTarget &&
                projectile.TargetUnitUid.IsValid();

            for (int i = 0; i < candidates.Count; i++)
            {
                PhysicsEntity2D entity = candidates[i];
                if (!(entity.QueryInfo.Owner is
                    UnitType target))
                    continue;
                if (restrictToTracked &&
                    target.UnitUid !=
                        projectile.TargetUnitUid)
                {
                    continue;
                }
                if (!projectile.Def.TargetFilter.Allows(
                        target,
                        projectile.OwnerUnitUid,
                        projectile.TeamSnapshot))
                    continue;
                if (!projectile.CanHitTarget(
                        target.UnitUid,
                        tick))
                    continue;
                if (!OverlapsTarget(
                        previous,
                        current,
                        radius,
                        entity))
                    continue;

                fp2 targetPoint =
                    PhysicsGeometry2D.GetPointWorld(
                        entity.Transform2D,
                        entity.Shape);
                fp2 hitPosition =
                    PhysicsGeometry2D.ClosestPointOnSegment(
                        targetPoint,
                        previous,
                        current);
                projectileHits.Add(
                    new ProjectileHitResult
                    {
                        ProjectileUid = projectile.Uid,
                        TargetUnitUid = target.UnitUid,
                        HitPosition = hitPosition,
                        HitDistance = fpmath.length(
                            hitPosition - previous),
                        HitLogicTick = tick,
                    });
            }

            projectileHits.Sort(CompareHitResults);
            for (int i = 0; i < projectileHits.Count; i++)
            {
                ProjectileHitResult hit =
                    projectileHits[i];
                hit.CandidateOrder = i;
                projectileHits[i] = hit;
            }
        }

        private static int CompareHitResults(
            ProjectileHitResult left,
            ProjectileHitResult right)
        {
            int comparison =
                left.HitDistance.CompareTo(
                    right.HitDistance);
            if (comparison != 0) return comparison;
            return left.TargetUnitUid.CompareTo(
                right.TargetUnitUid);
        }

        private static bool OverlapsTarget(
            fp2 previous,
            fp2 current,
            fp projectileRadius,
            PhysicsEntity2D target)
        {
            PhysicsTransform2D transform =
                target.Transform2D;
            PhysicsShape2D shape = target.Shape;
            switch (shape.Kind)
            {
                case PhysicsShapeKind.Point:
                    return PhysicsGeometry2D
                        .SweptPointOverlapsCircle(
                            previous,
                            current,
                            PhysicsGeometry2D.GetPointWorld(
                                transform,
                                shape),
                            projectileRadius);

                case PhysicsShapeKind.Circle:
                    return PhysicsGeometry2D
                        .SweptPointOverlapsCircle(
                            previous,
                            current,
                            PhysicsGeometry2D.GetPointWorld(
                                transform,
                                shape),
                            projectileRadius +
                            shape.Radius);

                case PhysicsShapeKind.Segment:
                    PhysicsGeometry2D.GetSegmentWorld(
                        transform,
                        shape,
                        out fp2 segmentStart,
                        out fp2 segmentEnd,
                        out fp width);
                    fp allowed =
                        projectileRadius +
                        width / (fp)2;
                    return SegmentDistanceSquared(
                            previous,
                            current,
                            segmentStart,
                            segmentEnd) <=
                        allowed * allowed;

                case PhysicsShapeKind.Rect:
                    PhysicsGeometry2D.GetRectWorld(
                        transform,
                        shape,
                        out fp2 center,
                        out fp2 right,
                        out fp2 forward,
                        out fp2 halfExtents);
                    fp2 expanded =
                        halfExtents +
                        new fp2(
                            projectileRadius,
                            projectileRadius);
                    fp2 localPrevious =
                        ToLocal(
                            previous,
                            center,
                            right,
                            forward);
                    fp2 localCurrent =
                        ToLocal(
                            current,
                            center,
                            right,
                            forward);
                    return SegmentIntersectsAabb(
                        localPrevious,
                        localCurrent,
                        expanded);

                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported target PhysicsShapeKind {shape.Kind}.");
            }
        }

        private static fp2 ToLocal(
            fp2 point,
            fp2 center,
            fp2 right,
            fp2 forward)
        {
            fp2 delta = point - center;
            return new fp2(
                fpmath.dot(delta, right),
                fpmath.dot(delta, forward));
        }

        private static fp SegmentDistanceSquared(
            fp2 aStart,
            fp2 aEnd,
            fp2 bStart,
            fp2 bEnd)
        {
            if (SegmentsIntersect(
                    aStart,
                    aEnd,
                    bStart,
                    bEnd))
                return fp.zero;

            fp best = DistanceToSegmentSquared(
                aStart,
                bStart,
                bEnd);
            best = fpmath.min(
                best,
                DistanceToSegmentSquared(
                    aEnd,
                    bStart,
                    bEnd));
            best = fpmath.min(
                best,
                DistanceToSegmentSquared(
                    bStart,
                    aStart,
                    aEnd));
            return fpmath.min(
                best,
                DistanceToSegmentSquared(
                    bEnd,
                    aStart,
                    aEnd));
        }

        private static fp DistanceToSegmentSquared(
            fp2 point,
            fp2 start,
            fp2 end)
        {
            fp2 closest =
                PhysicsGeometry2D.ClosestPointOnSegment(
                    point,
                    start,
                    end);
            return fpmath.lengthsq(point - closest);
        }

        private static bool SegmentsIntersect(
            fp2 a,
            fp2 b,
            fp2 c,
            fp2 d)
        {
            fp abC = Cross(b - a, c - a);
            fp abD = Cross(b - a, d - a);
            fp cdA = Cross(d - c, a - c);
            fp cdB = Cross(d - c, b - c);

            if (((abC > fp.zero &&
                  abD < fp.zero) ||
                 (abC < fp.zero &&
                  abD > fp.zero)) &&
                ((cdA > fp.zero &&
                  cdB < fp.zero) ||
                 (cdA < fp.zero &&
                  cdB > fp.zero)))
                return true;

            if (abC == fp.zero &&
                OnSegment(a, b, c)) return true;
            if (abD == fp.zero &&
                OnSegment(a, b, d)) return true;
            if (cdA == fp.zero &&
                OnSegment(c, d, a)) return true;
            return cdB == fp.zero &&
                OnSegment(c, d, b);
        }

        private static fp Cross(fp2 a, fp2 b) =>
            a.x * b.y - a.y * b.x;

        private static bool OnSegment(
            fp2 start,
            fp2 end,
            fp2 point)
        {
            return point.x >=
                    fpmath.min(start.x, end.x) &&
                point.x <=
                    fpmath.max(start.x, end.x) &&
                point.y >=
                    fpmath.min(start.y, end.y) &&
                point.y <=
                    fpmath.max(start.y, end.y);
        }

        private static bool SegmentIntersectsAabb(
            fp2 start,
            fp2 end,
            fp2 halfExtents)
        {
            fp tMin = fp.zero;
            fp tMax = fp.one;
            fp2 delta = end - start;
            return ClipAxis(
                    start.x,
                    delta.x,
                    -halfExtents.x,
                    halfExtents.x,
                    ref tMin,
                    ref tMax) &&
                ClipAxis(
                    start.y,
                    delta.y,
                    -halfExtents.y,
                    halfExtents.y,
                    ref tMin,
                    ref tMax);
        }

        private static bool ClipAxis(
            fp origin,
            fp delta,
            fp min,
            fp max,
            ref fp tMin,
            ref fp tMax)
        {
            if (delta == fp.zero)
                return origin >= min &&
                    origin <= max;

            fp first = (min - origin) / delta;
            fp second = (max - origin) / delta;
            if (first > second)
            {
                fp swap = first;
                first = second;
                second = swap;
            }

            tMin = fpmath.max(tMin, first);
            tMax = fpmath.min(tMax, second);
            return tMin <= tMax;
        }
    }
}
