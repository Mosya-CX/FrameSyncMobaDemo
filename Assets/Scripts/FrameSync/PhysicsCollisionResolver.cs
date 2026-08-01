using System.Collections.Generic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public sealed class PhysicsCollisionResolver : IMovementCollisionResolver
    {
        private readonly PhysicsWorld _physicsWorld;
        private readonly PathGridMap2D _pathGrid;
        private static readonly fp BoundaryMargin = (fp)0.1m;
        private static readonly fp PushOutStep = (fp)0.05m;
        private readonly List<PhysicsEntity2D> _nearbyBuffer = new List<PhysicsEntity2D>();

        public PhysicsCollisionResolver(PhysicsWorld physicsWorld, PathGridMap2D pathGrid = null)
        {
            _physicsWorld = physicsWorld;
            _pathGrid = pathGrid;
        }

        public fp2 ClampPosition(
            fp2 desiredPosition,
            fp2 currentPosition,
            fp unitRadius,
            RadiusClass radiusClass,
            UnitUid selfUid)
        {
            fp2 result = desiredPosition;

            if (_pathGrid != null)
            {
                fp2 minimum =
                    _pathGrid.WorldMin +
                    new fp2(
                        unitRadius + BoundaryMargin,
                        unitRadius + BoundaryMargin);
                fp2 maximum =
                    _pathGrid.WorldMax -
                    new fp2(
                        unitRadius + BoundaryMargin,
                        unitRadius + BoundaryMargin);
                result = new fp2(
                    fpmath.clamp(
                        result.x,
                        minimum.x,
                        maximum.x),
                    fpmath.clamp(
                        result.y,
                        minimum.y,
                        maximum.y));
            }

            // Wall-aware clamping via PathGrid
            if (_pathGrid != null)
            {
                fp2 delta = result - currentPosition;
                if (delta.x != fp.zero || delta.y != fp.zero)
                {
                    delta = ForcedMoveExecutor.ResolveWall(
                        currentPosition,
                        delta,
                        _pathGrid,
                        radiusClass);
                    result = currentPosition + delta;
                }
            }

            if (_physicsWorld != null && _physicsWorld.UnitFinalGrid != null)
            {
                result = ResolveUnitOverlap(
                    result,
                    unitRadius,
                    selfUid);
            }

            return result;
        }

        private fp2 ResolveUnitOverlap(
            fp2 target,
            fp unitRadius,
            UnitUid selfUid)
        {
            var grid = _physicsWorld.UnitFinalGrid;
            fp2 result = target;
            fp queryHalf = unitRadius * (fp)3;

            var queryBounds = new PhysicsBounds2D(
                new fp2(target.x - queryHalf, target.y - queryHalf),
                new fp2(target.x + queryHalf, target.y + queryHalf));

            _nearbyBuffer.Clear();
            grid.CollectCandidates(queryBounds, _nearbyBuffer);

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                var other = _nearbyBuffer[i];
                RuntimeUidQueryValue otherUid =
                    other.QueryInfo.UidSnapshot;
                if (otherUid.SpawnLogicTick ==
                        selfUid.SpawnLogicTick &&
                    otherUid.RuntimeEntityPrefabId ==
                        selfUid.RuntimeEntityPrefabId &&
                    otherUid.SpawnSequenceInTick ==
                        selfUid.SpawnSequenceInTick)
                {
                    continue;
                }
                fp2 otherPos = other.Transform2D.Position;
                fp2 delta = result - otherPos;
                fp distSq = fpmath.dot(delta, delta);
                fp minSeparation =
                    unitRadius +
                    other.Shape.Radius;
                fp minSepSq =
                    minSeparation *
                    minSeparation;

                if (distSq < minSepSq && distSq > fp.zero)
                {
                    fp dist = fpmath.sqrt(distSq);
                    fp overlap = minSeparation - dist;
                    if (overlap > fp.zero)
                    {
                        fp2 pushDir = delta / dist;
                        result += pushDir * (overlap + PushOutStep);
                    }
                }
            }

            return result;
        }
    }
}
