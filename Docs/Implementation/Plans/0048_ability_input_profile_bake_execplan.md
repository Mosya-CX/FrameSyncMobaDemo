# Plan 0048: Ability CastModelDef → Player Input Profile Bake

> Status: Completed
> Created: 2026-07-23
> Based on: `Docs/Design/moba_ability_system_design_v15_2.md`, `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md` §3, §5
> Predecessor: 0047 (A* Pathfinding)
> Parent candidate: NEXT_CANDIDATES.md Candidate B

## Purpose

Implement automatic derivation of `BakedPlayerAbilityInputProfile` from `CastModelDef` authoring data, so ability key presses use the correct input mode (PressCommit, LocalAimPrimaryCommit, PressFocusReleaseOrPrimaryCommit) per slot without hardcoded per-hero profiles.

## Observable behavior

- Hold-release abilities: press → Focus Command, release/primary-click → Commit Command
- Local-aim skillshots: press → enter local-aim mode, primary-click → Commit with AimSnapshot
- Press-commit abilities: press → immediate Commit (no aim)
- All three modes derived automatically from `CastModelDef` authoring

## Formal design documents

| Reference | Content |
|---|---|
| Ability v15.2 §3 | CastModelDef owns timing/input semantics; Kind determines signal handling |
| Player Input v1.1 §3 | Hold-release FSM: press→Focus, release→Commit, primary-click→Commit |
| Player Input v1.1 §5 | Profile derivation from CastModelDef; no duplicate config |
| Decision D-016 | Input mode derived offline from CastModelDef |
| Decision D-017 | Hold-release input mapping (Focus/Commit) |
| Decision D-018 | AI does not use player input profiles |

## Current real code paths

| File | Current state |
|---|---|
| `PlayerInput/PlayerCommandRequester.cs` | Has `IPlayerAbilityInputProfileProvider` interface; `GetProfile()` default fallback to PressCommit |
| `PlayerInput/PlayerInputController.cs` | Wires input callbacks; no profile provider injection |
| `Gameplay/Ability/CastModelDef.cs` | Has `CastModelKind` (Commit, HoldRelease, Channel, ActiveSignal); no IsHoldRelease/RequiresAim booleans |
| `Gameplay/Ability/AbilityDef.cs` | Has `CastModel` field; no explicit `AimKind` for Bake |
| `Bootstrap/GameBootstrap.cs` | Creates PlayerCommandRequester; no profile provider injection |

## In scope

1. `AbilityInputProfileBaker` — static utility reading `CastModelDef` and outputting `BakedPlayerAbilityInputProfile`
2. `AbilityInputProfileProvider` — runtime `IPlayerAbilityInputProfileProvider` implementation
3. Wire into `PlayerInputController` / `GameBootstrap` — inject provider
4. Tests: hold-release, local-aim, press-commit derivation, slot-indexed lookup, missing ability

## Out of scope

- Full AbilityDef ScriptableObject authoring inspector
- Targeting indicator rendering (skillshot arrows)
- `AimKind` auto-detection from CastModelDef (keep explicit)
- Abilities with multiple cast models per slot

## New files

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `PlayerInput/AbilityInputProfileProvider.cs` | ~90 | Runtime IPlayerAbilityInputProfileProvider |
| 2 | `PlayerInput/AbilityInputProfileBaker.cs` | ~70 | Static Bake method |
| 3 | Tests | ~60 | EditMode tests for mode derivation |

## Modified files

| File | Lines | Change |
|---|---|---|
| `PlayerInput/PlayerInputController.cs` | +30 | Inject IPlayerAbilityInputProfileProvider |
| `PlayerInput/PlayerCommandRequester.cs` | +10 | Accept profile provider in constructor |
| `Bootstrap/GameBootstrap.cs` | +30 | Create AbilityInputProfileProvider from AbilityDefinitionRegistry + Bake |
| `Gameplay/Ability/CastModelDef.cs` | +10 | Add convenience IsHoldRelease property |
| `Gameplay/Ability/AbilityDef.cs` | +10 | Add AimKind field |

## Assembly

New types in `FrameSyncMoba.PlayerInput`. Modified types in `FrameSyncMoba.Unit` and `FrameSyncMoba.Bootstrap`. No new asmdef, no cycle.

## Estimated code change

- New: ~220 lines
- Modified: ~90 lines
- **Total: ~310 lines**
