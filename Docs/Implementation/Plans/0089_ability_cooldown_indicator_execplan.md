# Ability Cooldown Indicator -- HUD Cooldown Display

> ExecPlan 0089 | 2026-07-24
> Design: `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` section 10
> Predecessor: 0053 (LuaBridge), Ability system (verified), CooldownDisplayController (existing)
> Conformance: Strict

## Goal

Ensure ability cooldown data flows from deterministic `AbilityHandler` through `UiSnapshotDto`/`LuaDataCache` to the Gameplay HUD. The core infrastructure already exists: `CooldownDisplayController` reads from `LuaDataCache`, and `GameBootstrap.PushUiSnapshot()` pushes cooldown data each tick. This plan verifies completeness and adds a HUD-level presenter.

## What already exists

- `AbilityHandler.GetCooldownRemainingTicks(slot, tick)` and `GetCooldownTotalTicks(slot)` -- deterministic cooldown queries.
- `UiSnapshotDto` with `CooldownRemaining0-3`, `CooldownTotal0-3` fields.
- `LuaBridge.PushTickData()` pushes cooldown data to Lua state.
- `LuaDataCache.CooldownRemaining(slot)` and `CooldownTotal(slot)` -- thread-safe access.
- `CooldownDisplayController` -- per-slot MonoBehaviour reading from LuaDataCache and driving fill images.

## What this plan adds

The existing infrastructure is functionally complete but was wired in a prior ExecPlan without explicit verification. This plan:

1. Audits the existing cooldown data pipeline.
2. Adds an `AbilityCooldownPresenter` MonoBehaviour that provides a centralized HUD-level cooldown update per Unity frame, driving the per-slot controllers.
3. Adds validation tests confirming the pipeline end-to-end.

## New types and files

| # | File | Lines | Purpose |
|---|---:|---|
| 1 | `Bootstrap/AbilityCooldownPresenter.cs` (new) | ~80 | Per-frame refresh of cooldown UI state from LuaDataCache |
| 2 | `Bootstrap/Tests/EditMode/CooldownPipelineTests.cs` (new) | ~100 | Verify cooldown data flows from AbilityHandler -> UiSnapshotDto -> LuaDataCache correctly |

## Public contract impact

None. No new public contracts; `AbilityCooldownPresenter` is internal Bootstrap detail.

## Snapshot / Checksum impact

None. Cooldown display is presentation-only.

## Design conformance checklist

- [x] Cooldown data flows from deterministic AbilityHandler to presentation (Design section 10)
- [x] No presentation state in GameplaySnapshot
- [x] Per-slot cooldown display via LuaDataCache

## Tests

- `CooldownPushReadbackTest` -- create AbilityHandler with known cooldown, push to UiSnapshotDto, verify LuaDataCache reads correctly
- `CooldownAllSlotsPopulatedTest` -- verify all 4 slots pushed with correct remaining/total values
