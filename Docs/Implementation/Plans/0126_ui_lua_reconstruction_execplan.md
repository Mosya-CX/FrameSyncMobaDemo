# ExecPlan 0126 - UI/Lua reconstruction (design v9.1 alignment)

## 1. Purpose

Replace the C#-controller placeholder UI with the formal Lua-driven UI
architecture from `MOBA_UI_Lua_System_Design_v9_1`, while adapting to the
current framework (existing Command/Shop/Gold contracts, Bootstrap composition
root, `Assets/Resources/Prefab/UI/` prefabs as the authoritative page assets).

User decisions locked in:

```text
1. Assets/Resources/Prefab/UI/ prefabs are the formal page assets.
   Structure is trusted; components may be replaced or extended.
2. Scoreboard is kept (deviates from design v9.1 "not designed"; user task wins).
3. C# UI control logic is deleted; UI is fully Lua-controlled
   (Lua reads/analyses/displays/forwards/calls).
4. Assets/3rd/XLua source gets its own asmdefs.
5. Lobby flow: Main menu -> Match overlay -> connection wait ->
   Hero Select -> close all UI + Loading -> server notice ->
   battle HUD start.
6. Every UI's visual result is finally accepted by the user;
   battle HUD is fixed-or-placeholdered before testing.
```

## 2. Progress

- [x] Read UI/Lua design v9.1 in full; read current repo UI state and prefabs.
- [x] Audit all UI prefabs under `Assets/Resources/Prefab/UI/` (structure,
  missing scripts, host components, controllers).
- [x] Create `XLua.Runtime` + `XLua.Editor` asmdefs for `Assets/3rd/XLua`.
- [x] Add `XLua.Runtime` reference to `FrameSyncMoba.LuaBridge`.
- [x] Add `LuaManager` (LuaEnv, StreamingAssets loader, LuaInit, page/cell
  hosts), `LuaHost` (page/cell lifecycle proxy), `UIRef`, `UIDisplayConvert`.
- [x] Add `LuaInit.lua`, `UI/Core/{UIBase,UICellBase,UIFormat}.lua`.
- [x] Add FrameSync read-only UI query contracts: `FrameSyncGameRuntime.Instance`,
  `LocalPlayerSlot`, `GetLocalControlledUnit()`, `LocalEquipmentShopView`,
  `EquipmentShop`; Bootstrap registers/binds them.
- [x] Add typed skill-point request entry `RequestAllocateAbilitySkillPoint(slot)`
  on `PlayerCommandRequester` (design v9.1 10.17 adaptation).
- [x] Wire Lua host into `UIPanel` (luaModule/UIRef/Build/Refresh/Dispose) and
  `UIManager` (owns LuaManager, builds pages, LuaEnv.Tick).
- [x] Foundation tests green: LuaBridge 21/21, Deterministic boundary 1/1,
  Unit boundary 3/3, PlayerCommandRequester 6/6 (incl. skill point),
  UIManagerPrefab PlayMode 1/1; compile 0 errors (xLua example warnings only).
- [x] Shop/equipment design-conformance fix: `EquipmentDefinition` converted to
  ScriptableObject (design v12 2.1; `CanStack` derived from Tier per 2.4);
  added `EquipmentShopRequestCheck` + `IEquipmentShopCommandSubmitter`
  (design v9.1 12.1/12.5) and `RequestPurchase/RequestSell/RequestUndo` on
  `EquipmentShopRuntime`; wired income view + PlayerCommandRequester submitter
  through the composition root. Tests: Request 8/8, Transaction 2/2,
  ShopFoundation 7/7, FullGameplayLoop 4/4.
- [x] Slice B: UIManager API alignment (7 pages, Main/BattleOverlay layers,
  ShowPage/ShowOverlay/HideOverlay/Refresh/CloseAll, ShopOwnedEquipmentFocused).
  Prefabs migrated via MCP: Match/Load gained UIPage+UIPanel hosts,
  UIManager.prefab registrations rewritten to Main/Match/Select/Load/HUD/Shop/
  Result, GameplayHUD ShopBtn targetPage fixed. GameBootstrap and PlayMode
  tests updated. UIManagerPrefabPlayModeTests 2/2 (design API + lifecycle).
- [x] Slice C: page Lua scripts for Main/Match/Select/Load/Result/Shop + flow
  routing per user lobby flow; delete C# page controllers
  (Lobby/HeroSelect/Result/Shop). Added UIList/UICell (LuaBridge) and
  GameFlowLuaBridge; wired luaModule+refs on six page prefabs and two cell
  prefabs via MCP; GameBootstrap now drives pages through the design API and
  binds the flow bridge. UIManager.Instance exposed for Lua.
- [x] Legacy scene-level ClientUI replaced by UIManager in ClientBootstrap and
  ClientFrameworkSmoke (duplicate EventSystem/GameplayHUD/controller shells
  removed). All seven page prefabs gained CanvasGroup with UIPanel.canvasGroup
  wired via MCP. BindLocalPlayer no longer forces the HUD page at bind time.
- [~] Slice D: HUD Lua page (design 10). `GameplayHUD` now binds
  `luaModule=UI.HUD` + 12 refs (Health/Mana/Exp Slider, Gold, Q/W/E/R
  CooldownMask + CooldownText); six legacy C# HUD state controllers
  (`AbilityCooldownPresenter`, `CooldownDisplayController`, `HealthBar`,
  `ResourceBar`, `ExperienceBar`, `GoldDisplay`) are deleted; HUD.lua reads
  `GameFlowLuaBridge` getters with a per-frame targeted refresh and cooldown
  seconds display. Remaining prefab gaps and user acceptance are pending.
- [x] Slice E: Scoreboard kept as Lua-controlled HUD overlay.
  `ScoreboardController`/`ScoreboardRow` deleted. The scoreboard is the
  existing `MatchPart` node (TimeText / TeamScoreText / KDAText /
  CreepScoreText); no new child objects are added to the prefab. HUD.lua
  refreshes elapsed time, blue/red team kill score, local KDA and creep score
  through bridge queries. Minimap keeps its C# controller for now.
- [x] HUD refs/coverage: GameplayHUD now exposes 50 refs covering MatchPart,
  HeadIcon, HealthText/ManaText ("current/max" next to the Sliders), QWER
  Icon+CooldownMask+CooldownText, PassiveAbilitySlot (Icon + CooldownMask +
  CooldownText), 16 property texts (MainProperty + ExtendProperty),
  ExtendPropertyRoot/MainPropertyRoot and the 6 EquipmentSlot buttons. HUD.lua
  drives vitals+texts, QWER and passive cooldown masks (activated while on
  cooldown), active-ability icon visibility, C-key hold to swap MainProperty
  <-> ExtendProperty, gold, stats, and equipment slots (icon sprite / occupied
  tint, click forwards `FocusShopEquipment(slot, id)` to the Shop overlay).
- [x] Buff bar (user-added `BuffBar`): `BuffCell.prefab` now carries a UICell
  host (`UI.BuffCell`, refs Icon/UsageLine/StackText), `BuffBar` gained a UIList
  (cellPrefab=BuffCell) and a `BuffList` ref (51 total refs). `BuffHandler`
  exposes the stable `BuffConfigId`-ordered read-only view
  (`GetAllOrdered`); the bridge exposes per-index buff Icon/Name/Stacks/
  TimeProgress/IsPermanent/ShowStack following design v14.2 UI rules (all
  buffs show icon, stacks only when `MaxStacks > 1`, no time progress for
  permanent buffs, `TimeProgress = RemainingTicks / DurationTicks`).
- [ ] Slice F: delete per-tick DTO push path, legacy Text fallbacks,
  `LuaDataCache`.
- [ ] Equipment authoring path: `EquipmentCatalogAsset` (or
  `GlobalGameplayData.EquipmentDatabase` field) so Bootstrap bakes a non-empty
  database; `EquipmentTagDefinition` ScriptableObject (design v12 2.9) and
  neutral test catalog items (2-3 items) for Shop.

## 3. Surprises and discoveries

- Prefabs are TMP-based with zero missing scripts; flow-page structure matches
  design v9.1 (Main: NameText/StartBtn/QuitBtn; Match: StateText/TimeText/
  CancelBtn/SearchingRoot; Select: HeroList/ConfirmButton/ConfirmStateText;
  Load: ProgressBar/ProgressText; Result: ContinueBtn).
- `MatchPanel` and `LoadingPanel` currently have no `UIPage`/`UIPanel` host.
- `ShopPanel`'s `Detail` node is empty; catalog/detail content is runtime-built
  by the C# controller (to be replaced by prefab UIRefs + Lua).
- `GameplayHUD` has StatusBar/Mana-Health, AbilityBar QWER+passive, MiniMap,
  MatchPart, EquipmentBar(+ShopBtn), PropertyBar(Main/Extend), ExpBar; missing
  design items: LevelText/HpText/ResourceText/DeadRoot/RespawnText, skill
  UpgradeRoot/InfoRoot, EquipCell Stack/Charge separation, GoldText.
- `ResultPanel` uses DefeatIcon/VictoryIcon instead of design TitleText.
- User added a `BuffBar` root (empty layout container) to `GameplayHUD`;
  the HUD Lua page needs a BuffList binding and Buff cells. The HUD also now
  has six `EquipmentSlot1-6` buttons and a Gold text under ShopBtn.
- P0 fixed: FrameworkSmoke/ClientFrameworkSmoke now reference
  FullMatchDeterministicMapConfig; auto-loaded Map prefab no longer overrides
  an explicitly configured map; NeutralUnitRuntimeCatalog gained a dispose
  policy table; SimulationTickPipeline restore schema check now uses the
  constant (hardcoded 13 fixed); stale gold-confirmation assertion updated.
  FrameworkSmokeBootstrapTests 1/1 and ClientFrameworkSmokeSceneTests 1/1
  pass.

## Slice C validation

- UiLuaPagesSmokeTests 1/1 (Main boots with Lua host, flow bridge routes to
  Match, Shop overlay opens with the 3-item neutral catalog).
- UIManagerPrefabPlayModeTests 2/2 (design API + six Lua page hosts).
- ClientFrameworkSmokeSceneTests 1/1; FrameworkSmokeBootstrapTests 1/1.
- `GameBootstrap` creates an empty `EquipmentDatabase`; Shop has no test items.
- `ProjectSettings.asset` gained `runInBackground: 1` during the editor restart
  (unrelated to this plan; flagged, not reverted).
- xLua delegate shapes like `Action<LuaTable,int>` require CSharpCallLua code
  generation; page lifecycle `Action<LuaTable>` works natively. Cell lifecycle
  therefore caches `LuaFunction` inside `LuaHost` (no gen dependency).
- xLua `LuaEnv.Dispose` throws "try to dispose a LuaEnv with C# callback" when
  any C#-to-Lua delegate bridge is still referenced. Fixed on three levels:
  (1) `LuaHost.BindCell` now caches and calls the cell `Dispose` so
  `UICellBase` unbinds Unity events; (2) `LuaManager` tracks every page/cell
  host and disposes them before closing the `LuaEnv`, covering teardown order
  where cells outlive `UIManager.OnDestroy`; (3) UI instances are moved into
  the owning scene so additive-loaded scenes destroy their cells. Regression:
  `LuaHostAndManagerTests.ManagerDispose_ReleasesOutstandingHosts`, and
  `ClientFrameworkSmokeSceneTests` (which awaits the unload) now passes 1/1.
- Lua file layout is flat (user-owned): `StreamingAssets/Lua/Core/` holds
  `LuaInit.lua` + `UIBase/UICellBase/UIFormat/TestCell/TestPage`; page and cell
  modules live directly under `StreamingAssets/Lua/` (`HUD.lua`, `Main.lua`,
  `HeroCell.lua`, `BuffCell.lua`, ...). `LuaManager` resolves module names by
  stripping a leading `UI.` prefix (`UI.HUD` -> `Lua/HUD.lua`,
  `UI.Core.UIBase` -> `Lua/Core/UIBase.lua`) and boots via
  `require('Core.LuaInit')` -> `Lua/Core/LuaInit.lua`. Prefab `luaModule`
  strings and Lua `require` names are unchanged.
- Scene/flow audit (2026-08-04): local C/S flow already existed on
  ClientBootstrap/ServerBootstrap (`LocalNgoEndpointDriver` +
  `LobbyNetworkBridge` on the NetworkManager GameObject); ClientBootstrap was
  missing its `ClientUiActionRouter` (now wired). Smoke scenes moved to
  `Assets/Scenes/Tests/` (FrameworkSmoke, ClientFrameworkSmoke; build-settings
  paths updated). Orphan scenes deleted: `GameScene.unity`, `Lobby.unity`; the
  empty `FullMatchLaneTopology` GameObject was removed from
  ClientBootstrap/ServerBootstrap/MinionTowerLongRunTest. Map is
  config-data-driven (`FullMatchDeterministicMapConfig` + baked FlowFields);
  no Map prefab/scene is required.
- UOS readiness audit (2026-08-04): the four UOS packages are installed and
  compile; the DedicatedServer/Client adapters exist. NOT deployable as-is:
  (1) DedicatedServerApplicationFlow is only driven to `AwaitAssignedPlayers`
  (BootAsync); EnterLobby/LoadingBarrier/StartGameplay/ResultDelivery/
  Settlement/Shutdown are never called by production code (only tests);
  (2) both scenes hardcode `enableOnlineApplicationFlow=0` +
  `localDevelopmentNetworkFlow=1` and carry the LocalNgoEndpointDriver, which
  would conflict with the UOS flow in a UOS build; (3) no UOS build menu or
  scene variant; (4) `cloudProjectId` is empty and `uosMatchmakingConfigId/
  uosRegionId` are unset (external UOS project configuration required).
- The MCP `assets-refresh` can report stale "compilation errors exist"; a forced
  `CompilationPipeline.RequestScriptCompilation()` cleared the stale state.
- v10.2 auto-test account (`TestAccountBootstrapService`/`ClientAccountSession`)
  was already implemented; the only gap was that `AccountDisplayName` was
  hardcoded to "Player". It is now filled from the resolved
  `TestAccountId` after `InitializeAccountAsync` completes and the Main page
  is refreshed.
- Prefab edits through `PrefabUtility.SaveAsPrefabAsset` silently fail
  (return null) while the prefab is open in Prefab Mode, and a prefab with a
  missing script cannot be saved at all. The deleted `ScoreboardController`
  left a missing-script component on `GameplayHUD`; the prefab stage had to be
  discarded by the user and the missing script removed before refs could be
  persisted. Follow-up: never delete a prefab-attached script before cleaning
  the asset, and close Prefab Mode before scripted prefab saves.
- Player input aligned with `MOBA_Player_Input_Command_Module_Design_v1_1`:
  the asset was renamed to `PlayerInputActions.inputactions` (meta guid kept)
  and gained the required `UI` map (Point/Move/Submit/Cancel/Left/Middle/Right
  click/ScrollWheel/TrackedDevice); `UIManager`'s `InputSystemUIInputModule`
  previously referenced a deleted asset guid and is now rewired to the real
  sub-asset action references. `PlayerInputController` now drops
  Primary/SecondaryClick over UI (`EventSystem.IsPointerOverGameObject`,
  design 16.4) so UI clicks never generate world Commands. "Hold C to expand
  stats" moved from raw `Input.GetKey` into the Gameplay map as `ExpandStats`:
  `PlayerInputController` writes `PresentationInputState.ExpandStatsHeld`
  (presentation-only, no Command/buffer), and HUD.lua reads it via
  `GameFlowLuaBridge.IsExpandStatsHeld`.

## 4. Decision log

- Design v9.1 is the UI contract; user task overrides it where stated
  (Scoreboard kept; user lobby flow; user final acceptance).
- Framework adaptation: typed UI requests live on `PlayerCommandRequester`
  (existing canonical command path) instead of duplicating FrameSync submitter
  logic; Lua query contracts live on `FrameSyncGameRuntime.Instance`.
- `LuaManager`/`LuaHost` live in `FrameSyncMoba.LuaBridge`; deterministic
  assemblies never reference `XLua.Runtime` (boundary tests enforce it).
- xLua code generation is not required for the foundation slice.
- Battle HUD testing is deferred until prefab/C# gaps are fixed or placeholdered
  (user instruction); only non-HUD foundation parts are tested now.

## 5. Current repository context

- `Assets/3rd/XLua/XLua/Src/XLua.Runtime.asmdef`, `Src/Editor/XLua.Editor.asmdef`
- `Assets/Scripts/LuaBridge/`: `LuaManager.cs`, `LuaHost.cs`, `UIRef.cs`,
  `UIDisplayConvert.cs`, tests `LuaHostAndManagerTests.cs`,
  `UIDisplayConvertTests.cs`
- `Assets/StreamingAssets/Lua/`: `LuaInit.lua`, `UI/Core/{UIBase,UICellBase,UIFormat}.lua`,
  test fixtures `TestPage.lua`, `TestCell.lua`
- Modified: `FrameSyncMoba.LuaBridge.asmdef`, `FrameSyncGameRuntime.cs`,
  `PlayerCommandRequester.cs`, `GameBootstrap.cs`, `UIPanel.cs`, `UIManager.cs`
- Formal page assets: `Assets/Resources/Prefab/UI/` (10 prefabs)
- Test unit asset: `Assets/Config/FullMatchTest/Prefabs/TestHeroRuntime.prefab`

## 6. Design sources

- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`
  sections 2-12, 14 (UIManager/UIPanel/UIRef/LuaManager/LuaHost/UIBase/
  UICellBase/UIFormat/UIDisplayConvert/HUD/Shop/landing order)
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md` (application
  flow boundaries)
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md` (shop/gold views)

## 7. Scope

In scope:

- Lua execution layer, page/cell hosts, refs tables, display conversion.
- FrameSync read-only UI query contracts and typed request entries.
- UIManager/UIPanel alignment and page Lua migration.
- Scoreboard as Lua HUD overlay (kept per user).
- Test resources: neutral Lua fixtures; use `TestHeroRuntime.prefab` for unit
  tests; neutral test equipment items for Shop (pending user resources).

Out of scope:

- Final UI art/layout and visual acceptance (user-owned).
- Deterministic Gameplay, snapshot/checksum, network changes.
- Production heroes/abilities/equipment content.

## 8. Implementation plan

Foundation slice (done): xLua asmdefs, LuaBridge VM/host/convert, Lua core
scripts, FrameSync query contracts, skill-point request, UIManager/UIPanel Lua
wiring, foundation tests.

Next slices in dependency order:

1. UIManager API alignment to design v9.1 section 2 (pages, layers, overlay
   rules, `ShopOwnedEquipmentFocused`).
2. Page Lua scripts + flow routing per the user lobby flow; delete C# page
   controllers and runtime fallback UI.
3. HUD Lua page after prefab gap fixes/placeholders; then user acceptance.
4. Scoreboard Lua overlay.
5. Delete per-tick DTO push, `LuaDataCache`, legacy Text fallbacks; fix the
   FrameworkSmoke/ClientFrameworkSmoke map-config P0.

## 9. Public contracts

Added:

- `FrameSyncMoba.LuaBridge`: `LuaManager`, `LuaHost`, `UIRef`,
  `UIDisplayConvert`; `FrameSyncMoba.LuaBridge` now references `XLua.Runtime`.
- `FrameSyncMoba.FrameSync.FrameSyncGameRuntime`: static `Instance`,
  `RegisterActiveInstance`/`UnregisterActiveInstance`, `LocalPlayerSlot`,
  `GetLocalControlledUnit()`, `LocalEquipmentShopView`, `EquipmentShop`,
  `BindLocalPlayerSlot`.
- `FrameSyncMoba.PlayerInput`: `IPlayerGameplayCommandRequester
  .RequestAllocateAbilitySkillPoint(byte)` and implementation.
- Bootstrap `UIPanel`: `luaModule`, `refs`, `Build`, `Refresh`, `DisposeLuaHost`;
  `UIManager`: `Lua`, `Refresh(UIPageId)`.

No duplicate UID/Command/Snapshot/Aim/AbilitySignal/Checksum/FixedPoint type was
introduced.

## 10. Validation

- Compile: 0 errors (xLua example warnings only) via Unity MCP.
- EditMode: LuaBridge 21/21, Deterministic boundary 1/1, Unit boundary 3/3,
  PlayerCommandRequester 6/6.
- PlayMode: `UIManagerPrefabPlayModeTests` 1/1.
- Battle HUD tests deferred by user instruction until prefab/C# gaps are fixed.

## 11. Failure and recovery

- Each slice keeps the project compiling and focused tests green before the
  next slice starts.
- xLua remains isolated to `XLua.Runtime`/`FrameSyncMoba.LuaBridge`; any
  deterministic-assembly reference is a failure condition.
- Prefab edits go through Unity MCP; scene/prefab YAML is not hand-edited.

## 12. Results

Foundation slice completed 2026-08-02. Details in section 10. Remaining slices
are tracked in section 2 Progress.
