using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor menu item for baking and validating JungleCampConfig assets.
    ///
    /// Design: moba_non_hero_unit_modules_design_v5.md section 4
    /// Follows the pattern established by MinionWaveBakeMenuItem (ExecPlan 0086).
    /// </summary>
    public static class JungleCampBakeMenuItem
    {
        [MenuItem("Tools/FrameSync/Bake Jungle Camp Config")]
        public static void BakeJungleCampConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:JungleCampConfig");
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[JungleCampBake] No JungleCampConfig assets found in project.");
                return;
            }

            int baked = 0;
            int failed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<JungleCampConfig>(path);
                if (config == null) continue;

                if (JungleCampConfigValidator.ValidateAndLog(config))
                {
                    EditorUtility.SetDirty(config);
                    baked++;
                }
                else
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[JungleCampBake] Complete: {baked} valid, {failed} errors.");
        }

        [MenuItem("Tools/FrameSync/Validate Jungle Camp Configs")]
        public static void ValidateJungleCampConfigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:JungleCampConfig");
            if (guids == null || guids.Length == 0)
            {
                Debug.Log("[JungleCampValidate] No JungleCampConfig assets found.");
                return;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<JungleCampConfig>(path);
                JungleCampConfigValidator.ValidateAndLog(config);
            }
        }
    }
}
