using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [CreateAssetMenu(
        fileName = "AbilityRuntimeCatalog",
        menuName = "FrameSyncMoba/Ability/Runtime Catalog")]
    public sealed class AbilityRuntimeCatalogAsset :
        ScriptableObject
    {
        [SerializeField] private AbilityAsset[] abilities =
            Array.Empty<AbilityAsset>();
        [SerializeField] private AbilitySlotDef[] slots =
            Array.Empty<AbilitySlotDef>();
        [SerializeField] private FixedPassiveDefinitionAsset[]
            fixedPassives = Array.Empty<FixedPassiveDefinitionAsset>();

        /// <summary>
        /// Authoring assets (presentation icon lookup). Gameplay only uses the
        /// baked <see cref="AbilityDef"/> registry.
        /// </summary>
        public IReadOnlyList<AbilityAsset> Abilities =>
            abilities;
        public IReadOnlyList<AbilitySlotDef> Slots =>
            slots;
        public IReadOnlyList<FixedPassiveDefinitionAsset> FixedPassives =>
            fixedPassives;

#if UNITY_EDITOR
        public void ConfigureForEditor(
            IEnumerable<AbilityAsset> abilityAssets,
            IEnumerable<AbilitySlotDef> slotDefinitions,
            IEnumerable<FixedPassiveDefinitionAsset> passiveAssets)
        {
            abilities = abilityAssets != null
                ? new List<AbilityAsset>(abilityAssets).ToArray()
                : Array.Empty<AbilityAsset>();
            slots = slotDefinitions != null
                ? new List<AbilitySlotDef>(slotDefinitions).ToArray()
                : Array.Empty<AbilitySlotDef>();
            fixedPassives = passiveAssets != null
                ? new List<FixedPassiveDefinitionAsset>(passiveAssets).ToArray()
                : Array.Empty<FixedPassiveDefinitionAsset>();
        }
#endif

        public AbilityDefinitionRegistry BakeOrThrow(
            int tickRate = 30)
        {
            return BakeCombinedOrThrow(
                new[] { this },
                tickRate);
        }

        public static AbilityDefinitionRegistry
            BakeCombinedOrThrow(
                IReadOnlyList<AbilityRuntimeCatalogAsset> catalogs,
                int tickRate = 30)
        {
            if (catalogs == null || catalogs.Count == 0)
                throw new InvalidOperationException(
                    "Combined Ability catalog requires at least one partition.");

            var abilityAssets = new List<AbilityAsset>();
            var passiveAssets =
                new List<FixedPassiveDefinitionAsset>();
            var slotsById =
                new Dictionary<byte, AbilitySlotDef>();
            for (int catalogIndex = 0;
                 catalogIndex < catalogs.Count;
                 catalogIndex++)
            {
                AbilityRuntimeCatalogAsset catalog =
                    catalogs[catalogIndex] ??
                    throw new InvalidOperationException(
                        $"Ability catalog partition {catalogIndex} is null.");
                if (catalog.abilities == null ||
                    catalog.slots == null ||
                    catalog.fixedPassives == null)
                    throw new InvalidOperationException(
                        $"Ability catalog partition '{catalog.name}' contains a null collection.");
                for (int i = 0; i < catalog.abilities.Length; i++)
                    abilityAssets.Add(
                        catalog.abilities[i] ??
                        throw new InvalidOperationException(
                            $"Ability catalog '{catalog.name}' entry {i} is null."));
                for (int i = 0;
                     i < catalog.fixedPassives.Length;
                     i++)
                    passiveAssets.Add(
                        catalog.fixedPassives[i] ??
                        throw new InvalidOperationException(
                            $"Ability catalog '{catalog.name}' fixed passive {i} is null."));
                for (int i = 0; i < catalog.slots.Length; i++)
                {
                    AbilitySlotDef slot = catalog.slots[i] ??
                        throw new InvalidOperationException(
                            $"Ability catalog '{catalog.name}' slot {i} is null.");
                    if (!slotsById.TryGetValue(
                            slot.SlotId,
                            out AbilitySlotDef combined))
                    {
                        slotsById.Add(
                            slot.SlotId,
                            CloneSlot(slot));
                    }
                    else
                    {
                        MergeSlot(combined, slot);
                    }
                }
            }

            abilityAssets.Sort(
                (left, right) =>
                    left.AbilityId.CompareTo(right.AbilityId));
            passiveAssets.Sort(
                (left, right) =>
                    left.AbilityId.CompareTo(right.AbilityId));
            var registry = new AbilityDefinitionRegistry();
            for (int i = 0; i < abilityAssets.Count; i++)
                registry.TryRegisterFromAsset(
                    abilityAssets[i],
                    tickRate);
            for (int i = 0; i < passiveAssets.Count; i++)
                registry.Register(passiveAssets[i].Bake());

            var slotIds = new List<byte>(slotsById.Keys);
            slotIds.Sort();
            for (int i = 0; i < slotIds.Count; i++)
            {
                AbilitySlotDef slot = slotsById[slotIds[i]];
                Array.Sort(slot.AbilityIds);
                slot.InitialActiveAbilityId =
                    slot.AbilityIds[0];
                registry.RegisterSlot(slot);
            }
            return registry;
        }

        private static AbilitySlotDef CloneSlot(
            AbilitySlotDef source)
        {
            return new AbilitySlotDef
            {
                SlotId = source.SlotId,
                MaxAllocatedPoints =
                    source.MaxAllocatedPoints,
                RequiredUnitLevelByRank =
                    source.RequiredUnitLevelByRank != null
                        ? (int[])source
                            .RequiredUnitLevelByRank
                            .Clone()
                        : null,
                AbilityIds = source.AbilityIds != null
                    ? (int[])source.AbilityIds.Clone()
                    : null,
                InitialActiveAbilityId =
                    source.InitialActiveAbilityId,
            };
        }

        private static void MergeSlot(
            AbilitySlotDef destination,
            AbilitySlotDef source)
        {
            if (destination.MaxAllocatedPoints !=
                    source.MaxAllocatedPoints ||
                !ArraysEqual(
                    destination.RequiredUnitLevelByRank,
                    source.RequiredUnitLevelByRank))
                throw new InvalidOperationException(
                    $"Ability SlotId {source.SlotId} has conflicting rank allocation rules across content partitions.");
            if (source.AbilityIds == null ||
                source.AbilityIds.Length == 0)
                throw new InvalidOperationException(
                    $"Ability SlotId {source.SlotId} has no abilities.");
            var ids = new List<int>(
                destination.AbilityIds ??
                Array.Empty<int>());
            for (int i = 0; i < source.AbilityIds.Length; i++)
            {
                int id = source.AbilityIds[i];
                if (ids.Contains(id))
                    throw new InvalidOperationException(
                        $"Ability SlotId {source.SlotId} repeats AbilityId {id} across content partitions.");
                ids.Add(id);
            }
            destination.AbilityIds = ids.ToArray();
        }

        private static bool ArraysEqual(
            int[] left,
            int[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null ||
                left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }
    }
}
