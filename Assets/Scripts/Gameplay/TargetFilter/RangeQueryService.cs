using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Generic spatial range query service for abilities, AI, and combat
    /// (Physics v13.1 section 9). Reads from PhysicsWorld.UnitFinalGrid,
    /// applies UnitTargetFilter, precise shape tests, sort, and truncation.
    /// </summary>
    public sealed class RangeQueryService
    {
        private readonly PhysicsWorld physicsWorld;

        /// <summary>
        /// Scratch list reused across queries to avoid per-call allocation.
        /// The caller must not retain references to this list between queries.
        /// </summary>
        private readonly List<QueryCandidate> scratchCandidates = new List<QueryCandidate>();

        private struct QueryCandidate
        {
            public Unit Unit;
            public fp SortKey;
        }

        public RangeQueryService(PhysicsWorld physicsWorld)
        {
            this.physicsWorld = physicsWorld ?? throw new ArgumentNullException(nameof(physicsWorld));
        }

        /// <summary>
        /// Queries the UnitFinalGrid for units matching the descriptor
        /// (Physics v13.1 section 9.7 pseudocode).
        ///
        /// The caller must provide pre-allocated result and scratch lists.
        /// Results are cleared before population. Scratch is cleared on return.
        /// </summary>
        public void Query(
            in RangeQueryDesc desc,
            UnitUid requesterUid,
            TeamId requesterTeam,
            List<Unit> result,
            List<PhysicsEntity2D> gridCandidates)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (gridCandidates == null)
            {
                throw new ArgumentNullException(nameof(gridCandidates));
            }

            if (physicsWorld.UnitFinalGrid == null)
            {
                result.Clear();
                return;
            }

            result.Clear();
            scratchCandidates.Clear();

            // 1. Build query AABB and collect grid candidates
            PhysicsBounds2D queryAabb = PhysicsGeometry2D.CalculateBounds(desc.Transform, desc.Shape);
            physicsWorld.UnitFinalGrid.CollectCandidates(queryAabb, gridCandidates);

            // 2. Filter, shape-test, and build sort keys
            for (int i = 0; i < gridCandidates.Count; i++)
            {
                PhysicsEntity2D entity = gridCandidates[i];

                // Resolve owner Unit
                var unit = entity.QueryInfo.Owner as Unit;
                if (unit == null)
                {
                    continue;
                }

                // Target filter (team, life state, capability, kind, subkind, prototype)
                if (!desc.TargetFilter.PassFilter(requesterUid, requesterTeam, unit))
                {
                    continue;
                }

                // Precise shape overlap test
                if (!ShapeOverlaps(desc.Transform, desc.Shape, entity))
                {
                    continue;
                }

                // Build sort key
                fp sortKey = BuildSortKey(
                    desc.SortMode,
                    desc.Transform.Position,
                    entity.Transform2D.Position,
                    entity.QueryInfo.UidSnapshot);

                scratchCandidates.Add(new QueryCandidate
                {
                    Unit = unit,
                    SortKey = sortKey,
                });
            }

            // 3. Stable sort (insertion sort for small-to-medium counts, stable)
            StableSort(scratchCandidates);

            // 4. Truncate to MaxResult
            int count = desc.MaxResult > 0
                ? Math.Min(desc.MaxResult, scratchCandidates.Count)
                : scratchCandidates.Count;

            for (int i = 0; i < count; i++)
            {
                result.Add(scratchCandidates[i].Unit);
            }
        }

        /// <summary>
        /// Precise shape overlap test between query shape and entity shape
        /// (Physics v13.1 section 9.7 ShapeOverlap call).
        /// Currently supports Circle queries against Circle entities.
        /// </summary>
        private static bool ShapeOverlaps(
            in PhysicsTransform2D queryTransform,
            in PhysicsShape2D queryShape,
            PhysicsEntity2D entity)
        {
            fp2 queryCenter = queryTransform.Position + queryShape.LocalOffset;

            switch (queryShape.Kind)
            {
                case PhysicsShapeKind.Point:
                {
                    fp2 targetCenter = entity.Transform2D.Position + entity.Shape.LocalOffset;
                    return PhysicsGeometry2D.PointOverlapsCircle(
                        queryCenter, targetCenter, entity.Shape.Radius);
                }

                case PhysicsShapeKind.Circle:
                {
                    fp2 targetCenter = entity.Transform2D.Position + entity.Shape.LocalOffset;
                    return PhysicsGeometry2D.CircleOverlapsCircle(
                        queryCenter, queryShape.Radius,
                        targetCenter, entity.Shape.Radius);
                }

                case PhysicsShapeKind.Rect:
                {
                    fp2 targetCenter = entity.Transform2D.Position + entity.Shape.LocalOffset;
                    PhysicsGeometry2D.GetRectWorld(
                        queryTransform, queryShape,
                        out fp2 center, out fp2 right, out fp2 forward, out fp2 halfExtents);
                    return PhysicsGeometry2D.RectOverlapsCircle(
                        center, right, forward, halfExtents,
                        targetCenter, entity.Shape.Radius);
                }

                case PhysicsShapeKind.Segment:
                {
                    fp2 targetCenter = entity.Transform2D.Position + entity.Shape.LocalOffset;
                    PhysicsGeometry2D.GetSegmentWorld(
                        queryTransform, queryShape,
                        out fp2 start, out fp2 end, out fp width);
                    return PhysicsGeometry2D.SegmentOverlapsCircle(
                        start, end, width,
                        targetCenter, entity.Shape.Radius);
                }

                default:
                    return false;
            }
        }

        private static fp BuildSortKey(
            RangeQuerySortMode sortMode,
            fp2 queryPosition,
            fp2 entityPosition,
            RuntimeUidQueryValue uid)
        {
            switch (sortMode)
            {
                case RangeQuerySortMode.Uid:
                    // Use UidSnapshot composite as sort key (already sorted by grid,
                    // but we encode a deterministic comparable key here too)
                    return UidToSortKey(uid);

                case RangeQuerySortMode.Distance:
                    return fpmath.distancesq(queryPosition, entityPosition);

                case RangeQuerySortMode.DistanceThenUid:
                default:
                {
                    // Combine distance-squared (primary) with tie-break from UID
                    fp distSq = fpmath.distancesq(queryPosition, entityPosition);
                    // Encode: high bits = distance, low bits = uid rank
                    // Use a small epsilon from UID to break ties
                    fp uidTieBreak = fp.FromRaw((uid.SpawnSequenceInTick + 1) & 0xFFFFFF);
                    return distSq + uidTieBreak * fp.FromRaw(1);
                }
            }
        }

        private static fp UidToSortKey(RuntimeUidQueryValue uid)
        {
            // Encode UID as a stable comparable fixed-point key
            long combined =
                ((long)uid.SpawnLogicTick << 32) |
                ((long)uid.RuntimeEntityPrefabId << 16) |
                uid.SpawnSequenceInTick;
            return fp.FromRaw(combined);
        }

        /// <summary>
        /// Simple stable insertion sort. Order is preserved for equal keys.
        /// For the expected small-to-medium candidate counts this is efficient
        /// and avoids the allocation of List.Sort comparers.
        /// </summary>
        private static void StableSort(List<QueryCandidate> candidates)
        {
            for (int i = 1; i < candidates.Count; i++)
            {
                QueryCandidate key = candidates[i];
                int j = i - 1;

                while (j >= 0 && candidates[j].SortKey > key.SortKey)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = key;
            }
        }
    }
}
