using System;
using System.IO;
using System.Text;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class MapPrefabAddressablesMigration
    {
        public const int MapPrefabId = 5001;
        public const string MapViewAddress = "view/map/main";
        private const string SourcePath = "Assets/Resources/Prefab/Map.prefab";
        private const string LogicPath =
            "Assets/Config/Formal/Prefabs/Logic/Map/Map.prefab";
        private const string ViewPath =
            "Assets/ClientContent/Views/Map/MapView.prefab";
        private const string ArchivePath =
            "Assets/Archive/LegacyMonolithicMapPrefab/Map.prefab";
        private const string TablePath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";
        private const string ReportPath =
            "Docs/Implementation/Addressables/MAP_PREFAB_MIGRATION.md";

        [MenuItem("FrameSyncMoba/Addressables/Migrate Map Prefab")]
        public static void Migrate()
        {
            EnsureParent(LogicPath);
            EnsureParent(ViewPath);
            EnsureParent(ArchivePath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) != null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ArchivePath) == null &&
                    !AssetDatabase.CopyAsset(SourcePath, ArchivePath))
                    throw new InvalidOperationException(
                        "Could not create the monolithic map archive copy.");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath) == null &&
                    !AssetDatabase.CopyAsset(SourcePath, ViewPath))
                    throw new InvalidOperationException(
                        "Could not create the map view copy.");
                string error = AssetDatabase.MoveAsset(SourcePath, LogicPath);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(
                        $"Could not move map logic prefab: {error}");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LogicPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath) == null)
                throw new InvalidOperationException(
                    "Both formal map logic and client map view prefabs are required.");

            StripLogicPrefab();
            StripViewPrefab();
            RegisterAddressableView();
            UpdatePrefabTable();
            WriteReport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AddressablesMigration] Map split into formal logic and Addressable client view prefabs.");
        }

        private static void StripLogicPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LogicPath);
            try
            {
                DestroyComponents<Renderer>(root);
                DestroyComponents<MeshFilter>(root);
                DestroyComponents<Collider>(root);
                PrefabUtility.SaveAsPrefabAsset(root, LogicPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StripViewPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ViewPath);
            try
            {
                DestroyComponents<FlowFieldSceneAuthoring>(root);
                DestroyComponents<FlowFieldVisualizer>(root);
                DestroyComponents<LaneAuthoring>(root);
                DestroyComponents<Collider>(root);
                PrefabUtility.SaveAsPrefabAsset(root, ViewPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DestroyComponents<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = components.Length - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(components[i], true);
        }

        private static void RegisterAddressableView()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.SharedGroup);
            string guid = AssetDatabase.AssetPathToGUID(ViewPath);
            AddressableAssetEntry entry =
                settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(MapViewAddress, false);
            entry.SetLabel("client-map", true, false, false);
            EditorUtility.SetDirty(settings);
        }

        private static void UpdatePrefabTable()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            var serialized = new SerializedObject(table);
            SerializedProperty groups = serialized.FindProperty("prefabGroups");
            SerializedProperty miscEntries = null;
            for (int i = 0; i < groups.arraySize; i++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(i);
                if (group.FindPropertyRelative("kind").intValue ==
                    (int)PrefabKind.Misc)
                {
                    miscEntries = group.FindPropertyRelative("entries");
                    break;
                }
            }
            if (miscEntries == null)
            {
                int groupIndex = groups.arraySize;
                groups.InsertArrayElementAtIndex(groupIndex);
                SerializedProperty group =
                    groups.GetArrayElementAtIndex(groupIndex);
                group.FindPropertyRelative("kind").intValue =
                    (int)PrefabKind.Misc;
                miscEntries = group.FindPropertyRelative("entries");
                miscEntries.ClearArray();
            }
            SerializedProperty target = null;
            for (int i = 0; i < miscEntries.arraySize; i++)
            {
                SerializedProperty candidate =
                    miscEntries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("prefabId").intValue ==
                    MapPrefabId)
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null)
            {
                int index = miscEntries.arraySize;
                miscEntries.InsertArrayElementAtIndex(index);
                target = miscEntries.GetArrayElementAtIndex(index);
            }
            GameObject logic = AssetDatabase.LoadAssetAtPath<GameObject>(LogicPath);
            target.FindPropertyRelative("prefabId").intValue = MapPrefabId;
            target.FindPropertyRelative("unityPrefab").objectReferenceValue = logic;
            target.FindPropertyRelative("gameplayConfigId").intValue = 0;
            target.FindPropertyRelative("editorAssetGuid").stringValue =
                AssetDatabase.AssetPathToGUID(LogicPath);
            target.FindPropertyRelative("clientViewAddress").stringValue =
                MapViewAddress;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            table.ValidateOrThrow();
            EditorUtility.SetDirty(table);
        }

        private static void WriteReport()
        {
            var report = new StringBuilder();
            report.AppendLine("# Map prefab Addressables migration");
            report.AppendLine();
            report.AppendLine("| Role | Asset | GUID/address |");
            report.AppendLine("|---|---|---|");
            report.AppendLine(
                $"| Formal deterministic authoring | `{LogicPath}` | `{AssetDatabase.AssetPathToGUID(LogicPath)}` |");
            report.AppendLine(
                $"| Client view | `{ViewPath}` | `{MapViewAddress}` |");
            report.AppendLine(
                $"| Historical monolithic copy | `{ArchivePath}` | `{AssetDatabase.AssetPathToGUID(ArchivePath)}` |");
            report.AppendLine();
            report.AppendLine("The logic prefab retains FlowFieldSceneAuthoring/LaneAuthoring and no Renderer, MeshFilter, Collider or material dependency. The client view contains render data and no deterministic map authoring components.");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(
                ReportPath,
                report.ToString(),
                new UTF8Encoding(false));
        }

        private static void EnsureParent(string assetPath)
        {
            string parent = Path.GetDirectoryName(assetPath)
                ?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
                return;
            string[] parts = parent.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
