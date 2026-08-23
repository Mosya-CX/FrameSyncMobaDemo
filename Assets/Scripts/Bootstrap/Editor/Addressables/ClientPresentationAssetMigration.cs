using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FrameSyncMoba.Bootstrap;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class ClientPresentationAssetMigration
    {
        private const string UiRoot = "Assets/ClientContent/UI";
        private const string ReportPath =
            "Docs/Implementation/Addressables/CLIENT_PRESENTATION_MIGRATION.md";

        private static readonly (string Source, string Destination)[]
            PresentationFolders =
            {
                ("Assets/Resources/Animation", "Assets/ClientContent/Animation"),
                ("Assets/Resources/Material", "Assets/ClientContent/Materials"),
                ("Assets/Resources/Prefab/Indicators", "Assets/ClientContent/Indicators"),
                ("Assets/Resources/Prefab/UI", UiRoot),
                ("Assets/Config/Formal/Animation", "Assets/ClientContent/Animation/Profiles"),
            };

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/ClientBootstrap.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/Tests/ClientFrameworkSmoke.unity",
            "Assets/Scenes/Tests/HeroTestScene.unity",
        };

        [MenuItem("FrameSyncMoba/Addressables/Migrate Shared Presentation and UI")]
        public static void Migrate()
        {
            var report = new StringBuilder();
            report.AppendLine("# Shared presentation and UI migration");
            report.AppendLine();
            report.AppendLine("All moves preserve asset GUIDs. Animation, material and indicator assets are bundled as dependencies of Addressable presentation roots.");
            report.AppendLine();
            report.AppendLine("## Folder moves");
            report.AppendLine();
            report.AppendLine("| Source | Destination |");
            report.AppendLine("|---|---|");
            for (int i = 0; i < PresentationFolders.Length; i++)
            {
                MoveFolderIfNeeded(
                    PresentationFolders[i].Source,
                    PresentationFolders[i].Destination);
                report.AppendLine(
                    $"| `{PresentationFolders[i].Source}` | `{PresentationFolders[i].Destination}` |");
            }
            MoveAssetIfNeeded(
                "Assets/Resources/MiniMap.renderTexture",
                "Assets/ClientContent/UI/MiniMap.renderTexture");
            report.AppendLine(
                "| `Assets/Resources/MiniMap.renderTexture` | `Assets/ClientContent/UI/MiniMap.renderTexture` |");
            MoveAssetIfNeeded(
                "Assets/Config/Formal/UnitOutlineRim.mat",
                "Assets/ClientContent/Materials/UnitOutlineRim.mat");
            report.AppendLine(
                "| `Assets/Config/Formal/UnitOutlineRim.mat` | `Assets/ClientContent/Materials/UnitOutlineRim.mat` |");

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "Addressables settings are required.");
            AddressableAssetGroup uiGroup =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.UiGroup);
            AddressableAssetGroup sharedGroup =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.SharedGroup);
            var registered = new Dictionary<UIPageId, string>();

            string managerPath = $"{UiRoot}/UIManager.prefab";
            GameObject managerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(managerPath);
            if (managerPrefab == null)
                throw new InvalidOperationException(
                    $"UIManager prefab is missing at '{managerPath}'.");
            UIManager manager = managerPrefab.GetComponent<UIManager>();
            if (manager == null)
                throw new InvalidOperationException(
                    "UIManager prefab has no UIManager component.");
            MigrateManager(manager, settings, uiGroup, registered);
            settings.RemoveAssetEntry(
                AssetDatabase.AssetPathToGUID(managerPath),
                false);
            RegisterRoot(
                settings, sharedGroup,
                "Assets/ClientContent/Indicators/DirectionIndicator.prefab",
                "ui/indicator/direction", "client-indicator");
            RegisterRoot(
                settings, sharedGroup,
                "Assets/ClientContent/Indicators/RangeCircleIndicator.prefab",
                "ui/indicator/range-circle", "client-indicator");
            RegisterRoot(
                settings, sharedGroup,
                "Assets/ClientContent/Indicators/GroundTargetIndicator.prefab",
                "ui/indicator/ground-target", "client-indicator");
            EditorUtility.SetDirty(managerPrefab);

            for (int i = 0; i < ScenePaths.Length; i++)
                MigrateSceneManagers(
                    ScenePaths[i], settings, uiGroup, registered);
            MigrateGameScenePresentationShell();

            report.AppendLine();
            report.AppendLine("Additional roots: `ui/indicator/direction`, `ui/indicator/range-circle`, `ui/indicator/ground-target`. UIManager is a lightweight scene-resident composition shell; its seven page prefabs are Addressable roots.");
            report.AppendLine();
            report.AppendLine("## UI page roots");
            report.AppendLine();
            report.AppendLine("| Page | Address | Asset |");
            report.AppendLine("|---|---|---|");
            var ids = new List<UIPageId>(registered.Keys);
            ids.Sort((left, right) => ((int)left).CompareTo((int)right));
            for (int i = 0; i < ids.Count; i++)
            {
                string address = registered[ids[i]];
                report.AppendLine(
                    $"| {ids[i]} | `{address}` | `{FindAddressPath(settings, address)}` |");
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(
                ReportPath,
                report.ToString(),
                new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(
                $"[AddressablesMigration] Shared presentation folders moved and {registered.Count} UI pages registered.");
        }

        private static void MigrateGameScenePresentationShell()
        {
            const string path = "Assets/Scenes/GameScene.unity";
            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Map" &&
                        root.GetComponentInChildren<Renderer>(true) != null &&
                        root.GetComponentInChildren<
                            FrameSyncMoba.Unit.FlowFieldSceneAuthoring>(true) ==
                        null)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                        changed = true;
                        continue;
                    }
                    FrameSyncMoba.PlayerInput.SkillIndicatorDriver[] drivers =
                        root.GetComponentsInChildren<
                            FrameSyncMoba.PlayerInput.SkillIndicatorDriver>(true);
                    for (int i = 0; i < drivers.Length; i++)
                    {
                        var serialized = new SerializedObject(drivers[i]);
                        serialized.FindProperty("directionIndicatorPrefab")
                            .objectReferenceValue = null;
                        serialized.FindProperty("rangeCirclePrefab")
                            .objectReferenceValue = null;
                        serialized.FindProperty("groundTargetPrefab")
                            .objectReferenceValue = null;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
                if (changed)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RegisterRoot(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string path,
            string address,
            string label)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new InvalidOperationException(
                    $"Client presentation root '{path}' is missing.");
            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry =
                settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(address, false);
            entry.SetLabel(label, true, false, false);
        }

        private static void MigrateSceneManagers(
            string scenePath,
            AddressableAssetSettings settings,
            AddressableAssetGroup uiGroup,
            IDictionary<UIPageId, string> registered)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                return;
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UIManager[] managers =
                        root.GetComponentsInChildren<UIManager>(true);
                    for (int i = 0; i < managers.Length; i++)
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(managers[i]))
                            continue;
                        changed |= MigrateManager(
                            managers[i], settings, uiGroup, registered);
                    }
                }
                if (changed)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool MigrateManager(
            UIManager manager,
            AddressableAssetSettings settings,
            AddressableAssetGroup uiGroup,
            IDictionary<UIPageId, string> registered)
        {
            var serialized = new SerializedObject(manager);
            SerializedProperty pages = serialized.FindProperty("pages");
            bool changed = false;
            for (int i = 0; i < pages.arraySize; i++)
            {
                SerializedProperty page = pages.GetArrayElementAtIndex(i);
                UIPageId pageId = (UIPageId)
                    page.FindPropertyRelative("PageId").intValue;
                SerializedProperty prefabProperty =
                    page.FindPropertyRelative("Prefab");
                SerializedProperty addressProperty =
                    page.FindPropertyRelative("Address");
                string address = $"ui/page/{pageId.ToString().ToLowerInvariant()}";
                string path = prefabProperty.objectReferenceValue != null
                    ? AssetDatabase.GetAssetPath(
                        prefabProperty.objectReferenceValue)
                    : FindAddressPath(settings, addressProperty.stringValue);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException(
                        $"UI page {pageId} has neither a prefab nor an address.");
                string guid = AssetDatabase.AssetPathToGUID(path);
                AddressableAssetEntry entry =
                    settings.CreateOrMoveEntry(guid, uiGroup, false, false);
                entry.SetAddress(address, false);
                entry.SetLabel("client-ui", true, false, false);
                prefabProperty.objectReferenceValue = null;
                addressProperty.stringValue = address;
                registered[pageId] = address;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }
            return changed;
        }

        private static void MoveFolderIfNeeded(
            string source,
            string destination)
        {
            if (!AssetDatabase.IsValidFolder(source))
            {
                if (!AssetDatabase.IsValidFolder(destination))
                    throw new InvalidOperationException(
                        $"Neither source '{source}' nor destination '{destination}' exists.");
                return;
            }
            EnsureParentFolder(destination);
            if (AssetDatabase.IsValidFolder(destination))
                throw new InvalidOperationException(
                    $"Destination '{destination}' already exists while source '{source}' also exists.");
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(
                    $"Could not move '{source}' to '{destination}': {error}");
        }

        private static void MoveAssetIfNeeded(
            string source,
            string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(destination) == null)
                    throw new InvalidOperationException(
                        $"Neither source '{source}' nor destination '{destination}' exists.");
                return;
            }
            EnsureParentFolder(destination);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(
                    $"Could not move '{source}' to '{destination}': {error}");
        }

        private static void EnsureParentFolder(string path)
        {
            string parent = Path.GetDirectoryName(path)
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

        private static string FindAddressPath(
            AddressableAssetSettings settings,
            string address)
        {
            if (string.IsNullOrEmpty(address))
                return string.Empty;
            for (int i = 0; i < settings.groups.Count; i++)
            {
                AddressableAssetGroup group = settings.groups[i];
                if (group == null)
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                    if (entry.address == address)
                        return entry.AssetPath;
            }
            return string.Empty;
        }
    }
}
