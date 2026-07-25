# ExecPlan 0053 — Lua UI Bridge Foundation: Health Bars, Cooldowns, Gold Display

> Parent: NEXT_CANDIDATES.md Candidate 0053
> Created: 2026-07-23
> Design authority: `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` §HUD, §5, §13; `moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`; `moba_equipment_shop_gold_system_design_v12.md`

## Purpose

Build a minimal read-only bridge between deterministic simulation state and Lua-driven Unity UI. Each tick-end, a `LuaUiBridgeBase` pushes a `UiSnapshotDto` to Lua, and controller MonoBehaviours consume the data to update health bars, cooldown indicators, and gold display.

## Observable behavior

- Unit takes damage → health bar slider updates on next tick-end
- Ability enters cooldown → cooldown indicator shows remaining fill
- Gold income confirmed → gold counter text updates
- Lua globals receive structured snapshot data each tick

## In scope

1. `LuaUiBridgeBase` abstract MonoBehaviour (Bootstrap asmdef) — defines `PushTickData(in UiSnapshotDto)` contract.
2. `XLuaUiBridge` concrete impl (Assembly-CSharp, `Assets/Scripts/LuaBridge/`) — wraps XLua `LuaEnv`, pushes DTO fields as Lua globals.
3. `UiSnapshotDto` struct (Bootstrap asmdef) — read-only per-tick snapshot: `CurrentHealth`, `MaxHealth`, `CurrentGold`, `CooldownRemaining[4]`.
4. `UIBindingTable` ScriptableObject (Bootstrap asmdef) — maps Gameplay field paths to Lua global variable path strings.
5. `HealthBarController` MonoBehaviour (Bootstrap asmdef) — reads Lua globals, updates `Slider.fillAmount`.
6. `CooldownDisplayController` MonoBehaviour (Bootstrap asmdef) — reads Lua globals per slot, updates `Image.fillAmount`.
7. `GoldDisplayController` MonoBehaviour (Bootstrap asmdef) — reads Lua global, updates `TMP_Text` / `Text`.
8. Lua script: `Assets/StreamingAssets/Lua/ui_bootstrap.lua` — consumed by XLua, responds to pushed data.
9. Wire in `GameBootstrap`: create bridge, populate DTO from StatHandler/GoldIncomeRuntime/AbilityHandler, call `PushTickData` after TickCompleted.
10. Tests: EditMode DTO population, PlayMode health bar update.

## Out of scope

- Full Lua UI framework (deferred)
- Shop UI, minimap, scoreboard (deferred)
- Final visual design (use Unity UI Slider/Image/Text)
- Custom per-unit UI layouts
- Lua-side business logic — pure data display

## New files (~400 lines production + ~150 lines test)

| File | Lines | Assembly |
|---|---|---|
| `Bootstrap/LuaUiBridgeBase.cs` | ~30 | FrameSyncMoba.Bootstrap |
| `Bootstrap/UiSnapshotDto.cs` | ~25 | FrameSyncMoba.Bootstrap |
| `Bootstrap/UIBindingTable.cs` | ~30 | FrameSyncMoba.Bootstrap |
| `Bootstrap/HealthBarController.cs` | ~60 | FrameSyncMoba.Bootstrap |
| `Bootstrap/CooldownDisplayController.cs` | ~60 | FrameSyncMoba.Bootstrap |
| `Bootstrap/GoldDisplayController.cs` | ~40 | FrameSyncMoba.Bootstrap |
| `LuaBridge/XLuaUiBridge.cs` | ~70 | Assembly-CSharp (no asmdef) |
| `StreamingAssets/Lua/ui_bootstrap.lua` | ~85 | — |
| `Bootstrap/Tests/UiBridgeTests.cs` | ~80 | FrameSyncMoba.Bootstrap.Tests |
| `Bootstrap/Tests/PlayMode/UiBridgePlayModeTests.cs` | ~70 | FrameSyncMoba.Bootstrap.PlayModeTests |

## Modified files (~60 lines)

| File | Change |
|---|---|
| `Bootstrap/GameBootstrap.cs` | +60: create XLuaUiBridge, populate UiSnapshotDto, call PushTickData after TickCompleted |

## Public contracts

| Contract | Owner |
|---|---|
| `LuaUiBridgeBase.PushTickData(in UiSnapshotDto)` | FrameSyncMoba.Bootstrap |
| `UiSnapshotDto` struct (read-only, presentation-only) | FrameSyncMoba.Bootstrap |
| `UIBindingTable` ScriptableObject | FrameSyncMoba.Bootstrap |

## Snapshot / Serialization / Checksum

- `UiSnapshotDto` is presentation-only. Does NOT enter `GameplaySnapshot`, `SharedGameplayChecksum`, or any deterministic serialization.
- Lua global state is not restored during rollback (consistent with AGENTS.md input-local state rule).

## Assembly strategy

- Abstract `LuaUiBridgeBase` in Bootstrap asmdef — defines the contract.
- Concrete `XLuaUiBridge` in Assembly-CSharp (no asmdef) — can reference XLua types directly.
- Bootstrap references `LuaUiBridgeBase` via `[SerializeField]`; Unity injects the concrete impl.
- No asmdef modification needed.

## Tests

- `UiSnapshotDto_PopulatedFromStatHandler` (EditMode) — DTO fields match source data.
- `UiSnapshotDto_PopulatedFromGoldIncome` (EditMode) — `CurrentGold` matches `GoldIncomeRuntime`.
- `UIBindingTable_MissingBinding_ReturnsError` (EditMode) — validation detects missing entries.
- `HealthBar_UpdatesOnDamage` (PlayMode) — unit takes damage, slider updates next frame.

## Design conformance

Strict — no deviation. `UiSnapshotDto` is read-only, presentation-only, not in checksum. Gold read via `CurrentAvailableGold` (derived, read-only). Lua interop at presentation boundary only.
