using System;
using System.Collections.Generic;
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
    public static class GameplayIconAddressablesMigration
    {
        private const string ReportPath =
            "Docs/Implementation/Addressables/GAMEPLAY_ICON_MIGRATION.md";

        private static readonly Type[] RootTypes =
        {
            typeof(AbilityAsset),
            typeof(FixedPassiveDefinitionAsset),
            typeof(EquipmentDefinition),
            typeof(BuffDefinition),
            typeof(HeroDisplayTable),
        };

        [MenuItem("FrameSyncMoba/Addressables/Migrate Gameplay Icon References")]
        public static void Migrate()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "Addressables settings are required.");
            AddressableAssetGroup group =
                LocalAddressablesConfigurator.EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.UiGroup);
            var report = new StringBuilder();
            report.AppendLine("# Gameplay presentation icon migration");
            report.AppendLine();
            report.AppendLine("Gameplay configuration now carries stable client addresses, never direct Sprite references. This table records every migrated serialized edge.");
            report.AppendLine();
            report.AppendLine("| Config asset | Serialized address field | Sprite asset | Address | GUID |");
            report.AppendLine("|---|---|---|---|---|");

            int migrated = 0;
            for (int typeIndex = 0; typeIndex < RootTypes.Length; typeIndex++)
            {
                string[] guids = AssetDatabase.FindAssets(
                    $"t:{RootTypes[typeIndex].Name}",
                    new[] { "Assets/Config/Formal" });
                Array.Sort(guids, StringComparer.Ordinal);
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    UnityEngine.Object asset =
                        AssetDatabase.LoadMainAssetAtPath(path);
                    migrated += MigrateAsset(
                        asset, path, settings, group, report);
                }
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
                $"[AddressablesMigration] Migrated {migrated} direct Gameplay Sprite references to stable addresses.");
        }

        private static int MigrateAsset(
            UnityEngine.Object asset,
            string assetPath,
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            StringBuilder report)
        {
            if (asset == null)
                return 0;
            var serialized = new SerializedObject(asset);
            var spriteProperties = new List<string>();
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType ==
                        SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue is Sprite &&
                    TryGetAddressPropertyPath(
                        iterator.propertyPath,
                        iterator.name,
                        out _))
                {
                    spriteProperties.Add(iterator.propertyPath);
                }
            }

            int migrated = 0;
            for (int i = 0; i < spriteProperties.Count; i++)
            {
                SerializedProperty spriteProperty =
                    serialized.FindProperty(spriteProperties[i]);
                if (!(spriteProperty?.objectReferenceValue is Sprite sprite) ||
                    !TryGetAddressPropertyPath(
                        spriteProperty.propertyPath,
                        spriteProperty.name,
                        out string addressPath))
                    continue;
                SerializedProperty addressProperty =
                    serialized.FindProperty(addressPath);
                if (addressProperty == null ||
                    addressProperty.propertyType !=
                        SerializedPropertyType.String)
                {
                    throw new InvalidOperationException(
                        $"No string address field '{addressPath}' exists for '{assetPath}:{spriteProperty.propertyPath}'.");
                }
                string spritePath = AssetDatabase.GetAssetPath(sprite);
                string guid = AssetDatabase.AssetPathToGUID(spritePath);
                string address = $"ui/icon/{guid}";
                AddressableAssetEntry entry =
                    settings.CreateOrMoveEntry(guid, group, false, false);
                entry.SetAddress(address, false);
                entry.SetLabel("client-ui-icon", true, false, false);
                addressProperty.stringValue = address;
                spriteProperty.objectReferenceValue = null;
                migrated++;
            }
            if (migrated > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
            serialized.UpdateIfRequiredOrScript();
            AppendAddressRows(
                serialized,
                assetPath,
                settings,
                report);
            return migrated;
        }

        private static void AppendAddressRows(
            SerializedObject serialized,
            string assetPath,
            AddressableAssetSettings settings,
            StringBuilder report)
        {
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.String ||
                    !IsAddressPropertyName(iterator.name) ||
                    string.IsNullOrWhiteSpace(iterator.stringValue))
                    continue;
                string address = iterator.stringValue;
                AddressableAssetEntry entry = FindAddressEntry(
                    settings,
                    address);
                if (entry == null)
                    throw new InvalidOperationException(
                        $"'{assetPath}:{iterator.propertyPath}' references unknown address '{address}'.");
                report.AppendLine(
                    $"| `{assetPath}` | `{iterator.propertyPath}` | `{entry.AssetPath}` | `{address}` | `{entry.guid}` |");
            }
        }

        private static bool IsAddressPropertyName(string propertyName) =>
            propertyName == "iconAddress" ||
            propertyName == "IconAddress" ||
            propertyName == "AvatarAddress" ||
            propertyName.EndsWith(
                "IconAddressOverride",
                StringComparison.Ordinal);

        private static AddressableAssetEntry FindAddressEntry(
            AddressableAssetSettings settings,
            string address)
        {
            for (int groupIndex = 0;
                 groupIndex < settings.groups.Count;
                 groupIndex++)
            {
                AddressableAssetGroup group = settings.groups[groupIndex];
                if (group == null)
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                    if (entry.address == address)
                        return entry;
            }
            return null;
        }

        private static bool TryGetAddressPropertyPath(
            string propertyPath,
            string propertyName,
            out string addressPath)
        {
            string replacement;
            if (propertyName == "icon")
                replacement = "iconAddress";
            else if (propertyName == "iconOverride")
                replacement = "iconAddressOverride";
            else if (propertyName == "Icon")
                replacement = "IconAddress";
            else if (propertyName == "Avatar")
                replacement = "AvatarAddress";
            else if (propertyName.EndsWith(
                         "IconOverride",
                         StringComparison.Ordinal))
            {
                replacement = propertyName.Substring(
                    0,
                    propertyName.Length - "IconOverride".Length) +
                    "IconAddressOverride";
            }
            else
            {
                addressPath = null;
                return false;
            }

            int separator = propertyPath.LastIndexOf('.');
            addressPath = separator >= 0
                ? propertyPath.Substring(0, separator + 1) + replacement
                : replacement;
            return true;
        }
    }
}
