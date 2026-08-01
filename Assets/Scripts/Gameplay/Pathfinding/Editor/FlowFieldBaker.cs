using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Editor
{
    public static class FlowFieldBaker
    {
        private const string DefaultOutputPath =
            "Assets/Config/FullMatchTest/FlowFields";

        [MenuItem(
            "Tools/Pathfinding/Bake Selected Flow Fields")]
        public static void BakeSelected()
        {
            GameObject selected =
                Selection.activeGameObject;
            FlowFieldSceneAuthoring authoring =
                selected != null
                    ? selected.GetComponentInParent<
                        FlowFieldSceneAuthoring>()
                    : null;
            if (authoring == null)
            {
                throw new InvalidOperationException(
                    "Select a GameObject with FlowFieldSceneAuthoring.");
            }
            Bake(
                authoring,
                DefaultOutputPath);
        }

        public static FlowFieldBakeAsset[] Bake(
            FlowFieldSceneAuthoring authoring,
            string outputPath)
        {
            if (authoring == null)
                throw new ArgumentNullException(
                    nameof(authoring));
            if (authoring.MapConfig == null)
                throw new InvalidOperationException(
                    "FlowFieldSceneAuthoring requires a map config.");
            LaneAuthoring[] authoredLanes =
                authoring.Lanes;
            if (authoredLanes.Length == 0)
                throw new InvalidOperationException(
                    "FlowFieldSceneAuthoring requires at least one lane.");

            EnsureFolder(outputPath);
            BakedDeterministicMapData map =
                authoring.MapConfig.BakeOrThrow();
            PathGridMap2D grid =
                map.CreatePathGrid();
            var service =
                new TeamFlowFieldService(grid);
            var lanes =
                new LaneRuntimeData[
                    authoredLanes.Length];
            for (int i = 0;
                 i < authoredLanes.Length;
                 i++)
            {
                if (authoredLanes[i] == null)
                    throw new InvalidOperationException(
                        $"Lane entry {i} is missing.");
                lanes[i] =
                    authoredLanes[i]
                        .BakeOrThrow();
            }
            Array.Sort(
                lanes,
                (left, right) =>
                    left.LaneId.CompareTo(
                        right.LaneId));

            var teamIds = new List<byte>();
            for (int laneIndex = 0;
                 laneIndex < lanes.Length;
                 laneIndex++)
            {
                LaneTeamSpawnData[] spawns =
                    lanes[laneIndex].TeamSpawns;
                for (int spawnIndex = 0;
                     spawnIndex < spawns.Length;
                     spawnIndex++)
                {
                    byte teamId =
                        spawns[spawnIndex]
                            .TeamId.Value;
                    if (!teamIds.Contains(teamId))
                        teamIds.Add(teamId);
                }
            }
            teamIds.Sort();
            var generated =
                new List<FlowFieldBakeAsset>(
                    teamIds.Count * 3);
            for (int teamIndex = 0;
                 teamIndex < teamIds.Count;
                 teamIndex++)
            {
                byte teamId =
                    teamIds[teamIndex];
                for (RadiusClass radiusClass =
                         RadiusClass.Small;
                     radiusClass <=
                         RadiusClass.Large;
                     radiusClass++)
                {
                    int[][] laneCosts =
                        new int[lanes.Length][];
                    var laneConfigs =
                        new LaneTargetConfig[
                            lanes.Length];
                    for (int laneIndex = 0;
                         laneIndex < lanes.Length;
                         laneIndex++)
                    {
                        if (!lanes[laneIndex]
                            .TryGetAdvanceTarget(
                                new TeamId(teamId),
                                out var target))
                        {
                            throw new InvalidOperationException(
                                $"Lane {lanes[laneIndex].LaneId} has no advance target for Team {teamId}.");
                        }
                        laneConfigs[laneIndex] =
                            new LaneTargetConfig
                            {
                                    LaneIndex =
                                        checked(
                                            (byte)laneIndex),
                                    Targets =
                                        new[] { target },
                                    GuidePoints =
                                        SnapGuidePoints(
                                            grid,
                                            lanes[laneIndex]
                                                .CenterlinePoints),
                                    GuideHalfWidth =
                                        lanes[laneIndex]
                                            .CorridorHalfWidth,
                                    GuideCostPerCell =
                                        authoring
                                            .GuideCostPerCell,
                                    OffGuideCostPerCell =
                                        authoring
                                            .OffGuideCostPerCell,
                            };
                        laneCosts[laneIndex] =
                            service.BuildLaneCostField(
                                laneConfigs[laneIndex],
                                radiusClass);
                    }
                    TeamFlowFieldData field =
                        service.BuildTeamFlowField(
                            teamId,
                            radiusClass,
                            laneCosts,
                            laneConfigs,
                            FlowFieldBuildConfig.Default);
                    if (!field.IsValid)
                        throw new InvalidOperationException(
                            $"Failed to bake Team {teamId}, Radius {radiusClass}.");
                    string assetPath =
                        $"{outputPath}/FlowField_Team{teamId}_{radiusClass}.asset";
                    FlowFieldBakeAsset asset =
                        AssetDatabase.LoadAssetAtPath<
                            FlowFieldBakeAsset>(
                            assetPath);
                    if (asset == null)
                    {
                        asset =
                            ScriptableObject
                                .CreateInstance<
                                    FlowFieldBakeAsset>();
                        AssetDatabase.CreateAsset(
                            asset,
                            assetPath);
                    }
                    asset.Key =
                        new FlowFieldKey(
                            teamId,
                            radiusClass);
                    asset.Field = field;
                    EditorUtility.SetDirty(asset);
                    generated.Add(asset);
                }
            }
            var serialized =
                new SerializedObject(authoring);
            SerializedProperty fieldsProperty =
                serialized.FindProperty(
                    "bakedFields");
            fieldsProperty.arraySize =
                generated.Count;
            for (int i = 0;
                 i < generated.Count;
                 i++)
            {
                fieldsProperty
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue =
                    generated[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Baked {generated.Count} flow fields from {lanes.Length} lanes.",
                authoring);
            return generated.ToArray();
        }

        private static fp2[] SnapGuidePoints(
            PathGridMap2D grid,
            fp2[] points)
        {
            if (points == null)
                return Array.Empty<fp2>();
            var result = new fp2[points.Length];
            for (int i = 0;
                 i < points.Length;
                 i++)
            {
                var cell =
                    grid.WorldToCell(points[i]);
                result[i] =
                    grid.CellToWorld(
                        cell.cx,
                        cell.cy);
            }
            return result;
        }

        private static void EnsureFolder(
            string assetPath)
        {
            string[] segments =
                assetPath.Split('/');
            string current = segments[0];
            for (int i = 1;
                 i < segments.Length;
                 i++)
            {
                string next =
                    $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        segments[i]);
                current = next;
            }
            if (!AssetDatabase.IsValidFolder(
                    assetPath))
            {
                throw new IOException(
                    $"Could not create {assetPath}.");
            }
        }
    }
}
