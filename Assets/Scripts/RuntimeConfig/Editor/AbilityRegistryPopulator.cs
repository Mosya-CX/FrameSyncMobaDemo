using System;
using FrameSyncMoba.Unit;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Automatically populates AbilityDefinitionRegistry from all
    /// AbilityAsset instances in the project at Editor time.
    /// Runs on domain reload (InitializeOnLoad) and when assets
    /// are imported/moved/deleted (AssetPostprocessor).
    ///
    /// Design: moba_ability_system_design_v15_2 section 5
    /// </summary>
    [InitializeOnLoad]
    public static class AbilityRegistryPopulator
    {
        private const string MenuItemPath = "FrameSyncMoba/Bake All Ability Assets";

        static AbilityRegistryPopulator()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    ValidateAllAssets();
            };
        }

        [MenuItem(MenuItemPath)]
        public static void BakeAllAbilityAssets()
        {
            var assets = FindAllAbilityAssets();
            int successCount = 0;
            int failureCount = 0;

            foreach (var asset in assets)
            {
                var result = AbilityAssetBakeValidator.Validate(asset);
                if (result.IsValid)
                {
                    try
                    {
                        asset.Bake(
                            RuntimeConfigBakeContext
                                .CurrentTickRate);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"AbilityAsset '{asset.name}': Bake failed - {ex.Message}", asset);
                        failureCount++;
                    }
                }
                else
                {
                    foreach (var error in result.Errors)
                        Debug.LogError($"AbilityAsset '{asset.name}': {error}", asset);
                    failureCount++;
                }
            }

            Debug.Log(
                $"Ability Registry Bake complete: {successCount} succeeded, {failureCount} failed.");
        }

        [MenuItem(MenuItemPath, validate = true)]
        public static bool ValidateBakeAll() => !EditorApplication.isPlaying;

        public static void ValidateAllAssets()
        {
            var assets = FindAllAbilityAssets();
            foreach (var asset in assets)
            {
                var result = AbilityAssetBakeValidator.Validate(asset);
                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                        Debug.LogWarning($"AbilityAsset '{asset.name}': {error}", asset);
                }
            }
        }

        public static int PopulateRegistry(AbilityDefinitionRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            var assets = FindAllAbilityAssets();
            int count = 0;

            foreach (var asset in assets)
            {
                var result = AbilityAssetBakeValidator.Validate(asset);
                if (!result.IsValid) continue;

                try
                {
                    var def = asset.Bake(
                        RuntimeConfigBakeContext
                            .CurrentTickRate);
                    registry.Register(def);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"AbilityAsset '{asset.name}': Registration failed - {ex.Message}", asset);
                }
            }

            return count;
        }

        public static AbilityAsset[] FindAllAbilityAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:AbilityAsset");
            var assets = new AbilityAsset[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<AbilityAsset>(path);
            }
            return assets;
        }
    }

    public sealed class AbilityAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool hasAbilityChanges = false;

            foreach (string path in importedAssets)
            {
                if (path.EndsWith(".asset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>(path);
                    if (asset != null)
                    {
                        var result = AbilityAssetBakeValidator.Validate(asset);
                        if (!result.IsValid)
                        {
                            foreach (var error in result.Errors)
                                Debug.LogWarning($"AbilityAsset '{asset.name}': {error}", asset);
                        }
                        hasAbilityChanges = true;
                    }
                }
            }

            foreach (string path in movedAssets)
            {
                if (path.EndsWith(".asset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<AbilityAsset>(path);
                    if (asset != null) hasAbilityChanges = true;
                }
            }

            if (hasAbilityChanges)
            {
                var allAssets = AbilityRegistryPopulator.FindAllAbilityAssets();
                Debug.Log($"Ability assets changed. {allAssets.Length} total in project. " +
                    "Use FrameSyncMoba/Bake All Ability Assets to regenerate registry.");
            }
        }
    }
}
