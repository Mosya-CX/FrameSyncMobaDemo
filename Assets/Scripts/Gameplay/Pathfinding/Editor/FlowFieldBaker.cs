using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.Unit.Editor
{
    /// <summary>
    /// Editor tool for offline FlowField construction.
    /// Builds TeamFlowFieldData for each team + radius class combination
    /// and serializes to FlowFieldBakeAsset ScriptableObjects.
    /// (Pathfinding Design v13.1 section 8.9)
    /// </summary>
    public static class FlowFieldBaker
    {
        private const string DefaultOutputPath = "Assets/Resources/FlowFields";

        [MenuItem("Tools/Pathfinding/Bake All Flow Fields")]
        public static void BakeAll()
        {
            if (!AssetDatabase.IsValidFolder(DefaultOutputPath))
            {
                EnsureFolderExists("Assets/Resources", "FlowFields");
            }

            byte[] teamIds = { 0, 1 };
            RadiusClass[] radiusClasses = { RadiusClass.Small, RadiusClass.Medium, RadiusClass.Large };

            fp2 worldMin = fp2.zero;
            fp2 worldMax = new fp2((fp)31m, (fp)31m);
            var grid = new PathGridMap2D();
            grid.Initialise(worldMin, worldMax, (fp)1m);

            var service = new TeamFlowFieldService(grid);

            // Example lane configs: two lanes going east
            var laneConfigs = new LaneTargetConfig[]
            {
                new LaneTargetConfig
                {
                    LaneIndex = 0,
                    Targets = new fp2[] { new fp2((fp)30m, (fp)10m) },
                },
                new LaneTargetConfig
                {
                    LaneIndex = 1,
                    Targets = new fp2[] { new fp2((fp)30m, (fp)20m) },
                },
            };

            foreach (byte teamId in teamIds)
            {
                foreach (RadiusClass rc in radiusClasses)
                {
                    // Build per-lane cost fields
                    int[][] laneCostFields = new int[laneConfigs.Length][];
                    for (int i = 0; i < laneConfigs.Length; i++)
                    {
                        laneCostFields[i] = service.BuildLaneCostField(laneConfigs[i], rc);
                    }

                    var field = service.BuildTeamFlowField(
                        teamId, rc, laneCostFields, FlowFieldBuildConfig.Default);

                    if (!field.IsValid)
                    {
                        Debug.LogWarning($"[FlowFieldBaker] Built field is invalid for team {teamId}, rc {rc}");
                        continue;
                    }

                    var asset = ScriptableObject.CreateInstance<FlowFieldBakeAsset>();
                    asset.Key = new FlowFieldKey(teamId, rc);
                    asset.Field = field;

                    string assetPath = $"{DefaultOutputPath}/FlowField_Team{teamId}_{rc}.asset";
                    AssetDatabase.CreateAsset(asset, assetPath);
                    Debug.Log($"[FlowFieldBaker] Baked: {assetPath} (Team {teamId}, {rc})");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FlowFieldBaker] Bake All Flow Fields complete.");
        }

        [MenuItem("Tools/Pathfinding/Bake Flow Field (Selected Team)")]
        public static void BakeSelectedTeam()
        {
            Debug.Log("[FlowFieldBaker] Bake Selected Team: run 'Bake All' or configure teamId manually.");
            BakeAll();
        }

        private static void EnsureFolderExists(string parent, string folder)
        {
            string path = $"{parent}/{folder}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
