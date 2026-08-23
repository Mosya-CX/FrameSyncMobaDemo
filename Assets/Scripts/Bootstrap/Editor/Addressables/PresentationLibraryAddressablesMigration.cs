using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FrameSyncMoba.RuntimeConfig;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class PresentationLibraryAddressablesMigration
    {
        private const string VfxLibraryPath =
            "Assets/Config/Formal/FullMatchVfxLibrary.asset";
        private const string AudioLibraryPath =
            "Assets/Config/Formal/AudioLibrary.asset";
        private const string PrefabTablePath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";
        private const string VfxRoot = "Assets/ClientContent/VFX";
        private const string AudioRoot = "Assets/ClientContent/Audio";
        private const string ReportPath =
            "Docs/Implementation/Addressables/PRESENTATION_LIBRARY_MIGRATION.md";

        [MenuItem("FrameSyncMoba/Addressables/Migrate VFX and Audio Libraries")]
        public static void Migrate()
        {
            EnsureFolder(VfxRoot);
            EnsureFolder(AudioRoot);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("Addressables settings are required.");
            AddressableAssetGroup vfxGroup =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.VfxGroup);
            AddressableAssetGroup audioGroup =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.AudioGroup);

            var vfxAddresses = new Dictionary<int, string>();
            var report = new StringBuilder();
            report.AppendLine("# VFX and audio Addressables migration");
            report.AppendLine();
            report.AppendLine("| Kind | Definition ID | Asset | Address | GUID |");
            report.AppendLine("|---|---:|---|---|---|");
            MigrateVfx(settings, vfxGroup, vfxAddresses, report);
            MigrateAudio(settings, audioGroup, report);
            UpdatePresentationPrefabEntries(vfxAddresses);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static void MigrateVfx(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            IDictionary<int, string> addresses,
            StringBuilder report)
        {
            ScriptableObject library =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(VfxLibraryPath);
            var serialized = new SerializedObject(library);
            SerializedProperty entries = serialized.FindProperty("_entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                int id = entry.FindPropertyRelative("VfxDefId").intValue;
                SerializedProperty prefabProperty =
                    entry.FindPropertyRelative("Prefab");
                SerializedProperty addressProperty =
                    entry.FindPropertyRelative("Address");
                GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
                string path = prefab != null
                    ? AssetDatabase.GetAssetPath(prefab)
                    : ResolveAddressPath(settings, addressProperty.stringValue);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException($"VFX {id} has neither prefab nor address.");
                path = MoveIfNeeded(path, VfxRoot);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string address = $"vfx/{id}";
                AddressableAssetEntry addressable =
                    settings.CreateOrMoveEntry(guid, group, false, false);
                addressable.SetAddress(address, false);
                addressable.SetLabel("client-vfx", true, false, false);
                prefabProperty.objectReferenceValue = null;
                addressProperty.stringValue = address;
                addresses[id] = address;
                report.AppendLine($"| VFX | {id} | `{path}` | `{address}` | `{guid}` |");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }

        private static void MigrateAudio(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            StringBuilder report)
        {
            ScriptableObject library =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(AudioLibraryPath);
            var serialized = new SerializedObject(library);
            SerializedProperty entries = serialized.FindProperty("_entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                int id = entry.FindPropertyRelative("SfxDefId").intValue;
                SerializedProperty clipProperty = entry.FindPropertyRelative("Clip");
                SerializedProperty addressProperty = entry.FindPropertyRelative("Address");
                AudioClip clip = clipProperty.objectReferenceValue as AudioClip;
                string path = clip != null
                    ? AssetDatabase.GetAssetPath(clip)
                    : ResolveAddressPath(settings, addressProperty.stringValue);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException($"SFX {id} has neither clip nor address.");
                path = MoveIfNeeded(path, AudioRoot);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string address = $"audio/{id}";
                AddressableAssetEntry addressable =
                    settings.CreateOrMoveEntry(guid, group, false, false);
                addressable.SetAddress(address, false);
                addressable.SetLabel("client-audio", true, false, false);
                clipProperty.objectReferenceValue = null;
                addressProperty.stringValue = address;
                report.AppendLine($"| Audio | {id} | `{path}` | `{address}` | `{guid}` |");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }

        private static void UpdatePresentationPrefabEntries(
            IReadOnlyDictionary<int, string> addresses)
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(PrefabTablePath);
            var serialized = new SerializedObject(table);
            SerializedProperty groups = serialized.FindProperty("prefabGroups");
            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(gi);
                if (group.FindPropertyRelative("kind").intValue !=
                    (int)PrefabKind.ParticleVfx)
                    continue;
                SerializedProperty entries = group.FindPropertyRelative("entries");
                for (int ei = 0; ei < entries.arraySize; ei++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(ei);
                    int id = entry.FindPropertyRelative("prefabId").intValue;
                    if (!addresses.TryGetValue(id, out string address))
                        continue;
                    entry.FindPropertyRelative("unityPrefab").objectReferenceValue = null;
                    entry.FindPropertyRelative("clientViewAddress").stringValue = address;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            table.ValidateOrThrow();
            EditorUtility.SetDirty(table);
        }

        private static string MoveIfNeeded(string sourcePath, string destinationRoot)
        {
            if (sourcePath.StartsWith(destinationRoot + "/", StringComparison.Ordinal))
                return sourcePath;
            string destination = $"{destinationRoot}/{Path.GetFileName(sourcePath)}";
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
                return destination;
            string error = AssetDatabase.MoveAsset(sourcePath, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(
                    $"Could not move '{sourcePath}' to '{destination}': {error}");
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            return destination;
        }

        private static string ResolveAddressPath(
            AddressableAssetSettings settings,
            string address)
        {
            if (string.IsNullOrEmpty(address))
                return string.Empty;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null) continue;
                foreach (AddressableAssetEntry entry in group.entries)
                    if (entry.address == address)
                        return entry.AssetPath;
            }
            return string.Empty;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
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
