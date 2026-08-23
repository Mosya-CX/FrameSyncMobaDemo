using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class
        MapPathfindingAssetIntegrationTests
    {
        private GameObject mapPrefab;
        private FlowFieldSceneAuthoring source;
        private BakedDeterministicMapData mapData;
        private PathGridMap2D grid;

        [SetUp]
        public void SetUp()
        {
            mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Archive/LegacyMonolithicMapPrefab/Map.prefab");
            Assert.That(
                mapPrefab,
                Is.Not.Null,
                "The retained monolithic map bake/audit source is required.");
            source =
                mapPrefab.GetComponent<
                    FlowFieldSceneAuthoring>();
            Assert.That(source, Is.Not.Null);
            mapData =
                source.MapConfig.BakeOrThrow();
            grid = mapData.CreatePathGrid();
        }

        [Test]
        public void MapPrefab_OwnsGridLanesFieldsAndVisualizer()
        {
            Assert.That(
                source.MapConfig,
                Is.Not.Null);
            Assert.That(
                source.Lanes.Length,
                Is.EqualTo(3));
            Assert.That(
                source.BakedFields.Length,
                Is.EqualTo(6));
            for (int i = 0;
                 i < source.BakedFields.Length;
                 i++)
            {
                Assert.That(
                    source.BakedFields[i],
                    Is.Not.Null);
                Assert.That(
                    source.BakedFields[i].IsValid,
                    Is.True);
            }

            FlowFieldVisualizer visualizer =
                mapPrefab.GetComponent<
                    FlowFieldVisualizer>();
            Assert.That(visualizer, Is.Not.Null);
            Assert.That(
                visualizer.Source,
                Is.SameAs(source));
        }

        [Test]
        public void RotatedThinObstacle_DoesNotBecomeAabbSquare()
        {
            BakedMapObstacle rotated = default;
            bool found = false;
            for (int i = 0;
                 i < mapData.Obstacles.Count;
                 i++)
            {
                BakedMapObstacle candidate =
                    mapData.Obstacles[i];
                if (fpmath.abs(candidate.AxisX.x) >
                        (fp)0.1m &&
                    fpmath.abs(candidate.AxisX.y) >
                        (fp)0.1m &&
                    candidate.HalfExtents.y >
                        candidate.HalfExtents.x)
                {
                    rotated = candidate;
                    found = true;
                    break;
                }
            }
            Assert.That(
                found,
                Is.True,
                "The map fixture requires a rotated thin bar.");

            var centerCell =
                grid.WorldToCell(rotated.Center);
            Assert.That(
                grid.IsPassable(
                    centerCell.cx,
                    centerCell.cy,
                    RadiusClass.Small),
                Is.False);

            fp2 aabbOnlyProbe =
                rotated.Center +
                rotated.AxisX *
                (rotated.HalfExtents.x +
                 (fp)2m);
            Assert.That(
                aabbOnlyProbe.x >=
                    rotated.Minimum.x &&
                aabbOnlyProbe.x <=
                    rotated.Maximum.x,
                Is.True);
            Assert.That(
                aabbOnlyProbe.y >=
                    rotated.Minimum.y &&
                aabbOnlyProbe.y <=
                    rotated.Maximum.y,
                Is.True);
            var probeCell =
                grid.WorldToCell(aabbOnlyProbe);
            Assert.That(
                grid.IsPassable(
                    probeCell.cx,
                    probeCell.cy,
                    RadiusClass.Small),
                Is.True,
                "A rotated bar must not block its entire world AABB.");
        }

        [Test]
        public void BakedObstacleLongAxes_MatchMapBoxColliders()
        {
            BoxCollider[] colliders =
                mapPrefab.GetComponentsInChildren<
                    BoxCollider>(
                    true);
            int obstacleLayer =
                LayerMask.NameToLayer(
                    "Obstacle");
            for (int obstacleIndex = 0;
                 obstacleIndex <
                 mapData.Obstacles.Count;
                 obstacleIndex++)
            {
                BakedMapObstacle obstacle =
                    mapData.Obstacles[
                        obstacleIndex];
                BoxCollider matched = null;
                float bestDistanceSq =
                    float.PositiveInfinity;
                for (int colliderIndex = 0;
                     colliderIndex <
                     colliders.Length;
                     colliderIndex++)
                {
                    BoxCollider candidate =
                        colliders[colliderIndex];
                    if (!candidate.enabled ||
                        candidate.gameObject.layer !=
                            obstacleLayer)
                        continue;
                    Vector3 worldCenter =
                        candidate.transform
                            .TransformPoint(
                                candidate.center);
                    Vector2 delta =
                        new Vector2(
                            worldCenter.x -
                                (float)obstacle.Center.x,
                            worldCenter.z -
                                (float)obstacle.Center.y);
                    if (delta.sqrMagnitude <
                        bestDistanceSq)
                    {
                        bestDistanceSq =
                            delta.sqrMagnitude;
                        matched = candidate;
                    }
                }

                Assert.That(
                    matched,
                    Is.Not.Null,
                    $"Obstacle {obstacle.StableObstacleId} has no matching BoxCollider.");
                Assert.That(
                    bestDistanceSq,
                    Is.LessThan(0.0001f));
                Vector3 worldX =
                    matched.transform.TransformVector(
                        Vector3.right *
                        matched.size.x);
                Vector3 worldZ =
                    matched.transform.TransformVector(
                        Vector3.forward *
                        matched.size.z);
                Vector3 actualLong =
                    worldX.sqrMagnitude >=
                        worldZ.sqrMagnitude
                        ? worldX
                        : worldZ;
                fp2 actualAxis =
                    fpmath.normalize(
                        new fp2(
                            (fp)actualLong.x,
                            (fp)actualLong.z));
                fp2 bakedAxis =
                    obstacle.HalfExtents.x >=
                        obstacle.HalfExtents.y
                        ? obstacle.AxisX
                        : obstacle.AxisY;
                fp alignment =
                    fpmath.abs(
                        fpmath.dot(
                            actualAxis,
                            bakedAxis));
                Assert.That(
                    alignment,
                    Is.GreaterThan((fp)0.999m),
                    $"Obstacle {obstacle.StableObstacleId} baked perpendicular to {matched.name}.");
            }
        }

        [Test]
        public void MiddleLaneFlow_PullsTowardLaneProgressively()
        {
            Assert.That(
                source.TryGetField(
                    1,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset asset),
                Is.True);

            var middleCell =
                grid.WorldToCell(fp2.zero);
            AssertCellDirection(
                asset.Field,
                middleCell.cx - 1,
                middleCell.cy + 1,
                Dir8.SE);
            AssertCellDirection(
                asset.Field,
                middleCell.cx - 2,
                middleCell.cy + 2,
                Dir8.E);
            AssertCellDirection(
                asset.Field,
                middleCell.cx - 8,
                middleCell.cy + 8,
                Dir8.NE);

            AssertCellDirection(
                asset.Field,
                middleCell.cx + 1,
                middleCell.cy - 1,
                Dir8.SE);
            AssertCellDirection(
                asset.Field,
                middleCell.cx + 2,
                middleCell.cy - 2,
                Dir8.S);
            AssertCellDirection(
                asset.Field,
                middleCell.cx + 8,
                middleCell.cy - 8,
                Dir8.SW);
        }

        [Test]
        public void StraightLaneSkeletonCells_UseAuthoredTangents()
        {
            Assert.That(
                source.TryGetField(
                    1,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset blue),
                Is.True);
            Assert.That(
                source.TryGetField(
                    2,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset red),
                Is.True);

            AssertWorldDirection(
                blue.Field,
                new fp2((fp)(-38m), (fp)(-30m)),
                Dir8.S);
            AssertWorldDirection(
                blue.Field,
                new fp2((fp)(-30m), (fp)(-38m)),
                Dir8.E);
            AssertWorldDirection(
                red.Field,
                new fp2((fp)30m, (fp)38m),
                Dir8.W);
            AssertWorldDirection(
                red.Field,
                new fp2((fp)38m, (fp)30m),
                Dir8.N);
        }

        [Test]
        public void FoundationJunctions_DistinguishDepartureFromArrivalTarget()
        {
            Assert.That(
                source.TryGetField(
                    1,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset blue),
                Is.True);
            Assert.That(
                source.TryGetField(
                    2,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset red),
                Is.True);
            AssertWorldHasDirection(
                blue.Field,
                new fp2((fp)(-38m), (fp)(-38m)),
                true);
            AssertWorldHasDirection(
                blue.Field,
                new fp2((fp)38m, (fp)38m),
                false);
            AssertWorldHasDirection(
                red.Field,
                new fp2((fp)38m, (fp)38m),
                true);
            AssertWorldHasDirection(
                red.Field,
                new fp2((fp)(-38m), (fp)(-38m)),
                false);
        }

        [Test]
        public void LaneOwnershipBoundaries_HaveDirectionsAndBypassVisualizerStride()
        {
            const int visualizerStride = 4;
            int boundaryCells = 0;
            int recoveredStrideGaps = 0;
            for (int assetIndex = 0;
                 assetIndex <
                 source.BakedFields.Length;
                 assetIndex++)
            {
                TeamFlowFieldData field =
                    source.BakedFields[
                        assetIndex].Field;
                var targetCells =
                    new HashSet<int>();
                for (int laneIndex = 0;
                     laneIndex < source.Lanes.Length;
                     laneIndex++)
                {
                    LaneRuntimeData lane =
                        source.Lanes[laneIndex]
                            .BakeOrThrow();
                    if (!lane.TryGetAdvanceTarget(
                            new TeamId(
                                source.BakedFields[
                                    assetIndex]
                                    .Key.TeamId),
                            out fp2 target))
                        continue;
                    var targetCell =
                        grid.WorldToCell(target);
                    targetCells.Add(
                        targetCell.cy *
                            field.Width +
                        targetCell.cx);
                }
                for (int y = 0;
                     y < field.Height;
                     y++)
                {
                    for (int x = 0;
                         x < field.Width;
                         x++)
                    {
                        if (!FlowFieldVisualizer
                                .IsLaneBoundaryCell(
                                    field,
                                    x,
                                    y))
                            continue;
                        boundaryCells++;
                        int index =
                            y * field.Width + x;
                        if (targetCells.Contains(index))
                            continue;
                        Assert.That(
                            (Dir8)field.DirectionCode[
                                index],
                            Is.Not.EqualTo(Dir8.None),
                            $"Lane boundary {x},{y} in {source.BakedFields[assetIndex].Key} has no direction.");
                        Assert.That(
                            FlowFieldVisualizer
                                .ShouldDrawFieldCell(
                                    field,
                                    x,
                                    y,
                                    visualizerStride,
                                    FlowFieldVisualizationMode
                                        .Directions),
                            Is.True);
                        if (x % visualizerStride != 0 ||
                            y % visualizerStride != 0)
                            recoveredStrideGaps++;
                    }
                }
            }
            Assert.That(
                boundaryCells,
                Is.GreaterThan(0));
            Assert.That(
                recoveredStrideGaps,
                Is.GreaterThan(0),
                "The fixture must contain lane-boundary cells skipped by regular stride sampling.");
        }

        [Test]
        public void SixLaneDirections_FollowAuthoredWaypointsInOrder()
        {
            for (byte teamId = 1;
                 teamId <= 2;
                 teamId++)
            {
                Assert.That(
                    source.TryGetField(
                        teamId,
                        RadiusClass.Small,
                        out FlowFieldBakeAsset asset),
                    Is.True);
                for (int laneIndex = 0;
                     laneIndex < source.Lanes.Length;
                     laneIndex++)
                {
                    LaneRuntimeData lane =
                        source.Lanes[laneIndex]
                            .BakeOrThrow();
                    AssertRouteVisitsWaypoints(
                        teamId,
                        laneIndex,
                        lane,
                        asset.Field);
                }
            }
        }

        [Test]
        public void AStar_TraversesEveryAuthoredLaneSegment()
        {
            var service =
                new AStarPathService(grid);
            for (int laneIndex = 0;
                 laneIndex < source.Lanes.Length;
                 laneIndex++)
            {
                LaneRuntimeData lane =
                    source.Lanes[laneIndex]
                        .BakeOrThrow();
                for (int segment = 0;
                     segment <
                     lane.CenterlinePoints.Length - 1;
                     segment++)
                {
                    PathResult result =
                        service.FindPath(
                            lane.CenterlinePoints[
                                segment],
                            lane.CenterlinePoints[
                                segment + 1],
                            RadiusClass.Small,
                            20000);
                    Assert.That(
                        result.Success,
                        Is.True,
                        $"Lane {lane.LaneId} segment {segment} failed: {result.Status}.");
                    Assert.That(
                        result.PathCellIndices,
                        Is.Not.Empty);
                    for (int pathIndex = 0;
                         pathIndex <
                         result.PathCellIndices.Length;
                         pathIndex++)
                    {
                        int cell =
                            result.PathCellIndices[
                                pathIndex];
                        Assert.That(
                            grid.IsPassable(
                                cell % grid.Width,
                                cell / grid.Width,
                                RadiusClass.Small),
                            Is.True);
                    }
                }
            }
        }

        [Test]
        public void FlowFieldAndRvo_ProduceStableWalkableProgress()
        {
            Assert.That(
                source.TryGetField(
                    1,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset asset),
                Is.True);
            LaneRuntimeData lane =
                source.Lanes[0]
                    .BakeOrThrow();
            fp2 start =
                GetSpawn(lane, 1).Position;
            fp2 offsetStart =
                start +
                new fp2(
                    (fp)0.55m,
                    fp.zero);
            fp2 firstDirection =
                GetRequiredFlowDirection(
                    asset.Field,
                    start);
            fp2 secondDirection =
                GetRequiredFlowDirection(
                    asset.Field,
                    offsetStart);
            RVOInput[] inputs =
            {
                CreateRvoInput(
                    1,
                    start,
                    firstDirection),
                CreateRvoInput(
                    2,
                    offsetStart,
                    secondDirection),
            };
            var rvo =
                new DeterministicRVOSystem(
                    RVOConfig.Default);

            RvoResult[] first =
                rvo.Step(inputs);
            RvoResult[] second =
                rvo.Step(inputs);

            Assert.That(
                second.Length,
                Is.EqualTo(first.Length));
            for (int i = 0;
                 i < first.Length;
                 i++)
            {
                Assert.That(
                    second[i].UnitUid,
                    Is.EqualTo(first[i].UnitUid));
                Assert.That(
                    second[i].FinalVelocity,
                    Is.EqualTo(
                        first[i].FinalVelocity));
                fp2 next =
                    inputs[i].Position +
                    first[i].FinalVelocity *
                    (fp)0.1m;
                Assert.That(
                    grid.IsCircleWalkable(
                        next,
                        RadiusClassHelper
                            .SmallRadius),
                    Is.True);
            }
        }

        private void AssertRouteVisitsWaypoints(
            byte teamId,
            int laneIndex,
            LaneRuntimeData lane,
            in TeamFlowFieldData field)
        {
            LaneTeamSpawnData spawn =
                GetSpawn(lane, teamId);
            var cell =
                grid.WorldToCell(
                    spawn.Position);
            int current =
                cell.cy * field.Width +
                cell.cx;
            Assert.That(
                field.OwnerLane[current],
                Is.EqualTo(laneIndex),
                $"Team {teamId} Lane {lane.LaneId} starts in the wrong OwnerLane.");

            int waypoint = teamId == 1
                ? 0
                : lane.CenterlinePoints.Length - 1;
            int waypointStep = teamId == 1
                ? 1
                : -1;
            for (int step = 0;
                 step < field.CellCount;
                 step++)
            {
                fp2 position =
                    grid.CellToWorld(
                        current % field.Width,
                        current / field.Width);
                if (waypoint >= 0 &&
                    waypoint <
                    lane.CenterlinePoints.Length &&
                    fpmath.lengthsq(
                        position -
                        lane.CenterlinePoints[
                            waypoint]) <=
                    (fp)4m)
                {
                    waypoint += waypointStep;
                }
                int next =
                    field.NextCell[current];
                if (next < 0)
                    break;
                Assert.That(
                    next,
                    Is.Not.EqualTo(current));
                current = next;
            }
            Assert.That(
                waypoint,
                Is.EqualTo(
                    teamId == 1
                        ? lane.CenterlinePoints.Length
                        : -1),
                $"Team {teamId} Lane {lane.LaneId} did not visit every authored waypoint in order.");
            Assert.That(
                field.NextCell[current],
                Is.EqualTo(-1));
            lane.TryGetAdvanceTarget(
                new TeamId(teamId),
                out fp2 target);
            fp2 end =
                grid.CellToWorld(
                    current % field.Width,
                    current / field.Width);
            Assert.That(
                fpmath.lengthsq(end - target),
                Is.LessThanOrEqualTo(
                    fp.one));
        }

        private fp2 GetRequiredFlowDirection(
            in TeamFlowFieldData field,
            fp2 position)
        {
            var cell =
                grid.WorldToCell(position);
            byte code =
                field.DirectionCode[
                    cell.cy * field.Width +
                    cell.cx];
            Assert.That(
                code,
                Is.Not.EqualTo(
                    (byte)Dir8.None));
            return Dir8Helper.ToFP2(
                (Dir8)code);
        }

        private void AssertCellDirection(
            in TeamFlowFieldData field,
            int cellX,
            int cellY,
            Dir8 expected)
        {
            int index =
                cellY * field.Width +
                cellX;
            Assert.That(
                grid.IsPassable(
                    cellX,
                    cellY,
                    RadiusClass.Small),
                Is.True,
                $"Flow sample {cellX},{cellY} must be walkable.");
            Assert.That(
                field.OwnerLane[index],
                Is.EqualTo(1),
                $"Flow sample {cellX},{cellY} must belong to the middle lane.");
            Assert.That(
                (Dir8)field.DirectionCode[index],
                Is.EqualTo(expected),
                $"Unexpected flow direction at {cellX},{cellY}.");
        }

        private void AssertWorldDirection(
            in TeamFlowFieldData field,
            fp2 position,
            Dir8 expected)
        {
            var cell = grid.WorldToCell(position);
            int index =
                cell.cy * field.Width +
                cell.cx;
            Assert.That(
                (Dir8)field.DirectionCode[index],
                Is.EqualTo(expected),
                $"Unexpected flow direction at {position} / cell {cell.cx},{cell.cy}.");
        }

        private void AssertWorldHasDirection(
            in TeamFlowFieldData field,
            fp2 position,
            bool expected)
        {
            var cell = grid.WorldToCell(position);
            int index =
                cell.cy * field.Width +
                cell.cx;
            Assert.That(
                field.DirectionCode[index] !=
                    (byte)Dir8.None,
                Is.EqualTo(expected));
        }

        private static LaneTeamSpawnData GetSpawn(
            LaneRuntimeData lane,
            byte teamId)
        {
            for (int i = 0;
                 i < lane.TeamSpawns.Length;
                 i++)
            {
                if (lane.TeamSpawns[i]
                        .TeamId.Value ==
                    teamId)
                    return lane.TeamSpawns[i];
            }
            throw new InvalidOperationException(
                $"Lane {lane.LaneId} has no Team {teamId} spawn.");
        }

        private static RVOInput CreateRvoInput(
            byte stableIndex,
            fp2 position,
            fp2 direction)
        {
            return new RVOInput
            {
                SelfUid =
                    new UnitUid(
                        0,
                        stableIndex,
                        0),
                Position = position,
                DesiredVelocity = direction,
                Radius =
                    RadiusClassHelper
                        .SmallRadius,
                MaxSpeed = fp.one,
            };
        }
    }
}
