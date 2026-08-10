using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor bake + validation for CrowdControlDefinition assets
    /// (CC v6.2 2.6). Bake compiles authoring fields into the hidden runtime
    /// fields of the same asset; uniqueness is validated across the catalog.
    /// </summary>
    public static class CrowdControlDefinitionBaker
    {
        [MenuItem("MOBA/Bake Crowd Control Catalog")]
        public static void BakeSelectedCatalog()
        {
            CrowdControlCatalogAsset catalog =
                Selection.activeObject as
                    CrowdControlCatalogAsset;
            if (catalog == null)
            {
                Debug.LogError(
                    "Select a CrowdControlCatalogAsset before baking.");
                return;
            }
            BakeCatalog(catalog);
        }

        public static bool BakeCatalog(
            CrowdControlCatalogAsset catalog)
        {
            if (catalog == null ||
                catalog.Definitions == null)
            {
                return true;
            }
            var seenIds =
                new HashSet<CrowdControlId>();
            bool dirty = false;
            for (int i = 0;
                 i < catalog.Definitions.Length;
                 i++)
            {
                CrowdControlDefinition definition =
                    catalog.Definitions[i];
                if (definition == null)
                {
                    continue;
                }
                definition.Bake();
                if (!seenIds.Add(
                        definition.ControlId))
                {
                    Debug.LogError(
                        $"CrowdControl catalog '{catalog.name}' contains duplicate ControlId {definition.ControlId.Value}.",
                        catalog);
                    return false;
                }
                EditorUtility.SetDirty(definition);
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            return true;
        }
    }
}
