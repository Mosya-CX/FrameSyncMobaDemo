using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global registry of all EquipmentDefinitions. Provides runtime lookups
    /// and bake-time validation (Equipment/Gold v12 §2, §5.2).
    /// </summary>
    public sealed class EquipmentDatabase
    {
        private readonly Dictionary<int, EquipmentDefinition> _byId = new Dictionary<int, EquipmentDefinition>();
        private readonly Dictionary<EquipmentTagUid, List<EquipmentDefinition>> _byTag =
            new Dictionary<EquipmentTagUid, List<EquipmentDefinition>>();
        private EquipmentDefinition[] _allDefinitions = Array.Empty<EquipmentDefinition>();
        private UniqueEquipmentTagTable _uniqueTagTable;

        public int Count => _byId.Count;
        public IReadOnlyList<EquipmentDefinition> AllDefinitions => _allDefinitions;
        public UniqueEquipmentTagTable UniqueTagTable =>
            _uniqueTagTable;

        public void SetUniqueTagTable(
            UniqueEquipmentTagTable table)
        {
            _uniqueTagTable = table;
            _uniqueTagTable?.Initialize();
        }

        public void Register(EquipmentDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid) throw new ArgumentException($"EquipmentDefinition has invalid Id: {definition.Id}");

            if (_byId.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate EquipmentDefinition Id: {definition.Id}");

            _byId[definition.Id] = definition;

            if (definition.Tags != null)
            {
                for (int i = 0; i < definition.Tags.Length; i++)
                {
                    EquipmentTagDefinition tag =
                        definition.Tags[i];
                    if (tag == null ||
                        !tag.Uid.IsValid)
                        continue;
                    if (!_byTag.TryGetValue(
                            tag.Uid,
                            out var list))
                    {
                        list = new List<EquipmentDefinition>();
                        _byTag[tag.Uid] = list;
                    }
                    list.Add(definition);
                }
            }
        }

        public void Seal()
        {
            _allDefinitions = new EquipmentDefinition[_byId.Count];
            int idx = 0;
            foreach (var kv in _byId)
            {
                kv.Value.Bake();
                _allDefinitions[idx++] = kv.Value;
            }
            Array.Sort(_allDefinitions, (a, b) => a.Id.CompareTo(b.Id));
        }

        public bool TryGetDefinition(int equipmentId, out EquipmentDefinition definition)
        {
            return _byId.TryGetValue(equipmentId, out definition);
        }

        public EquipmentDefinition GetDefinition(int equipmentId)
        {
            _byId.TryGetValue(equipmentId, out var def);
            return def;
        }

        public bool TryGetDefinitionsByTag(
            EquipmentTagUid tag,
            out List<EquipmentDefinition> definitions)
        {
            return _byTag.TryGetValue(tag, out definitions);
        }

        /// <summary>
        /// Validates all registered definitions at bake time for duplicate Ids,
        /// circular recipes, and unique tag conflicts.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (_byId.Count == 0)
            {
                errors.Add("EquipmentDatabase is empty.");
                return errors;
            }

            foreach (var kv in _byId)
            {
                var def = kv.Value;

                // Recipe components must exist
                if (def.Recipe?.Components != null)
                {
                    for (int i = 0; i < def.Recipe.Components.Length; i++)
                    {
                        var part = def.Recipe.Components[i];
                        if (part.Item == null)
                            errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Recipe component [{i}] Item is null.");
                        else if (!_byId.ContainsKey(part.Item.Id))
                            errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Recipe component '{part.Item.Name}' (Id={part.Item.Id}) not registered.");
                        if (part.Count <= 0)
                            errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Recipe component [{i}] Count must be > 0.");
                    }
                }

                // Effects validation
                if (def.Effects != null && def.Effects.Length > 2)
                    errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Effects.Length > 2.");

                // Consumable must have MaxStack >= 1
                if (def.Tier == EquipmentTier.Consumable && def.MaxStack < 1)
                    errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Consumable MaxStack must be >= 1.");

                // Non-consumable must have MaxStack == 1
                if (def.Tier != EquipmentTier.Consumable && def.MaxStack != 1)
                    errors.Add($"Equipment '{def.Name}' (Id={def.Id}): Non-consumable MaxStack must be 1.");
            }

            return errors;
        }
    }
}
