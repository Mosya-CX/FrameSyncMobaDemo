# Scoreboard + Minimap UI -- Gameplay HUD Elements

> ExecPlan 0087 | 2026-07-24
> Design: `MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` sections 4-5, 10
> Predecessor: 0053 (LuaBridge), MatchRuleRuntime (verified)
> Conformance: Strict

## Goal

Build two Gameplay HUD elements: scoreboard (K/D/A per player) and minimap (colored dots for units). Both read only deterministic state via existing LuaBridge/LuaDataCache infrastructure. No new deterministic state.

## What already exists

- `UiSnapshotDto` with `PlayerCount`, `Kills`, `Deaths`, `Assists`, `AllPlayerKills[]`, etc. -- already populated by GameBootstrap.
- `LuaBridge.PushTickData()` pushes all scoreboard arrays.
- `LuaDataCache` provides thread-safe access to latest UiSnapshotDto.
- `UnitWorld.GetAllUnits()` for unit position queries.
- `GoldDisplayController`, `CooldownDisplayController` -- established pattern for simple UI controllers.

## New types and files

### Production code (~280 lines)

| # | File | Assembly | Lines | Purpose |
|---|---:|---|---|---|
| 1 | `Bootstrap/ScoreboardController.cs` | `FrameSyncMoba.Bootstrap` | ~130 | Reads KDA from LuaDataCache, renders scoreboard rows with hero names, kills, deaths, assists |
| 2 | `Bootstrap/MinimapController.cs` | `FrameSyncMoba.Bootstrap` | ~100 | Reads unit positions from UnitWorld, renders colored dots on RawImage texture |
| 3 | `Bootstrap/GameBootstrap.cs` (modify) | `FrameSyncMoba.Bootstrap` | +50 | Wire ScoreboardController, MinimapController, populate Scoreboard DTO fields |

## Public contract impact

- `ScoreboardController` -- new public MonoBehaviour, Presentation-only.
- `MinimapController` -- new public MonoBehaviour, Presentation-only.
- No changes to deterministic types, snapshot, or checksum.

## Snapshot / Checksum impact

None. Both controllers are presentation-only.

## Design conformance checklist

- [x] Scoreboard reads `MatchStatisticsRuntime` via `UiSnapshotDto` (Design sections 1.4, 10)
- [x] Minimap reads `UnitWorld` positions via `GetAllUnits()` (Physics v13.1)
- [x] No new deterministic state, no snapshot membership
- [x] Presentation-only consumption pattern

## Tests

Three focused tests:

1. `ScoreboardDataReadbackTest` -- simulate KDA data via UiSnapshotDto, verify ScoreboardController reads correctly
2. `MinimapPositionMappingTest` -- given known unit positions, verify minimap dot placement
3. `ScoreboardMinimapLifecycleTest` -- controller Show/Hide lifecycle
