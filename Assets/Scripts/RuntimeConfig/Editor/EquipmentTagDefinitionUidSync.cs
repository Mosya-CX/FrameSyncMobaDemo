using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEditor;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Assigns deterministic equipment-tag UIDs from asset GUIDs while
    /// keeping AssetDatabase ownership in an Editor-only assembly.
    /// </summary>
    [InitializeOnLoad]
    internal static class EquipmentTagDefinitionUidSync
    {
        private static readonly HashSet<string> PendingPaths =
            new HashSet<string>();
        private static bool isScheduled;

        static EquipmentTagDefinitionUidSync()
        {
            EditorApplication.delayCall += SyncAllInvalidAssets;
        }

        internal static void Schedule(IEnumerable<string> paths)
        {
            if (paths != null)
            {
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path) &&
                        path.EndsWith(".asset"))
                    {
                        PendingPaths.Add(path);
                    }
                }
            }

            if (isScheduled || PendingPaths.Count == 0)
                return;
            isScheduled = true;
            EditorApplication.delayCall += SyncPendingAssets;
        }

        private static void SyncAllInvalidAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            string[] guids = AssetDatabase.FindAssets(
                "t:EquipmentTagDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                EnsureUid(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
        }

        private static void SyncPendingAssets()
        {
            isScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                PendingPaths.Clear();
                return;
            }

            string[] paths = new string[PendingPaths.Count];
            PendingPaths.CopyTo(paths);
            PendingPaths.Clear();
            for (int i = 0; i < paths.Length; i++)
                EnsureUid(paths[i]);
        }

        private static void EnsureUid(string path)
        {
            EquipmentTagDefinition tag =
                AssetDatabase.LoadAssetAtPath<EquipmentTagDefinition>(path);
            if (tag == null || tag.Uid.IsValid)
                return;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return;

            tag.AssignUidForEditor(StableHash32(guid));
            EditorUtility.SetDirty(tag);
            AssetDatabase.SaveAssetIfDirty(tag);
        }

        private static int StableHash32(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }

    internal sealed class EquipmentTagDefinitionPostprocessor :
        AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            EquipmentTagDefinitionUidSync.Schedule(importedAssets);
            EquipmentTagDefinitionUidSync.Schedule(movedAssets);
        }
    }
}
