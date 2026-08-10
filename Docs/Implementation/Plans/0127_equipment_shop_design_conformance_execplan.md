# ExecPlan 0127 - Equipment/Shop design-conformance slice

## 1. Purpose

Bring the equipment/shop domain in line with
`moba_equipment_shop_gold_system_design_v12` while keeping the current Command,
snapshot and GoldIncomeRuntime contracts intact.

## 2. Progress

- [x] Convert `EquipmentDefinition` to ScriptableObject (design v12 2.1;
  `CanStack` derived from Tier per 2.4; Icon + TextArea Description).
- [x] Add `EquipmentTagUid` + `EquipmentTagDefinition` ScriptableObject
  (design v12 2.9) with auto-assigned Uid; `EquipmentDefinition.Tags` now
  references tag assets.
- [x] Restrict exclusivity to `UniqueEquipmentTagTable` tags only
  (design v12 2.10/2.12); `UniqueEquipmentTagTable` keyed by Uid; shop
  `ValidatePostPurchaseState` consults the table.
- [x] Add `EquipmentShopRequestCheck` + `IEquipmentShopCommandSubmitter`
  (UI design v9.1 12.1/12.5) and `RequestPurchase/RequestSell/RequestUndo`
  on `EquipmentShopRuntime`; income view + submitter wired in the composition
  root.
- [x] Add `EquipmentCatalogAsset` authoring ScriptableObject and
  `BakeOrThrow()` (design v12 5.2 shop reads EquipmentDatabase.Definitions).
- [x] Create neutral framework fixtures: 3 tags, 3 items, catalog under
  `Assets/Fixtures/Framework/Config/Equipment/`; wired into ClientBootstrap
  and ServerBootstrap GameBootstrap `equipmentCatalog`.
- [x] Tests green: Request 8/8, Transaction 2/2, TagAndCatalog 4/4,
  ShopFoundation 7/7, FullGameplayLoop 4/4; compile 0 errors.

## 3. Surprises and discoveries

- `UniqueEquipmentTagTable` existed but was not consulted by the shop's
  post-purchase validation; every tag previously conflicted.
- `EquipmentTagDefinition` must live in a same-named file or Unity cannot bind
  `m_Script` when the asset is created.
- Design's `GlobalGameplayData.EquipmentDatabase` conflicts with the current
  RuntimeConfig -> Unit assembly direction; the catalog asset lives in the Unit
  assembly like `UnitRuntimeCatalogAsset`.

## 4. Decision log

- Keep `int` EquipmentId (project Command/View canonical) instead of a second
  `EquipmentId` value type.
- `EquipmentEffectDef` stays a plain serializable class (design 3.2: no SO per
  effect).
- Neutral fixtures follow the existing Assets/Fixtures pattern.

## 5. Current repository context

- Changed: `EquipmentDefinition.cs`, `EquipmentTag.cs` (new),
  `EquipmentTagDefinition.cs` (new), `UniqueEquipmentTagTable.cs`,
  `EquipmentDatabase.cs`, `EquipmentHandler.cs`, `EquipmentShopRuntime.cs`,
  `EquipmentShopRequests.cs` (new), `EquipmentCatalogAsset.cs` (new),
  `FrameSyncGameRuntime.cs`, `PlayerCommandRequester.cs`, `GameBootstrap.cs`,
  tests `EquipmentShopRequestTests.cs`, `EquipmentTagAndCatalogTests.cs`.
- Fixture assets: `Assets/Fixtures/Framework/Config/Equipment/`.

## 6. Design sources

- `moba_equipment_shop_gold_system_design_v12.md` sections 2.1-2.13, 5.2-5.23.
- `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` sections 12, 13.

## 7. Scope

In scope: definition SO + tags + unique-table exclusivity + Request API +
catalog bake + neutral fixtures.

Out of scope: `EquipmentId` type, EquipmentTagUid in command payloads,
production equipment content.

## 8. Implementation plan

Completed as described in Progress. Remaining design-alignment candidates:

- `EquipmentTagUid`/`EquipmentId` strong typing if a future contract requires
  it.
- Consumable sell/undo single-unit semantics already verified by Transaction
  tests.

## 9. Public contracts

Added: `EquipmentTagUid`, `EquipmentTagDefinition`,
`EquipmentShopRequestCheck`, `IEquipmentShopCommandSubmitter`,
`EquipmentCatalogAsset`, `EquipmentShopRuntime.Request*`,
`EquipmentShopRuntime.ConfigureIncomeView/SetCommandSubmitter`,
`EquipmentDatabase.SetUniqueTagTable/UniqueTagTable`,
`EquipmentDefinition.CanStack`, `EquipmentHandler.HasTag(Uid/TagDefinition)`.

## 10. Validation

See Progress; Unity MCP compile 0 errors; EditMode suites listed above.

## 11. Failure and recovery

Assets created via Unity MCP script-execute; scene wiring recorded in the scene
assets. Tag asset script bindings repaired through SerializedObject when the
same-file rule was violated.

## 12. Results

Equipment/shop design-conformance slice completed 2026-08-02; Shop now has a
non-empty neutral catalog on both endpoint scenes.
