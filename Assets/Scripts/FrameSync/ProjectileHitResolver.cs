using System.Collections.Generic;
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
        public int HitLogicTick;
    }

    public sealed class ProjectileHitResolver
    {
        private readonly PhysicsWorld _physicsWorld;
        private readonly UnitWorld _unitWorld;
        private readonly List<PhysicsEntity2D> _candidateBuffer = new List<PhysicsEntity2D>();
        private static readonly fp MinHitRadius = (fp)0.1m;

        public ProjectileHitResolver(PhysicsWorld physicsWorld, UnitWorld unitWorld)
        {
            _physicsWorld = physicsWorld;
            _unitWorld = unitWorld;
        }

        public void ProcessAllHits(ProjectileWorld projectileWorld)
        {
            var projectiles = projectileWorld.GetAllOrdered();
            for (int i = 0; i < projectiles.Count; i++)
            {
                var proj = projectiles[i];
                if (!proj.IsActive) continue;
                ResolveHits(proj);
            }
        }

        private void ResolveHits(ProjectileRuntime proj)
        {
            var grid = _physicsWorld?.UnitFinalGrid;
            if (grid == null) return;

            fp hitRadius = proj.Def.HitRadius;
            if (hitRadius < MinHitRadius) hitRadius = MinHitRadius;

            fp2 prevPos = proj.PrevPosition;
            fp2 currPos = proj.Position;

            fp minX = fpmath.min(prevPos.x, currPos.x) - hitRadius;
            fp minY = fpmath.min(prevPos.y, currPos.y) - hitRadius;
            fp maxX = fpmath.max(prevPos.x, currPos.x) + hitRadius;
            fp maxY = fpmath.max(prevPos.y, currPos.y) + hitRadius;

            var queryBounds = new PhysicsBounds2D(new fp2(minX, minY), new fp2(maxX, maxY));
            _candidateBuffer.Clear();
            grid.CollectCandidates(queryBounds, _candidateBuffer);

            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                var entity = _candidateBuffer[i];
                var queryInfo = entity.QueryInfo;
                if (!(queryInfo.Owner is UnitType targetUnit)) continue;
                if (!targetUnit.UnitUid.IsValid()) continue;
                if (targetUnit.UnitUid == proj.OwnerUnitUid) continue;
                if (!proj.CanHitTarget(targetUnit.UnitUid)) continue;
                if (targetUnit.LifeState != LifeState.Alive && targetUnit.LifeState != LifeState.Dying) continue;

                fp2 targetPos = entity.Transform2D.Position;
                bool hit;
                bool isMoving = prevPos.x != currPos.x || prevPos.y != currPos.y;
                if (isMoving)
                    hit = PhysicsGeometry2D.SweptPointOverlapsCircle(prevPos, currPos, targetPos, hitRadius);
                else
                    hit = PhysicsGeometry2D.PointOverlapsCircle(currPos, targetPos, hitRadius);

                if (hit)
                {
                    proj.RegisterHit(targetUnit.UnitUid);
                    ProjectileEffectDispatcher.DispatchOnHit(proj, targetUnit.UnitUid, _unitWorld);

                    if (proj.Def.AoE.HasAoE && proj.Def.AoE.Trigger == AoETrigger.OnImpact)
                    {
                        ProjectileEffectDispatcher.DispatchAoE(
                            proj, targetPos, proj.Def.AoE.AoERadius, _unitWorld, _physicsWorld);
                    }
                }
            }
        }
    }
}
