using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring ScriptableObject for the full equipment catalog. It owns the
    /// EquipmentDefinition assets and the UniqueEquipmentTagTable, and bakes
    /// them into the runtime EquipmentDatabase used by the shop.
    /// (design v12 2.10, 5.2: the shop reads EquipmentDatabase.Definitions)
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentCatalog",
        menuName = "MOBA/Equipment Catalog")]
    public sealed class EquipmentCatalogAsset :
        ScriptableObject
    {
        public EquipmentDefinition[] Definitions;
        public UniqueEquipmentTagTable UniqueTags;

        public EquipmentDatabase BakeOrThrow(
            int tickRate = 30)
        {
            var database = new EquipmentDatabase();
            if (UniqueTags != null)
                database.SetUniqueTagTable(
                    UniqueTags);
            if (Definitions != null)
            {
                for (int i = 0;
                     i < Definitions.Length;
                     i++)
                {
                    EquipmentDefinition definition =
                        Definitions[i];
                    if (definition == null)
                        throw new System.InvalidOperationException(
                            $"Equipment catalog slot {i} is null.");
                    database.Register(definition);
                }
            }
            database.Seal(tickRate);

            List<string> errors =
                database.Validate();
            if (errors.Count > 0)
                throw new System.InvalidOperationException(
                    $"Equipment catalog bake failed: {string.Join("; ", errors)}");
            return database;
        }
    }
}
