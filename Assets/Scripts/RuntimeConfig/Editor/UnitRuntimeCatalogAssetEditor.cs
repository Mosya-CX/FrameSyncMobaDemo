using FrameSyncMoba.Unit;
using UnityEditor;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Keeps editor-only hero presentation rows synchronized without making
    /// the runtime Unit assembly reference UnityEditor.
    /// </summary>
    [CustomEditor(typeof(UnitRuntimeCatalogAsset))]
    public sealed class UnitRuntimeCatalogAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (!changed)
                return;

            var catalog = (UnitRuntimeCatalogAsset)target;
            if (catalog.HeroDisplayTableForSync != null)
            {
                HeroDisplayTableSync.Sync(
                    catalog.HeroDisplayTableForSync,
                    catalog);
            }
        }
    }
}
