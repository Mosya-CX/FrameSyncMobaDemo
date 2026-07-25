using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor menu for baking and validating MinionWaveConfig assets.
    /// </summary>
    public static class MinionWaveBakeMenuItem
    {
        private const string ConfigAssetPath = "Assets/Settings/MinionWaveConfig.asset";
        private const string MenuPath = "Tools/FrameSync/Bake Minion Wave Config";

        [MenuItem(MenuPath)]
        public static void BakeMinionWaveConfig()
        {
            // Find or create the config asset
            var config = AssetDatabase.LoadAssetAtPath<MinionWaveConfig>(ConfigAssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                // Ensure Settings folder exists
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                    AssetDatabase.CreateFolder("Assets", "Settings");

                config = ScriptableObject.CreateInstance<MinionWaveConfig>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
            }

            // Validate structural integrity
            bool valid = MinionWaveConfigValidator.Validate(config);
            if (valid)
            {
                Debug.Log(
                    $"[MinionWaveBake] {(isNew ? "Created" : "Updated")} MinionWaveConfig " +
                    $"with {config.WaveCount} wave entries. Validation passed.");
            }
            else
            {
                Debug.LogWarning("[MinionWaveBake] Validation failed. See console for details.");
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();


            Selection.activeObject = config;
            Debug.Log($"[MinionWaveBake] Done. Config at: {ConfigAssetPath}");
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateBakeMinionWaveConfig() => !EditorApplication.isPlaying;


    }
}
