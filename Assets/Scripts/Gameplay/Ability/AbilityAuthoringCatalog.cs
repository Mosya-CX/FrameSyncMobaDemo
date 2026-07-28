using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class AbilitySlotDef
    {
        public byte SlotId;
        [Min(1)] public byte MaxAllocatedPoints = 5;
        public int[] RequiredUnitLevelByRank =
            Array.Empty<int>();
        public int[] AbilityIds = Array.Empty<int>();
        public int InitialActiveAbilityId;

        public void ValidateOrThrow(
            AbilityDefinitionRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (AbilityIds == null || AbilityIds.Length == 0)
                throw new InvalidOperationException(
                    $"Ability slot {SlotId} has no abilities.");
            if (RequiredUnitLevelByRank == null ||
                RequiredUnitLevelByRank.Length !=
                    MaxAllocatedPoints)
            {
                throw new InvalidOperationException(
                    $"Ability slot {SlotId} requires exactly {MaxAllocatedPoints} rank requirements.");
            }
            int previousRequirement = 0;
            var ids = new HashSet<int>();
            bool containsInitial = false;
            for (int i = 0; i < AbilityIds.Length; i++)
            {
                int abilityId = AbilityIds[i];
                if (!ids.Add(abilityId) ||
                    !registry.TryGet(abilityId, out _))
                {
                    throw new InvalidOperationException(
                        $"Ability slot {SlotId} has missing or duplicate AbilityId {abilityId}.");
                }
                containsInitial |=
                    abilityId == InitialActiveAbilityId;
            }
            if (!containsInitial)
                throw new InvalidOperationException(
                    $"Ability slot {SlotId} initial AbilityId is not in the slot.");
            for (int i = 0;
                 i < RequiredUnitLevelByRank.Length;
                 i++)
            {
                int requirement =
                    RequiredUnitLevelByRank[i];
                if (requirement <= previousRequirement)
                    throw new InvalidOperationException(
                        $"Ability slot {SlotId} rank requirements must be strictly increasing.");
                previousRequirement = requirement;
            }
        }
    }

    [Serializable]
    public struct AbilityInitialLevel
    {
        public int AbilityId;
        [Min(0)] public int Level;
    }

    [Serializable]
    public sealed class AbilityLoadoutSlot
    {
        public byte SlotId;
        public byte InitialAllocatedPoints;
        public AbilityInitialLevel[] InitialLevels =
            Array.Empty<AbilityInitialLevel>();
    }

}
