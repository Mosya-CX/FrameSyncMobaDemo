# ExecPlan 0034 — Ability Command Dispatch

> **Design authority**: `Docs/Design/moba_ability_system_design_v15_2.md`
> **Estimated code**: ~200 lines
> **Dependencies**: PlayerInput (0031), GameplayCommand extensions (0031), AbilityHandler

## Rationale

CastAbility/CancelAbility GameplayCommands are produced by PlayerCommandRequester (0031) but SimulationTickPipeline.DispatchCommand silently drops them. AbilityHandler.HandleSignal already implements the full signal-to-session pipeline. The gap is purely routing + Cancel handling.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| — | — | No new files needed |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/SimulationTickPipeline.cs` | +25 | DispatchCommand: add CastAbility/CancelAbility case routing to AbilityHandler.HandleSignal |
| `Unit/Ability/AbilityHandler.cs` | +35 | Handle Cancel signal: interrupt active session, clear hold-release state |
| `Unit/Ability/AbilityRuntime.cs` | +20 | CancelSession method: interrupt without full cooldown |

## Key conformance

- Ability v15.2: Focus/Commit/Cancel signal language
- Player Input v1.1: Hold-release FSM produces Focus→Commit→Cancel
- CC v6.2: CrowdControlHandler gating already present in AbilityHandler
- Cancel: right-click does not cancel hold-release; only explicit CancelAbility command does
