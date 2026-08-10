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

        public AbilityDefinitionRegistry BakeOrThrow()
        {
            var registry =
                new AbilityDefinitionRegistry();
            if (abilities == null)
                throw new InvalidOperationException(
                    "Ability catalog abilities must not be null.");
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] == null)
                    throw new InvalidOperationException(
                        $"Ability catalog entry {i} is null.");
                registry.TryRegisterFromAsset(abilities[i]);
            }

            if (slots == null)
                throw new InvalidOperationException(
                    "Ability catalog slots must not be null.");
            var slotIds = new HashSet<byte>();
            for (int i = 0; i < slots.Length; i++)
            {
                AbilitySlotDef slot = slots[i] ??
                    throw new InvalidOperationException(
                        $"Ability slot entry {i} is null.");
                if (!slotIds.Add(slot.SlotId))
                    throw new InvalidOperationException(
                        $"Duplicate Ability SlotId {slot.SlotId}.");
                slot.ValidateOrThrow(registry);
                registry.RegisterSlot(slot);
            }

            if (fixedPassives == null)
                throw new InvalidOperationException(
                    "Ability catalog fixed passives must not be null.");
            for (int i = 0;
                 i < fixedPassives.Length;
                 i++)
            {
                FixedPassiveDefinitionAsset passive =
                    fixedPassives[i];
                if (passive == null)
                    throw new InvalidOperationException(
                        $"Ability catalog fixed passive {i} is null.");
                registry.Register(passive.Bake());
            }
            return registry;
        }
    }
}
