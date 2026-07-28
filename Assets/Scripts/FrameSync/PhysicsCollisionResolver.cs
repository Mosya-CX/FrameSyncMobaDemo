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
        private static readonly fp MapHalfSize = (fp)50;
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
            RadiusClass radiusClass)
        {
            fp2 result = desiredPosition;

            fp minBound = -MapHalfSize + unitRadius + BoundaryMargin;
            fp maxBound = MapHalfSize - unitRadius - BoundaryMargin;
            result = new fp2(
                fpmath.clamp(result.x, minBound, maxBound),
                fpmath.clamp(result.y, minBound, maxBound));

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
                result = ResolveUnitOverlap(result, unitRadius);
            }

            return result;
        }

        private fp2 ResolveUnitOverlap(fp2 target, fp unitRadius)
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
