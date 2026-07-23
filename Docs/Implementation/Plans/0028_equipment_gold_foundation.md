# ExecPlan 0028 — Equipment & Gold System Foundation

> **Design authority**: `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`
> **Estimated code**: ~750–950 lines
> **Dependencies**: Combat ✓ / Stats ✓ / GoldIncomeRuntime ✓ / FrameSync ✓ / Unit lifecycle ✓

## Rationale

GoldIncomeRuntime already exists as the sole gold batch owner. The Equipment system is the natural next Gameplay module: it connects Combat rewards (gold) to Unit power (stats/modifiers), and the design explicitly requires EquipmentHandler on Unit, EquipmentShopRuntime in snapshot, and gold batch digests in SharedGameplayChecksum.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| `Unit/Equipment/EquipmentHandler.cs` | ~280 | EquipmentHandler (6-slot, add/remove/swap, FixedStats via StatHandler, death/respawn lifecycle), EquipmentInstance runtime, EquipmentFixedStat struct, EquipmentTier enum, EquipmentHandlerSnapshot struct, IRollback |
| `Unit/Equipment/EquipmentDefinition.cs` | ~120 | EquipmentDefinition ScriptableObject (Id/Tier/Value/MaxStack/FixedStats/Effects/Tags/Recipe), EquipmentRecipe + EquipmentRecipePart structs |
| `Unit/Equipment/EquipmentEffect.cs` | ~100 | EquipmentEffectDef ScriptableObject, EquipmentEffectModule abstract base, EquipmentEffectInvokeTiming enum, EquipmentEffectRuntime + ModuleRuntimeState |
| `Unit/Equipment/EquipmentShopRuntime.cs` | ~280 | EquipmentShopRuntime : IRollback (buy/sell/undo skeleton, OperationLog, UndoableOperationStack), ShopTraderRuntime, ShopOperationRecord, EquipmentShopRuntimeSnapshot + ShopTraderRuntimeSnapshot, EquipmentShopOperationType enum, EquipmentShopFailureReason enum, CombatParticipationFlags, EquipmentPurchasePlan, IConfirmedGoldIncomeView interface |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Core/Unit.cs` | +5 | Add `EquipmentHandler` property |
| `Unit/Core/UnitWorld.cs` | +15 | Create EquipmentHandler in SpawnUnit; ClearForDeath/ClearForRespawn hooks |
| `FrameSync/GameplaySnapshot.cs` | +10 | Add `EquipmentShopRuntimeSnapshot` field (was placeholder comment) |
| `FrameSync/SimulationTickPipeline.cs` | +30 | Wire EquipmentHandler.Advance + EquipmentShopRuntime tick; Capture/Restore integration |
| `FrameSync/GoldIncomeRuntime.cs` | +40 | Add `GoldIncomeBatchDigest` struct; `SealTick()` method; digest computed from batch records |

## Key conformance

- `EquipmentHandler` implements `IRollback<EquipmentHandlerSnapshot>` (6-slots, FixedStat tokens, shared cooldowns)
- `EquipmentShopRuntime` implements `IRollback<EquipmentShopRuntimeSnapshot>` (per-player traders, OperationLog, undo stack)
- `GoldIncomeRuntime` is sole owner of gold batches/digests — NOT in GameplaySnapshot ✓
- `GoldIncomeBatchDigest[T]` forced into `SharedGameplayChecksum(T)` ✓
- FixedStats use `StatHandler.AddModifier` — handles survive death/respawn per design
- Death calls `ClearForDeath` (retain EquipmentInstances, release cross-life handles) — Respawn calls `ClearForRespawn` (rebuild life-stage handles)

## Deferred to later plan

- Complete buy/sell/undo transaction logic (ProcessCommand, RequestCheck, CanUndo)
- EquipmentEffectDef full module dispatch (OnEquipped/OnUnequipped/Tick/DamageDealt/etc.)
- Active equipment use (CheckUse/Use via ActionArbiter)
- CombatParticipationFlags auto-invalidation
- EquipmentDatabase / GlobalGameplayData bake
