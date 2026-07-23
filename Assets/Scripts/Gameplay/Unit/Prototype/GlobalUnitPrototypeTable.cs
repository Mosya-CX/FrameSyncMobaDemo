using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global lookup table for UnitPrototype by UnitPrototypeId (Unit v27.3 §1.6).
    /// Loaded at match start and read-only thereafter. Validates prototypes
    /// against the StatDefinitionTable at load time.
    /// </summary>
    public sealed class GlobalUnitPrototypeTable
    {
        private readonly Dictionary<int, UnitPrototype> prototypes = new Dictionary<int, UnitPrototype>();

        /// <summary>
        /// Adds a prototype to the table. Throws on duplicate UnitPrototypeId.
        /// </summary>
        public void Add(UnitPrototype prototype)
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            if (prototypes.ContainsKey(prototype.UnitPrototypeId))
            {
                throw new ArgumentException(
                    $"Duplicate UnitPrototypeId {prototype.UnitPrototypeId}.",
                    nameof(prototype));
            }

            prototypes[prototype.UnitPrototypeId] = prototype;
        }

        /// <summary>
        /// Looks up a prototype by UnitPrototypeId.
        /// </summary>
        public bool TryGet(int prototypeId, out UnitPrototype prototype)
        {
            return prototypes.TryGetValue(prototypeId, out prototype);
        }

        /// <summary>
        /// Validates all prototypes against a StatDefinitionTable (§1.6/§5.2.3).
        /// Checks that all StatPreset StatIds exist in the table and that no
        /// StatPreset has duplicate StatIds.
        /// Throws on any validation failure.
        /// </summary>
        public void ValidateAll(StatDefinitionTable statDefinitionTable)
        {
            if (statDefinitionTable == null)
            {
                throw new ArgumentNullException(nameof(statDefinitionTable));
            }

            foreach (var kvp in prototypes)
            {
                UnitPrototype prototype = kvp.Value;

                if (prototype.BaseStats == null)
                {
                    continue;
                }

                var seenStatIds = new HashSet<StatId>();

                for (int i = 0; i < prototype.BaseStats.Stats.Count; i++)
                {
                    StatPresetEntry entry = prototype.BaseStats.Stats[i];
                    StatId statId = entry.StatId;

                    if (seenStatIds.Contains(statId))
                    {
                        throw new InvalidOperationException(
                            $"UnitPrototype {prototype.UnitPrototypeId} has duplicate StatId {statId} in its BaseStats.");
                    }
                    seenStatIds.Add(statId);

                    if (!statDefinitionTable.TryGet(statId, out StatDefinition def))
                    {
                        throw new InvalidOperationException(
                            $"UnitPrototype {prototype.UnitPrototypeId} references StatId {statId} not in StatDefinitionTable.");
                    }

                    if (!def.SupportsLevelGrowth && entry.GrowthValue != default)
                    {
                        throw new InvalidOperationException(
                            $"UnitPrototype {prototype.UnitPrototypeId}: StatId {statId} does not support level growth but has non-zero GrowthValue.");
                    }
                }
            }
        }

        /// <summary>
        /// Number of registered prototypes.
        /// </summary>
        public int Count => prototypes.Count;
    }
}