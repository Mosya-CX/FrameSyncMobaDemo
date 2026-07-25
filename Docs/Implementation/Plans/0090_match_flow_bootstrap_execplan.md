# Match Flow Bootstrap -- Countdown -> Start -> End State Machine

> ExecPlan 0090 | 2026-07-24
> Design: `FrameSync_Flow_Integrated_System_Design_v10_2.md` sections 2, 14
> Predecessor: FrameSync pipeline (verified), MatchRuleRuntime (verified)
> Conformance: Strict

## Goal

Build the `MatchFlowStateMachine` wrapper that drives the overall match lifecycle: PreGame -> Countdown -> Active -> Ending -> Finished. The underlying `MatchRuleRuntime` already implements the phase state machine with `BeginCountdown()`, `AdvanceTick()`, and `EvaluateAuthorityConfirmedTick()`. This plan wraps it in a composable bootstrap interface and adds the `MatchResultSnapshot` for end-of-match data.

## What already exists

- `MatchRuleRuntime` with states: Preparing, Countdown, Running, Ending, Finished.
- `MatchPhase` enum.
- `MatchStatisticsRuntime` with KDA tracking, gold allocations, capture/restore.
- `MatchRuleRuntimeSnapshot` with all phase state.
- `BakedGlobalGameplayData` with `CountdownTicks`, `EndingDurationTicks`.
- `MatchEndReason` enum (BaseDestroyed, SimultaneousBaseDestruction).
- `GameBootstrap` calls `Runtime.MatchRule.BeginCountdown(0, config.CountdownTicks)`.

## What this plan adds

1. `MatchFlowStateMachine` -- a lightweight wrapper that integrates with GameBootstrap, driving `MatchRuleRuntime.AdvanceTick()` each tick and recording the match result.
2. `MatchResultSnapshot` -- captures winner, stats, duration at match end for Result screen consumption.
3. GameBootstrap wiring -- advance state machine per tick, gate Gameplay commands during PreGame/Countdown.

## New types and files

| # | File | Lines | Purpose |
|---|---:|---|
| 1 | `FrameSync/MatchFlowStateMachine.cs` (new) | ~150 | Wraps MatchRuleRuntime, drives AdvanceTick per tick, records MatchResultSnapshot |
| 2 | `FrameSync/MatchResultSnapshot.cs` (new) | ~60 | Serializable match result: winner, stats, duration, end reason |
| 3 | `Bootstrap/GameBootstrap.cs` (modify) | +30 | Wire MatchFlowStateMachine, advance per tick |

## Public contract impact

- `MatchFlowStateMachine` -- new public class in FrameSync assembly, composable by GameBootstrap.
- `MatchResultSnapshot` -- new public struct for Result screen consumption.
- No changes to MatchRuleRuntime public contract.

## Snapshot / Checksum impact

`MatchRuleRuntimeSnapshot` is unchanged. `MatchFlowStateMachine` does not add snapshot state -- it is a bootstrap-level wrapper.

## Design conformance checklist

- [x] Match phases: PreGame -> Countdown -> Running -> Ending -> Finished (Design section 2.2, 14.2)
- [x] Countdown transitions to Running at RunningStartTick (Design section 14)
- [x] GameOverTick set when base destroyed and authority confirmed (Design section 14.5)
- [x] FinishTick = GameOverTick + EndingDurationTicks (Design section 14)
- [x] MatchResultState captures winner, stats, duration (Design section 14.6)
- [x] Gameplay commands gated during PreGame/Countdown (Design section 14)

## Tests

- `MatchFlowPhaseTransitionTest` -- verify phase transitions: Preparing -> Countdown -> Running -> Ending -> Finished
- `MatchFlowCountdownTest` -- verify Running starts at correct tick after countdown
- `MatchResultSnapshotTest` -- verify snapshot captures winner and stats correctly
