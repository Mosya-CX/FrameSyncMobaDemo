using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Single source for the project's configured TickRate when editor tools
    /// need to Bake. Reads GlobalGameplayData so offline Bake tools never
    /// hardcode a rate; runtime Bake uses the same configured value.
    /// </summary>
    public static class RuntimeConfigBakeContext
    {
        private const string GlobalGameplayDataPath =
            "Assets/Config/Formal/GlobalGameplayData.asset";
        private const int FallbackTickRate = 30;

        public static int CurrentTickRate
        {
            get
            {
                Object asset =
                    AssetDatabase.LoadAssetAtPath<Object>(
                        GlobalGameplayDataPath);
                if (asset == null)
                {
                    Debug.LogWarning(
                        "[RuntimeConfigBakeContext] GlobalGameplayData " +
                        $"missing; falling back to {FallbackTickRate} tps.");
                    return FallbackTickRate;
                }
                var serialized =
                    new SerializedObject(asset);
                SerializedProperty tickRate =
                    serialized.FindProperty(
                        "frameSync.TickRate");
                if (tickRate == null)
                {
                    Debug.LogWarning(
                        "[RuntimeConfigBakeContext] frameSync.TickRate " +
                        $"not found; falling back to {FallbackTickRate} tps.");
                    return FallbackTickRate;
                }
                return tickRate.intValue;
            }
        }
    }
}
