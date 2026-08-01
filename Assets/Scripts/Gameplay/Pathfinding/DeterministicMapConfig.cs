using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct DeterministicMapObstacleAuthoring
    {
        [Min(1)] public int StableObstacleId;
        public Vector2 Center;
        public Vector2 Size;
        [Tooltip("Unity Y-axis rotation projected from world X/Z into Gameplay X/Y, in degrees.")]
        public float RotationDegrees;
        public RadiusClass MinimumBlockedRadiusClass;
    }

    [Serializable]
    public struct DeterministicSpawnPointAuthoring
    {
        [Min(0)] public int SpawnPointId;
        public Vector2 Position;
        public Vector2 Forward;
        [Min(0)] public int TeamId;
    }

    public readonly struct BakedMapObstacle
    {
        public readonly int StableObstacleId;
        public readonly fp2 Center;
        public readonly fp2 AxisX;
        public readonly fp2 AxisY;
        public readonly fp2 HalfExtents;
        public readonly fp2 Minimum;
        public readonly fp2 Maximum;
        public readonly RadiusClass MinimumBlockedRadiusClass;

        public BakedMapObstacle(
            int stableObstacleId,
            fp2 center,
            fp2 axisX,
            fp2 axisY,
            fp2 halfExtents,
            RadiusClass minimumBlockedRadiusClass)
        {
            StableObstacleId = stableObstacleId;
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            HalfExtents = halfExtents;
            fp2 aabbHalfExtents =
                new fp2(
                    fpmath.abs(axisX.x) *
                        halfExtents.x +
                    fpmath.abs(axisY.x) *
                        halfExtents.y,
                    fpmath.abs(axisX.y) *
                        halfExtents.x +
                    fpmath.abs(axisY.y) *
                        halfExtents.y);
            Minimum = center -
                aabbHalfExtents;
            Maximum = center +
                aabbHalfExtents;
            MinimumBlockedRadiusClass =
                minimumBlockedRadiusClass;
        }
    }

    public readonly struct BakedSpawnPoint
    {
        public readonly int SpawnPointId;
        public readonly fp2 Position;
        public readonly fp2 Forward;
        public readonly TeamId TeamId;

        public BakedSpawnPoint(
            int spawnPointId,
            fp2 position,
            fp2 forward,
            TeamId teamId)
        {
            SpawnPointId = spawnPointId;
            Position = position;
            Forward = forward;
            TeamId = teamId;
        }
    }

    public sealed class BakedDeterministicMapData
    {
        private readonly BakedMapObstacle[] obstacles;
        private readonly BakedSpawnPoint[] spawnPoints;

        public int MapConfigId { get; }
        public uint MapDataVersion { get; }
        public fp2 WorldMinimum { get; }
        public fp2 WorldMaximum { get; }
        public fp CellSize { get; }
        public IReadOnlyList<BakedMapObstacle> Obstacles =>
            obstacles;
        public IReadOnlyList<BakedSpawnPoint> SpawnPoints =>
            spawnPoints;

        public BakedDeterministicMapData(
            int mapConfigId,
            uint mapDataVersion,
            fp2 worldMinimum,
            fp2 worldMaximum,
            fp cellSize,
            BakedMapObstacle[] obstacles,
            BakedSpawnPoint[] spawnPoints)
        {
            MapConfigId = mapConfigId;
            MapDataVersion = mapDataVersion;
            WorldMinimum = worldMinimum;
            WorldMaximum = worldMaximum;
            CellSize = cellSize;
            this.obstacles =
                obstacles ?? Array.Empty<BakedMapObstacle>();
            this.spawnPoints =
                spawnPoints ?? Array.Empty<BakedSpawnPoint>();
        }

        public PathGridMap2D CreatePathGrid()
        {
            var grid = new PathGridMap2D();
            grid.Initialise(
                WorldMinimum,
                WorldMaximum,
                CellSize);
            for (int i = 0;
                 i < obstacles.Length;
                 i++)
            {
                BakedMapObstacle obstacle =
                    obstacles[i];
                grid.SetOrientedRectObstruction(
                    obstacle.Center,
                    obstacle.AxisX,
                    obstacle.AxisY,
                    obstacle.HalfExtents,
                    true,
                    obstacle.MinimumBlockedRadiusClass);
            }
            return grid;
        }

        public BakedSpawnPoint GetRequiredSpawnPoint(
            int spawnPointId)
        {
            int low = 0;
            int high = spawnPoints.Length;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (spawnPoints[middle].SpawnPointId <
                    spawnPointId)
                    low = middle + 1;
                else
                    high = middle;
            }
            if (low >= spawnPoints.Length ||
                spawnPoints[low].SpawnPointId !=
                    spawnPointId)
                throw new InvalidOperationException(
                    $"Map {MapConfigId} has no SpawnPointId {spawnPointId}.");
            return spawnPoints[low];
        }
    }

    [CreateAssetMenu(
        fileName = "DeterministicMapConfig",
        menuName = "FrameSyncMoba/Map/Deterministic Map Config")]
    public sealed class DeterministicMapConfig :
        ScriptableObject
    {
        [SerializeField, Min(1)] private int mapConfigId = 1;
        [SerializeField, Min(1)] private uint mapDataVersion = 1;
        [SerializeField] private Vector2 worldMinimum =
            new Vector2(-20f, -12f);
        [SerializeField] private Vector2 worldMaximum =
            new Vector2(20f, 12f);
        [SerializeField, Min(0.05f)] private float cellSize = 0.5f;
        [SerializeField] private List<DeterministicMapObstacleAuthoring>
            obstacles =
                new List<DeterministicMapObstacleAuthoring>();
        [SerializeField] private List<DeterministicSpawnPointAuthoring>
            spawnPoints =
                new List<DeterministicSpawnPointAuthoring>();

        public int MapConfigId => mapConfigId;
        public uint MapDataVersion => mapDataVersion;

        public BakedDeterministicMapData BakeOrThrow()
        {
            if (mapConfigId <= 0 || mapDataVersion == 0)
                throw new InvalidOperationException(
                    "MapConfigId and MapDataVersion must be positive.");
            ValidateFinite(worldMinimum, nameof(worldMinimum));
            ValidateFinite(worldMaximum, nameof(worldMaximum));
            ValidateFinite(cellSize, nameof(cellSize));
            if (worldMaximum.x <= worldMinimum.x ||
                worldMaximum.y <= worldMinimum.y ||
                cellSize <= 0f)
                throw new InvalidOperationException(
                    "Map bounds and CellSize are invalid.");

            var sortedObstacles =
                new List<DeterministicMapObstacleAuthoring>(
                    obstacles ??
                    new List<DeterministicMapObstacleAuthoring>());
            sortedObstacles.Sort((left, right) =>
                left.StableObstacleId.CompareTo(
                    right.StableObstacleId));
            var bakedObstacles =
                new BakedMapObstacle[sortedObstacles.Count];
            for (int i = 0;
                 i < sortedObstacles.Count;
                 i++)
            {
                DeterministicMapObstacleAuthoring obstacle =
                    sortedObstacles[i];
                if (obstacle.StableObstacleId <= 0 ||
                    (i > 0 &&
                     sortedObstacles[i - 1]
                         .StableObstacleId ==
                     obstacle.StableObstacleId))
                    throw new InvalidOperationException(
                        "Map obstacle IDs must be positive and unique.");
                ValidateFinite(
                    obstacle.Center,
                    $"Obstacle {obstacle.StableObstacleId} Center");
                ValidateFinite(
                    obstacle.Size,
                    $"Obstacle {obstacle.StableObstacleId} Size");
                ValidateFinite(
                    obstacle.RotationDegrees,
                    $"Obstacle {obstacle.StableObstacleId} RotationDegrees");
                if (obstacle.Size.x <= 0f ||
                    obstacle.Size.y <= 0f ||
                    obstacle.MinimumBlockedRadiusClass <
                        RadiusClass.Small ||
                    obstacle.MinimumBlockedRadiusClass >
                        RadiusClass.Large)
                    throw new InvalidOperationException(
                        $"Obstacle {obstacle.StableObstacleId} is invalid.");
                Vector2 half =
                    obstacle.Size * 0.5f;
                float radians =
                    obstacle.RotationDegrees *
                    Mathf.Deg2Rad;
                Vector2 axisX =
                    new Vector2(
                        Mathf.Cos(radians),
                        -Mathf.Sin(radians));
                Vector2 axisY =
                    new Vector2(
                        -axisX.y,
                        axisX.x);
                Vector2 aabbHalf =
                    new Vector2(
                        Mathf.Abs(axisX.x) * half.x +
                        Mathf.Abs(axisY.x) * half.y,
                        Mathf.Abs(axisX.y) * half.x +
                        Mathf.Abs(axisY.y) * half.y);
                Vector2 minimum =
                    obstacle.Center - aabbHalf;
                Vector2 maximum =
                    obstacle.Center + aabbHalf;
                if (minimum.x < worldMinimum.x ||
                    minimum.y < worldMinimum.y ||
                    maximum.x > worldMaximum.x ||
                    maximum.y > worldMaximum.y)
                    throw new InvalidOperationException(
                        $"Obstacle {obstacle.StableObstacleId} is outside map bounds.");
                bakedObstacles[i] =
                    new BakedMapObstacle(
                        obstacle.StableObstacleId,
                        ToFp2(obstacle.Center),
                        ToFp2(axisX),
                        ToFp2(axisY),
                        ToFp2(half),
                        obstacle.MinimumBlockedRadiusClass);
            }

            var sortedSpawns =
                new List<DeterministicSpawnPointAuthoring>(
                    spawnPoints ??
                    new List<DeterministicSpawnPointAuthoring>());
            sortedSpawns.Sort((left, right) =>
                left.SpawnPointId.CompareTo(
                    right.SpawnPointId));
            var bakedSpawns =
                new BakedSpawnPoint[sortedSpawns.Count];
            for (int i = 0;
                 i < sortedSpawns.Count;
                 i++)
            {
                DeterministicSpawnPointAuthoring spawn =
                    sortedSpawns[i];
                if (spawn.SpawnPointId < 0 ||
                    (i > 0 &&
                     sortedSpawns[i - 1].SpawnPointId ==
                     spawn.SpawnPointId) ||
                    spawn.TeamId < 0 ||
                    spawn.TeamId > byte.MaxValue)
                    throw new InvalidOperationException(
                        "Map spawn IDs and TeamIds are invalid.");
                ValidateFinite(
                    spawn.Position,
                    $"SpawnPoint {spawn.SpawnPointId} Position");
                ValidateFinite(
                    spawn.Forward,
                    $"SpawnPoint {spawn.SpawnPointId} Forward");
                if (spawn.Position.x < worldMinimum.x ||
                    spawn.Position.y < worldMinimum.y ||
                    spawn.Position.x > worldMaximum.x ||
                    spawn.Position.y > worldMaximum.y ||
                    spawn.Forward.sqrMagnitude <= 0f)
                    throw new InvalidOperationException(
                        $"SpawnPoint {spawn.SpawnPointId} is invalid.");
                fp2 forward =
                    fpmath.normalize(ToFp2(spawn.Forward));
                bakedSpawns[i] =
                    new BakedSpawnPoint(
                        spawn.SpawnPointId,
                        ToFp2(spawn.Position),
                        forward,
                        new TeamId((byte)spawn.TeamId));
            }

            var result =
                new BakedDeterministicMapData(
                    mapConfigId,
                    mapDataVersion,
                    ToFp2(worldMinimum),
                    ToFp2(worldMaximum),
                    (fp)cellSize,
                    bakedObstacles,
                    bakedSpawns);
            PathGridMap2D grid =
                result.CreatePathGrid();
            for (int i = 0;
                 i < bakedSpawns.Length;
                 i++)
                if (!grid.IsCircleWalkable(
                        bakedSpawns[i].Position,
                        (fp)0.5m))
                    throw new InvalidOperationException(
                        $"SpawnPoint {bakedSpawns[i].SpawnPointId} overlaps blocked map data.");
            return result;
        }

        private static fp2 ToFp2(Vector2 value) =>
            new fp2(
                (fp)value.x,
                (fp)value.y);

        private static void ValidateFinite(
            Vector2 value,
            string label)
        {
            ValidateFinite(value.x, label);
            ValidateFinite(value.y, label);
        }

        private static void ValidateFinite(
            float value,
            string label)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value))
                throw new InvalidOperationException(
                    $"{label} must be finite.");
        }

        private void OnValidate()
        {
            try
            {
                BakeOrThrow();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Invalid DeterministicMapConfig '{name}': {exception.Message}",
                    this);
            }
        }
    }
}
