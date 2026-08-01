using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct LaneTeamSpawnAuthoring
    {
        [Range(0, byte.MaxValue)] public int TeamId;
        public Transform SpawnPoint;
    }

    public readonly struct LaneTeamSpawnData
    {
        public readonly TeamId TeamId;
        public readonly fp2 Position;
        public readonly fp2 Forward;

        public LaneTeamSpawnData(
            TeamId teamId,
            fp2 position,
            fp2 forward)
        {
            TeamId = teamId;
            Position = position;
            Forward = forward;
        }
    }

    public sealed class LaneRuntimeData
    {
        public ushort LaneId { get; }
        public LaneTeamSpawnData[] TeamSpawns { get; }
        public fp2[] CenterlinePoints { get; }
        public fp CorridorHalfWidth { get; }

        public LaneRuntimeData(
            ushort laneId,
            LaneTeamSpawnData[] teamSpawns,
            fp2[] centerlinePoints,
            fp corridorHalfWidth)
        {
            LaneId = laneId;
            TeamSpawns = teamSpawns ??
                Array.Empty<LaneTeamSpawnData>();
            CenterlinePoints = centerlinePoints ??
                Array.Empty<fp2>();
            CorridorHalfWidth = corridorHalfWidth;
        }

        public bool TryGetAdvanceTarget(
            TeamId teamId,
            out fp2 target)
        {
            target = default;
            if (CenterlinePoints.Length == 0)
                return false;

            for (int i = 0; i < TeamSpawns.Length; i++)
            {
                LaneTeamSpawnData spawn = TeamSpawns[i];
                if (spawn.TeamId != teamId)
                    continue;

                fp2 first =
                    CenterlinePoints[0];
                fp2 last =
                    CenterlinePoints[
                        CenterlinePoints.Length - 1];
                fp firstDistanceSq =
                    fpmath.lengthsq(
                        first - spawn.Position);
                fp lastDistanceSq =
                    fpmath.lengthsq(
                        last - spawn.Position);
                if (firstDistanceSq ==
                    lastDistanceSq)
                {
                    fp firstProgress =
                        fpmath.dot(
                            first - spawn.Position,
                            spawn.Forward);
                    fp lastProgress =
                        fpmath.dot(
                            last - spawn.Position,
                            spawn.Forward);
                    target = lastProgress >=
                        firstProgress
                            ? last
                            : first;
                }
                else
                {
                    target = firstDistanceSq >
                        lastDistanceSq
                            ? first
                            : last;
                }
                return true;
            }
            return false;
        }

        public fp2 GetNearestCenterlinePoint(
            fp2 position,
            out fp distanceSq)
        {
            if (CenterlinePoints.Length == 0)
                throw new InvalidOperationException(
                    $"Lane {LaneId} has no centerline.");

            fp2 best = CenterlinePoints[0];
            distanceSq = fpmath.lengthsq(position - best);
            for (int i = 0;
                 i < CenterlinePoints.Length - 1;
                 i++)
            {
                fp2 start = CenterlinePoints[i];
                fp2 end = CenterlinePoints[i + 1];
                fp2 segment = end - start;
                fp segmentLengthSq =
                    fpmath.lengthsq(segment);
                fp2 candidate;
                if (segmentLengthSq <= fp.zero)
                {
                    candidate = start;
                }
                else
                {
                    fp progress = fpmath.dot(
                        position - start,
                        segment) / segmentLengthSq;
                    progress = fpmath.clamp(
                        progress,
                        fp.zero,
                        fp.one);
                    candidate = start + segment * progress;
                }
                fp candidateDistanceSq =
                    fpmath.lengthsq(position - candidate);
                if (candidateDistanceSq <
                    distanceSq)
                {
                    distanceSq =
                        candidateDistanceSq;
                    best = candidate;
                }
            }
            return best;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LaneAuthoring : MonoBehaviour
    {
        [Min(1)]
        [SerializeField] private int laneId = 1;
        [SerializeField] private LaneTeamSpawnAuthoring[] teamSpawns =
            Array.Empty<LaneTeamSpawnAuthoring>();
        [SerializeField] private Transform[] centerlinePoints =
            Array.Empty<Transform>();
        [Min(0f)]
        [SerializeField] private float corridorHalfWidth = 2f;

        public ushort LaneId => checked((ushort)laneId);

        public LaneRuntimeData BakeOrThrow()
        {
            if (laneId <= 0 || laneId > ushort.MaxValue)
                throw new InvalidOperationException(
                    $"{name} LaneId must be in [1, {ushort.MaxValue}].");
            if (float.IsNaN(corridorHalfWidth) ||
                float.IsInfinity(corridorHalfWidth) ||
                corridorHalfWidth < 0f)
                throw new InvalidOperationException(
                    $"{name} CorridorHalfWidth must be finite and nonnegative.");
            if (teamSpawns == null || teamSpawns.Length == 0)
                throw new InvalidOperationException(
                    $"{name} requires at least one explicit team spawn.");
            if (centerlinePoints == null ||
                centerlinePoints.Length < 2)
                throw new InvalidOperationException(
                    $"{name} requires at least two centerline points.");

            var bakedSpawns =
                new LaneTeamSpawnData[teamSpawns.Length];
            for (int i = 0; i < teamSpawns.Length; i++)
            {
                LaneTeamSpawnAuthoring entry = teamSpawns[i];
                if (entry.TeamId <= 0 ||
                    entry.TeamId > byte.MaxValue ||
                    entry.SpawnPoint == null)
                    throw new InvalidOperationException(
                        $"{name} team spawn {i} is invalid.");
                if (i > 0 &&
                    teamSpawns[i - 1].TeamId >= entry.TeamId)
                    throw new InvalidOperationException(
                        $"{name} team spawns must be authored in strictly increasing TeamId order.");
                Vector3 position = entry.SpawnPoint.position;
                Vector3 forward = entry.SpawnPoint.forward;
                fp2 forward2D = new fp2(
                    (fp)forward.x,
                    (fp)forward.z);
                if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                        forward2D,
                        out fp2 normalizedForward,
                        out _))
                    throw new InvalidOperationException(
                        $"{name} team spawn {i} has zero planar forward.");
                bakedSpawns[i] = new LaneTeamSpawnData(
                    new TeamId((byte)entry.TeamId),
                    new fp2((fp)position.x, (fp)position.z),
                    normalizedForward);
            }

            var bakedCenterline =
                new fp2[centerlinePoints.Length];
            for (int i = 0;
                 i < centerlinePoints.Length;
                 i++)
            {
                if (centerlinePoints[i] == null)
                    throw new InvalidOperationException(
                        $"{name} centerline point {i} is missing.");
                Vector3 point = centerlinePoints[i].position;
                bakedCenterline[i] =
                    new fp2((fp)point.x, (fp)point.z);
            }

            return new LaneRuntimeData(
                (ushort)laneId,
                bakedSpawns,
                bakedCenterline,
                (fp)corridorHalfWidth);
        }
    }
}
