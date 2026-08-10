using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring list of BuffDefinition assets registered into the runtime
    /// BuffDefinitionRegistry at match start (design v14.2 section 3).
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuffCatalog",
        menuName = "MOBA/Buff Catalog")]
    public sealed class BuffCatalogAsset : ScriptableObject
    {
        public BuffDefinition[] Definitions;

        public void RegisterAll(
            BuffDefinitionRegistry registry)
        {
            if (registry == null) return;
            if (Definitions == null) return;
            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                BuffDefinition definition =
                    Definitions[i];
                if (definition == null)
                    continue;
                registry.Register(definition);
            }
        }
    }
}
