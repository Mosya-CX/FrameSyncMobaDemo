# ExecPlan 0030 — Equipment System Completion

> **Design authority**: `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`
> **Estimated code**: ~500–700 lines
> **Dependencies**: EquipmentHandler ✓ (0028) / CombatSystem ✓ / Stats ✓ / GoldIncomeRuntime ✓

## Rationale

Plan 0028 built the Equipment skeleton (Handler, Definition, ShopRuntime with IRollback). Plan 0030 completes the system: full buy/sell/undo transaction logic, effect module event dispatch, EquipmentDatabase for definition lookups, and CombatParticipationFlags auto-invalidation per design §5.22.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| `Unit/Equipment/EquipmentDatabase.cs` | ~80 | Global registry of all EquipmentDefinitions. Bake-time validation (duplicate Id, circular recipe, tag conflicts). Runtime lookup by Id. |
| `Unit/Equipment/EquipmentEffectDispatch.cs` | ~100 | Central dispatcher: listens to per-Unit EventBus events, finds relevant equipment effects, executes modules in stable Slot/Effect/Module order |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Equipment/EquipmentShopRuntime.cs` | +200 | Full buy/sell/undo: TryBuildPurchasePlan, RequestPurchase, ProcessPurchase, RequestSell, ProcessSell, RequestUndo, ProcessUndo. Two-layer check (RequestCheck + ProcessCommand). Undo invalidation rules. |
| `Unit/Equipment/EquipmentHandler.cs` | +60 | Add GetSlotDef, HasTag, HasDefinition for shop queries. MergeIntoStack, FindStackableSlot. |
| `Unit/Combat/CombatSystem.cs` | +30 | After damage/heal/shield settlement: notify EquipmentShopRuntime with CombatParticipationFlags for undo invalidation |
| `Unit/Combat/CombatEvents.cs` | +15 | Add CombatParticipation update callback; wire to EquipmentShopRuntime |
| `FrameSync/SimulationTickPipeline.cs` | +15 | Wire EquipmentEffectDispatch tick + EquipmentShopRuntime.Advance into Tick loop |

## Key conformance

- `EquipmentShopRuntime` implements two-layer check: RequestCheck (local, no side-effects) → ProcessCommand (deterministic, all endpoints)
- `TryBuildPurchasePlan` computes PurchaseCost = Target.Value − Sum(ConsumedComponents.Value)
- Sell gives GoldDelta = +Value × SellRate (positive)
- Undo restores `Before` state, marks `Record.Reverted = true`, pops undo stack
- `EquipmentEffectDispatch` iterates in fixed order: Slot 0→5, EffectIndex 0→1, ModuleIndex 0→N
- `CombatParticipationFlags` auto-set during CombatSystem settlement: DamageDealt/DamageTaken/HealDealt/HealTaken/ShieldGranted/ShieldReceived
- Shop undo invalidated by: leaving shop range, combat participation, equipment use
