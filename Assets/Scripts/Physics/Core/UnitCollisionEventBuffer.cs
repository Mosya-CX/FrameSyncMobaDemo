using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Owns the only cross-Tick Unit contact state. Detection and dispatch use
    /// explicit UID ordering and never depend on registration order.
    /// </summary>
    public sealed class UnitCollisionEventBuffer
    {
        private readonly List<PhysicsEntity2D> sortedEntities =
            new List<PhysicsEntity2D>();
        private readonly List<UnitContactPair> previousPairs =
            new List<UnitContactPair>();
        private readonly List<UnitContactPair> currentPairs =
            new List<UnitContactPair>();
        private readonly List<UnitContactPair> enterPairs =
            new List<UnitContactPair>();
        private readonly List<UnitContactPair> exitPairs =
            new List<UnitContactPair>();
        private UnitContactPair[] pendingRestoredPairs = Array.Empty<UnitContactPair>();

        public IReadOnlyList<UnitContactPair> PreviousPairs => previousPairs;

        public void DetectAndPublish(IReadOnlyList<PhysicsEntity2D> unitEntities)
        {
            sortedEntities.Clear();
            for (int i = 0; i < unitEntities.Count; i++)
            {
                PhysicsEntity2D entity = unitEntities[i];
                if (entity != null && entity.QueryInfo.Owner != null)
                    sortedEntities.Add(entity);
            }
            sortedEntities.Sort(CompareEntityUid);
            ValidateUniqueEntityUids();

            currentPairs.Clear();
            enterPairs.Clear();
            exitPairs.Clear();

            for (int i = 0; i < sortedEntities.Count; i++)
            {
                PhysicsEntity2D first = sortedEntities[i];
                if (!TryGetParticipant(first, out IUnitCollisionParticipant firstTarget))
                    continue;
                for (int j = i + 1; j < sortedEntities.Count; j++)
                {
                    PhysicsEntity2D second = sortedEntities[j];
                    if (!TryGetParticipant(second, out IUnitCollisionParticipant secondTarget) ||
                        first.QueryInfo.TeamSnapshot == second.QueryInfo.TeamSnapshot ||
                        first.Shape.Kind != PhysicsShapeKind.Circle ||
                        second.Shape.Kind != PhysicsShapeKind.Circle)
                        continue;

                    fp2 firstCenter = PhysicsGeometry2D.GetPointWorld(
                        first.Transform2D, first.Shape);
                    fp2 secondCenter = PhysicsGeometry2D.GetPointWorld(
                        second.Transform2D, second.Shape);
                    if (!PhysicsGeometry2D.CircleOverlapsCircle(
                            firstCenter, first.Shape.Radius,
                            secondCenter, second.Shape.Radius))
                        continue;

                    var pair = new UnitContactPair(
                        first.QueryInfo.UidSnapshot,
                        second.QueryInfo.UidSnapshot);
                    currentPairs.Add(pair);
                    if (previousPairs.BinarySearch(pair) < 0)
                        enterPairs.Add(pair);
                }
            }

            currentPairs.Sort();
            for (int i = 0; i < previousPairs.Count; i++)
                if (currentPairs.BinarySearch(previousPairs[i]) < 0)
                    exitPairs.Add(previousPairs[i]);

            for (int i = 0; i < enterPairs.Count; i++) PublishEnter(enterPairs[i]);
            for (int i = 0; i < exitPairs.Count; i++) PublishExit(exitPairs[i]);

            previousPairs.Clear();
            previousPairs.AddRange(currentPairs);
        }

        public void Capture(ref UnitCollisionEventBufferSnapshot state)
        {
            state.PreviousPairs = new System.Collections.Generic.List<UnitContactPair>(previousPairs);
        }

        public void Restore(in UnitCollisionEventBufferSnapshot state)
        {
            var pairs = state.PreviousPairs ?? new System.Collections.Generic.List<UnitContactPair>();
            for (int i = 0; i < pairs.Count; i++)
            {
                UnitContactPair pair = pairs[i];
                if (!pair.MinUid.IsValid || !pair.MaxUid.IsValid ||
                    pair.MinUid.CompareTo(pair.MaxUid) >= 0 ||
                    (i > 0 && pairs[i - 1].CompareTo(pair) >= 0))
                    throw new InvalidOperationException(
                        "Physics PreviousPairs snapshot is not canonical.");
            }
            pendingRestoredPairs = pairs.ToArray();
        }

        public void ApplyPendingRestore()
        {
            previousPairs.Clear();
            previousPairs.AddRange(pendingRestoredPairs);
            pendingRestoredPairs = Array.Empty<UnitContactPair>();
            currentPairs.Clear();
            enterPairs.Clear();
            exitPairs.Clear();
        }

        private void PublishEnter(UnitContactPair pair)
        {
            if (!TryResolve(pair.MinUid, out PhysicsEntity2D minEntity,
                    out IUnitCollisionParticipant minTarget) ||
                !TryResolve(pair.MaxUid, out PhysicsEntity2D maxEntity,
                    out IUnitCollisionParticipant maxTarget))
                return;
            fp2 delta = PhysicsGeometry2D.GetPointWorld(
                    maxEntity.Transform2D, maxEntity.Shape) -
                PhysicsGeometry2D.GetPointWorld(minEntity.Transform2D, minEntity.Shape);
            fp2 normal = fp2.zero;
            PhysicsGeometry2D.TryCreateFacing(delta, out normal, out _);
            minTarget.PublishUnitCollisionEnter(pair.MaxUid, normal);
            maxTarget.PublishUnitCollisionEnter(pair.MinUid, -normal);
        }

        private void PublishExit(UnitContactPair pair)
        {
            if (TryResolve(pair.MinUid, out _, out IUnitCollisionParticipant minTarget))
                minTarget.PublishUnitCollisionExit(pair.MaxUid);
            if (TryResolve(pair.MaxUid, out _, out IUnitCollisionParticipant maxTarget))
                maxTarget.PublishUnitCollisionExit(pair.MinUid);
        }

        private bool TryResolve(
            RuntimeUidQueryValue uid,
            out PhysicsEntity2D entity,
            out IUnitCollisionParticipant participant)
        {
            int low = 0;
            int high = sortedEntities.Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (sortedEntities[middle].QueryInfo.UidSnapshot.CompareTo(uid) < 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            if (low < sortedEntities.Count &&
                sortedEntities[low].QueryInfo.UidSnapshot == uid &&
                TryGetParticipant(sortedEntities[low], out participant))
            {
                entity = sortedEntities[low];
                return true;
            }
            entity = null;
            participant = null;
            return false;
        }

        private static bool TryGetParticipant(
            PhysicsEntity2D entity,
            out IUnitCollisionParticipant participant)
        {
            participant = entity.QueryInfo.Owner as IUnitCollisionParticipant;
            return participant != null && participant.CanParticipateInUnitCollision;
        }

        private void ValidateUniqueEntityUids()
        {
            for (int i = 0; i < sortedEntities.Count; i++)
            {
                RuntimeUidQueryValue uid = sortedEntities[i].QueryInfo.UidSnapshot;
                if (!uid.IsValid || (i > 0 &&
                    sortedEntities[i - 1].QueryInfo.UidSnapshot == uid))
                    throw new InvalidOperationException(
                        "Registered Unit physics identities must be valid and unique.");
            }
        }

        private static int CompareEntityUid(PhysicsEntity2D first, PhysicsEntity2D second) =>
            first.QueryInfo.UidSnapshot.CompareTo(second.QueryInfo.UidSnapshot);
    }
}
