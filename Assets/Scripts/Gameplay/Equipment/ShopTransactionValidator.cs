using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Validates shop transactions: gold sufficiency, inventory space,
    /// unique-item conflicts, and recipe component availability.
    /// Extracted from EquipmentShopRuntime for testability (Equipment/Gold v12 §5).
    /// </summary>
    public static class ShopTransactionValidator
    {
        /// <summary>
        /// Check whether a purchase is valid given current equipment state.
        /// </summary>
        public static EquipmentShopFailureReason ValidatePurchase(
            EquipmentDefinition targetDef,
            EquipmentHandler handler,
            int currentAvailableGold,
            EquipmentDatabase database,
            out EquipmentPurchasePlan plan)
        {
            plan = default;

            if (targetDef == null)
                return EquipmentShopFailureReason.ItemNotFound;

            // Duplicate finished item check
            if (targetDef.Tier == EquipmentTier.Finished && handler.HasDefinition(targetDef))
                return EquipmentShopFailureReason.DuplicateFinishedItem;

            // Unique tag conflicts
            if (targetDef.Tags != null)
            {
                for (int i = 0; i < targetDef.Tags.Length; i++)
                {
                    if (handler.HasTag(targetDef.Tags[i]))
                        return EquipmentShopFailureReason.UniqueTagConflict;
                }
            }

            // Calculate cost and component consumption
            int purchaseCost = targetDef.Value;
            var consumedSlots = new List<int>();

            if (targetDef.Recipe?.Components != null)
            {
                for (int i = 0; i < targetDef.Recipe.Components.Length; i++)
                {
                    var part = targetDef.Recipe.Components[i];
                    int needed = part.Count;
                    for (int s = 0; s < EquipmentHandler.SlotCount && needed > 0; s++)
                    {
                        if (handler.GetSlotDef(s) == part.Item)
                        {
                            consumedSlots.Add(s);
                            purchaseCost -= part.Item.Value;
                            needed--;
                        }
                    }
                    if (needed > 0)
                        return EquipmentShopFailureReason.InvalidRecipe;
                }
            }

            // Destination slot
            int destSlot;
            bool mergeIntoExisting = false;
            int stackableSlot = handler.FindStackableSlot(targetDef);
            if (stackableSlot >= 0)
            {
                destSlot = stackableSlot;
                mergeIntoExisting = true;
            }
            else
            {
                var mockSlots = new bool[EquipmentHandler.SlotCount];
                for (int i = 0; i < consumedSlots.Count; i++)
                    mockSlots[consumedSlots[i]] = true;

                destSlot = -1;
                for (int s = 0; s < EquipmentHandler.SlotCount; s++)
                {
                    if (!mockSlots[s] && handler.GetSlotDef(s) == null)
                    { destSlot = s; break; }
                }
                if (destSlot < 0)
                    return EquipmentShopFailureReason.InventoryFull;
            }

            if (currentAvailableGold < purchaseCost)
                return EquipmentShopFailureReason.InsufficientGold;

            plan = new EquipmentPurchasePlan
            {
                TargetEquipmentId = targetDef.Id,
                PurchaseCost = purchaseCost,
                ConsumedComponentSlots = consumedSlots.ToArray(),
                MergeIntoExistingStack = mergeIntoExisting,
                DestinationSlot = destSlot,
            };

            return EquipmentShopFailureReason.None;
        }

        /// <summary>
        /// Validates a sell request.
        /// </summary>
        public static EquipmentShopFailureReason ValidateSell(
            EquipmentHandler handler, int slot, fp sellRate, out int sellValue)
        {
            sellValue = 0;
            var def = handler.GetSlotDef(slot);
            if (def == null)
                return EquipmentShopFailureReason.EmptySlot;

            sellValue = (int)((fp)def.Value * sellRate);
            if (sellValue <= 0) sellValue = 1;
            return EquipmentShopFailureReason.None;
        }

        /// <summary>
        /// Validates whether the undo operation is available.
        /// </summary>
        public static EquipmentShopFailureReason ValidateUndo(ShopTraderRuntime trader)
        {
            if (trader == null)
                return EquipmentShopFailureReason.ControlledUnitNotFound;
            if (trader.UndoableOperationStack.Count == 0)
                return EquipmentShopFailureReason.NoUndoableTransaction;
            return EquipmentShopFailureReason.None;
        }
    }
}
