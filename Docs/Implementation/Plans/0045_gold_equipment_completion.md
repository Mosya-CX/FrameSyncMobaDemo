# Plan 0045: Gold & Equipment Completion

> Historical status: formerly marked Completed. The 2026-07-22 design-conformance re-audit does **not** accept this as implemented or verified because the plan itself records missing Equipment/Gold integration tests and unwired runtime helpers, while authoritative float and snapshot/checksum gaps remain. Current status is `Partial` in `MODULE_STATUS.md`; use the 0046 recovery sequence rather than extending this plan.
> Created: 2026-07-22
> Design: Equipment/Gold v12
> Predecessor: 0044 XP & Level-Up System
> Lines: ~395 (new + modified)

## Summary

Closes the kill→reward→shop→stats loop by completing Equipment/Gold integration:
- Gold bounty wiring from kills to GoldIncomeRuntime
- EquipmentEffectRuntime lifecycle (create on purchase, cleanup on removal, rebuild on respawn)
- Shop transaction validation extraction
- Equipment passive effect application on purchase/removal
- CurrentAvailableGold computation with shop delta integration

## Files Changed

### New files

| File | Lines | Description |
|---|---|---|
| `Unit/Equipment/ShopTransactionValidator.cs` | ~110 | Extracted validation: purchase (gold, slots, unique items), sell, undo |
| `Unit/Equipment/EquipmentPassiveApplier.cs` | ~125 | OnEquip/OnUnequip passive application, respawn rebuild |
| `Unit/Equipment/UniqueEquipmentTagTable.cs` | ~70 | Global unique tag configuration |

### Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/SimulationTickPipeline.cs` | +5/-5 | Fix gold wiring: use `CombatSystem.GoldAllocations` directly instead of nonexistent `GetTickResult()` |
| `Unit/Equipment/EquipmentHandler.cs` | +55 | Add `EquipmentEffectRuntime[]` to `EquipmentInstance`; create EffectRuntimes on `Add()`, cleanup on `Remove()`, rebuild on `Restore()`/`ClearForRespawn()`; add `ReleaseEffectRuntimes()` helper |
| `FrameSync/GoldIncomeRuntime.cs` | +10 | Add `GetCurrentAvailableGold(playerSlot, shopGoldDelta)` |
| `Unit/Equipment/EquipmentShopRuntime.cs` | +15 | Add `ComputeEffectiveShopGoldDelta(playerSlot)` |
| `Unit/Combat/DeathEffectDispatcher.cs` | +10 | Add `DistributeGold()` hook for death gold effects |

## Design Conformance

- Equipment/Gold v12 §1.7: EquipmentEffectRuntime creation, EffectRuntimes stored on EquipmentInstance
- Equipment/Gold v12 §3: Passive effect lifecycle (OnEquipped, OnUnequipped, per-tick advance)
- Equipment/Gold v12 §5.17: CurrentAvailableGold = ConfirmedEarnedGoldTotal + EffectiveShopGoldDelta
- Equipment/Gold v12 §7.15: Gold allocation deterministic wiring through CombatSystem
- Equipment/Gold v12 §2.10: UniqueEquipmentTagTable for cross-item exclusivity

## Tests

Tests deferred to a follow-up plan due to MCP connectivity. Manual verification checklist:
- Purchase item → fixed stats applied, EffectRuntimes created
- Sell item → stats removed, EffectRuntimes cleaned up
- Kill enemy → gold recorded in GoldIncomeRuntime via pipeline
- Undo purchase → gold delta reverts, EffectRuntimes cleaned up
- Death/respawn → EffectRuntimes rebuilt

## Remaining Limitations

- UniqueEquipmentTagTable not yet wired into EquipmentShopRuntime.TryBuildPurchasePlan (uses inline tag check)
- EquipmentPassiveApplier not yet called from EquipmentHandler (handler uses its own DispatchOnEquipped)
- ShopTransactionValidator not yet consumed by EquipmentShopRuntime (still uses inline validation)
- No EditMode tests for gold/equipment integration
