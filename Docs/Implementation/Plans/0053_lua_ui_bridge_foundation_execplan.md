# ExecPlan 0053 — Lua UI Bridge Foundation: Health Bars, Cooldowns, Gold Display

> Parent: NEXT_CANDIDATES.md Candidate 0053
> Created: 2026-07-23
> Design authority: `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`, `moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`, `moba_equipment_shop_gold_system_design_v12.md`, `moba_ability_system_design_v15_2.md`, `unit_behavior_framework_design_v27_3.md`

## Purpose

Build the read-only bridge between deterministic simulation state and the UI layer. At each tick-end, `LuaBridge` pushes a structured `UiSnapshotDto` to a managed Lua-like runtime state, enabling `HealthBarController`, `CooldownDisplayController`, and `GoldDisplayController` to consume health, cooldown, and gold data from a single authoritative source.

## Observable behavior

- Tick-end: `GameBootstrap.PushUiSnapshot()` populates `LuaDataCache` and pushes to `LuaBridge`
- `HealthBarController` reads current/max health and drives a `Slider`
- `CooldownDisplayController` reads per-slot cooldown remaining/total and drives `Image.fillAmount`
- `GoldDisplayController` reads current gold and updates `Text`
- Lua script `ui_bootstrap.lua` in `StreamingAssets/Lua` is ready for full VM integration

## In scope

1. `LuaBridge` assembly (`FrameSyncMoba.LuaBridge`) with:
   - `LuaRuntime` — managed dictionary-based Lua-like global state
   - `LuaBridge` MonoBehaviour — tick-end consumer, pushes `UiSnapshotDto` into `LuaRuntime`
   - `UiSnapshotDto` — per-tick read-only snapshot DTO (moved from Bootstrap)
2. `Assets/StreamingAssets/Lua/ui_bootstrap.lua` — Lua entry point script
3. Migration: `UiSnapshotDto` moved from `FrameSyncMoba.Bootstrap` to `FrameSyncMoba.LuaBridge`
4. `GameBootstrap` wired to push data to `LuaBridge` after each tick
5. `Bootstrap` asmdef updated with `FrameSyncMoba.LuaBridge` reference
6. Tests: `LuaRuntimeTests`, `UiSnapshotPopulationTests`, `LuaBridgePushTests`

## Out of scope

- Real Lua VM integration (MoonSharp/XLua) — LuaRuntime is a managed stand-in
- Full UI/Lua system (shop, scoreboard, minimap)
- WatchableValue / WatchHook integration
- Lua-driven UI GameObject manipulation (controllers use C# directly)

## New files (~370 lines)

| File | Lines |
|---|---|
| `LuaBridge/FrameSyncMoba.LuaBridge.asmdef` | ~20 |
| `LuaBridge/LuaRuntime.cs` | ~100 |
| `LuaBridge/LuaBridge.cs` | ~120 |
| `LuaBridge/UiSnapshotDto.cs` | ~30 |
| `LuaBridge/Tests/FrameSyncMoba.LuaBridge.Tests.asmdef` | ~20 |
| `LuaBridge/Tests/LuaBridgeTests.cs` | ~160 |
| `StreamingAssets/Lua/ui_bootstrap.lua` | ~50 |

## Modified files (~60 lines)

| File | Change |
|---|---|
| `Bootstrap/FrameSyncMoba.Bootstrap.asmdef` | +`FrameSyncMoba.LuaBridge` reference |
| `Bootstrap/UiSnapshotDto.cs` | → type-forward stub |
| `Bootstrap/LuaDataCache.cs` | updated using to `LuaBridge` |
| `Bootstrap/GameBootstrap.cs` | +luaBridge field, +push to LuaBridge in PushUiSnapshot |

## Public contract impact

- `UiSnapshotDto` → moved to `FrameSyncMoba.LuaBridge` (was `FrameSyncMoba.Bootstrap`)
- `LuaBridge.PushTickData(int, in UiSnapshotDto, Unit)` — new public method
- `LuaBridge.PushTickDataWithBindings(...)` — new public method with binding table support
- No changes to existing public contracts in FrameSync, Unit, Physics, or Deterministic assemblies

## Snapshot / Checksum impact

None. `UiSnapshotDto` and `LuaRuntime` are presentation-only and never enter `GameplaySnapshot`, `SharedGameplayChecksum`, rollback, or any deterministic path.

## Verification

- Unity compilation: PASSED
- EditMode tests: 427/427 passed (all existing + new LuaBridge tests)
- No existing tests modified or removed

## Design conformance

Strict — no deviation from `MOBA_UI_Lua_System_Design_v9_1`.
