using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [CreateAssetMenu(
        fileName = "AbilityLoadout",
        menuName = "FrameSyncMoba/Ability/Unit Loadout")]
    public sealed class AbilityLoadoutAsset : ScriptableObject
    {
        [SerializeField] private AbilityLoadoutSlot[] slots =
            Array.Empty<AbilityLoadoutSlot>();
        [SerializeField] private int fixedPassiveAbilityId;

        public void ApplyOrThrow(
            AbilityHandler handler,
            AbilityDefinitionRegistry registry)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (slots == null)
                throw new InvalidOperationException(
                    "Ability loadout slots must not be null.");

            var seenSlots = new HashSet<byte>();
            for (int i = 0; i < slots.Length; i++)
            {
                AbilityLoadoutSlot loadout = slots[i] ??
                    throw new InvalidOperationException(
                        $"Ability loadout slot {i} is null.");
                if (!seenSlots.Add(loadout.SlotId) ||
                    !registry.TryGetSlot(
                        loadout.SlotId,
                        out AbilitySlotDef slotDef))
                {
                    throw new InvalidOperationException(
                        $"Ability loadout has duplicate or missing SlotId {loadout.SlotId}.");
                }
                if (loadout.InitialAllocatedPoints >
                    slotDef.MaxAllocatedPoints)
                {
                    throw new InvalidOperationException(
                        $"Ability slot {loadout.SlotId} initial points exceed maximum.");
                }

                var runtimeSlot = new AbilitySlotRuntime
                {
                    SlotIndex = slotDef.SlotId,
                    AllocatedPoints =
                        loadout.InitialAllocatedPoints,
                    MaxAllocatedPoints =
                        slotDef.MaxAllocatedPoints,
                    RequiredUnitLevelByRank =
                        slotDef.RequiredUnitLevelByRank != null
                            ? (int[])slotDef
                                .RequiredUnitLevelByRank
                                .Clone()
                            : System.Array
                                .Empty<int>(),
                    ActiveAbilityId =
                        slotDef.InitialActiveAbilityId,
                };
                Dictionary<int, int> initialLevels =
                    BuildInitialLevelMap(
                        loadout,
                        slotDef);
                for (int abilityIndex = 0;
                     abilityIndex < slotDef.AbilityIds.Length;
                     abilityIndex++)
                {
                    int abilityId =
                        slotDef.AbilityIds[abilityIndex];
                    if (!registry.TryGet(
                            abilityId,
                            out AbilityDef definition))
                    {
                        throw new InvalidOperationException(
                            $"AbilityId {abilityId} disappeared during loadout Bake.");
                    }
                    initialLevels.TryGetValue(
                        abilityId,
                        out int level);
                    runtimeSlot.AddAbility(
                        new AbilityRuntime
                        {
                            Definition = definition,
                            Level = level,
                        });
                }
                handler.AddSlot(runtimeSlot);
            }

            if (fixedPassiveAbilityId > 0)
            {
                if (!registry.TryGetPassive(
                        fixedPassiveAbilityId,
                        out PassiveAbilityDef passive))
                {
                    throw new InvalidOperationException(
                        $"Ability loadout fixed passive {fixedPassiveAbilityId} is not registered.");
                }
                handler.SetFixedPassive(passive);
            }
        }

        private static Dictionary<int, int>
            BuildInitialLevelMap(
                AbilityLoadoutSlot loadout,
                AbilitySlotDef slotDef)
        {
            var levels = new Dictionary<int, int>();
            AbilityInitialLevel[] configured =
                loadout.InitialLevels ??
                Array.Empty<AbilityInitialLevel>();
            for (int i = 0; i < configured.Length; i++)
            {
                if (configured[i].AbilityId <= 0 ||
                    configured[i].Level < 0 ||
                    configured[i].Level >
                        slotDef.MaxAllocatedPoints ||
                    Array.IndexOf(
                        slotDef.AbilityIds,
                        configured[i].AbilityId) < 0 ||
                    !levels.TryAdd(
                        configured[i].AbilityId,
                        configured[i].Level))
                {
                    throw new InvalidOperationException(
                        $"Ability slot {loadout.SlotId} has invalid initial level entry {i}.");
                }
            }
            return levels;
        }
    }
}
