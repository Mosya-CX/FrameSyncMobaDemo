using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class LocalAddressablesConfigurator
    {
        [MenuItem("FrameSyncMoba/Addressables/Configure Local-Only Groups")]
        public static void Configure()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true,
                    true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Addressables settings could not be created or loaded.");
            }

            settings.BuildRemoteCatalog = false;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.BundleLocalCatalog = false;
            settings.OptimizeCatalogSize = true;
            settings.NonRecursiveBuilding = true;
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            ProjectConfigData.GenerateBuildLayout = false;

            AddressableAssetGroup firstClientGroup = null;
            for (int i = 0;
                 i < AddressablesProjectConstants.ClientGroups.Length;
                 i++)
            {
                AddressableAssetGroup group = EnsureLocalGroup(
                    settings,
                    AddressablesProjectConstants.ClientGroups[i]);
                if (firstClientGroup == null)
                    firstClientGroup = group;
            }

            if (firstClientGroup != null)
                settings.DefaultGroup = firstClientGroup;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AddressablesConfig] Local-only Addressables groups configured; remote catalog and startup catalog updates are disabled.");
        }

        public static AddressableAssetGroup EnsureLocalGroup(
            AddressableAssetSettings settings,
            string groupName)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name is required.", nameof(groupName));

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            BundledAssetGroupSchema bundled =
                group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
                bundled = group.AddSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(
                settings,
                AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(
                settings,
                AddressableAssetSettings.kLocalLoadPath);
            bundled.IncludeInBuild = true;
            bundled.UseAssetBundleCache = false;
            bundled.BundleMode =
                BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            ContentUpdateGroupSchema update =
                group.GetSchema<ContentUpdateGroupSchema>();
            if (update == null)
                update = group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = true;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(bundled);
            EditorUtility.SetDirty(update);
            return group;
        }
    }
}
