using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring list of CrowdControlDefinition assets registered into the
    /// runtime registry at match start (CC v6.2 2.1).
    /// </summary>
    [CreateAssetMenu(
        fileName = "CrowdControlCatalog",
        menuName = "MOBA/Crowd Control Catalog")]
    public sealed class CrowdControlCatalogAsset :
        ScriptableObject
    {
        public CrowdControlDefinition[] Definitions;

        public void RegisterAll(
            CrowdControlDefinitionRegistry registry)
        {
            if (registry == null ||
                Definitions == null)
            {
                return;
            }
            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                CrowdControlDefinition definition =
                    Definitions[i];
                if (definition == null)
                {
                    continue;
                }
                registry.Register(definition);
            }
        }
    }
}
