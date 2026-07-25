# Shop UI Foundation — 购买、出售、撤销界面

> ExecPlan 0055 | 2026-07-23
> Design: `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` §11-13
> Predecessor: 0053 (LuaBridge), gold/equipment runtime (verified)
> Conformance: Strict

## Goal

Build the Shop page as a `BattleOverlay` over the HUD: catalog list, detail panel, owned equipment grid, and Buy/Sell/Undo buttons. Shop reads `EquipmentDatabase.Definitions` for catalog, `IEquipmentShopView` for prices, and submits requests to `EquipmentShopRuntime`.

## What already exists

- `EquipmentShopRuntime` — full buy/sell/undo with RequestCheck/ProcessCommand, snapshot, rollback. Wired in `FrameSyncGameRuntime`.
- `GoldIncomeRuntime` — gold tracking including `GetConfirmedAvailableGold()`.
- `EquipmentDatabase` — `AllDefinitions` for catalog, `GetDefinition(id)`.
- `EquipmentHandler` — per-unit 6-slot inventory.
- `LuaBridge` / `LuaRuntime` / `LuaDataCache` — tick-end UI data push.
- `GameBootstrap` — composition root, pushes `UiSnapshotDto` each tick.
- `HealthBarController`, `GoldDisplayController`, `CooldownDisplayController` — simple UI controllers.

## New types and files

### Production code (~380 lines)

| # | File | Assembly | Lines | Purpose |
|---|---:|---|---|---|
| 1 | `Bootstrap/UI/ShopPageController.cs` | `FrameSyncMoba.Bootstrap` | ~180 | MonoBehaviour managing shop lifecycle: catalog scroll list, detail panel, owned equipment grid, buy/sell/undo buttons. Code-driven UI construction in `Awake()` — no prefab dependency. |
| 2 | `Bootstrap/UI/EquipmentSlotView.cs` | `FrameSyncMoba.Bootstrap` | ~80 | Per-slot reusable controller: icon placeholder, name text, price text, stack count, click handler for selection. |
| 3 | `Bootstrap/GameBootstrap.cs` (modify) | `FrameSyncMoba.Bootstrap` | +40 | Add `ShopPageController` field, wire in `Awake()`, provide gold/equipment shop references. |
| 4 | `StreamingAssets/Lua/shop.lua` | — | ~80 | Lua script: catalog filtering, item selection, buy/sell/undo button handlers, gold display refresh. |

### Test code (~120 lines)

| # | File | Assembly | Lines |
|---|---:|---|---|
| 5 | `Bootstrap/Tests/EditMode/ShopPageEditModeTests.cs` | `FrameSyncMoba.Bootstrap.Tests` (new EditMode asmdef) | ~120 |

## Public contract impact

- `ShopPageController` — new public MonoBehaviour, Presentation-only.
- `EquipmentSlotView` — new public MonoBehaviour, reusable.
- No changes to `EquipmentShopRuntime`, `GoldIncomeRuntime`, `EquipmentHandler`, or any deterministic type.
- No new assemblies.

## Snapshot / Checksum impact

None. Shop is presentation-only. Purchase/sell/undo go through existing `EquipmentShopRuntime` → deterministic pipeline.

## Design conformance checklist

- [x] Shop page as `BattleOverlay` over HUD (Design §2.1, §11)
- [x] Catalog reads `EquipmentDatabase.Definitions` (Design §1.4, §11)
- [x] Detail panel shows name/description/stats/price/recipe (Design §11)
- [x] 6-slot owned grid (Design §11)
- [x] Buy/Sell/Undo via `EquipmentShopRuntime` requests (Design §12)
- [x] Price reads via `IEquipmentShopView` / ShopRuntime (Design §1.4, §11)
- [x] Gold display reads from `LuaDataCache` (Design §10, §13)
- [x] Shop does NOT participate in snapshot/rollback (Design §1.7)
- [x] No changes to deterministic types

## Tests

Three focused tests (no full suite):

1. `ShopPageLifecycleTest` — `ShopPageController.Show()` → canvas active, `Hide()` → canvas inactive
2. `ShopPriceCalculationTest` — sell rate 0.7: item value 1000 → sell price 700
3. `ShopPurchaseFlowTest` — `EquipmentShopRuntime.TryBuildPurchasePlan()` with existing equipment, verify plan and cost

## Risk assessment

- **Low risk**: all code is presentation-only, zero deterministic impact.
- UI constructed in code (no prefab dependency) — self-contained, easy to verify.
- `IEquipmentShopView` interface: `EquipmentShopRuntime` already provides all needed methods (`TryBuildPurchasePlan`, `TrySell`, `CanUndo`). ShopPageController binds to these directly.
