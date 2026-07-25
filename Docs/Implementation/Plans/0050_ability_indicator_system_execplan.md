# ExecPlan 0050 — Ability Indicator System: Ground Targeting & Range Display

> Parent: NEXT_CANDIDATES.md Candidate A
> Created: 2026-07-23
> Design authority: `moba_ability_system_design_v15_2.md`, `MOBA_Player_Input_Command_Module_Design_v1_1.md` §3, `moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`

## Purpose

Provide runtime visual indicators for ability aiming. When the player enters `LocalAimPrimaryCommit` mode (skillshot/local-aim abilities), the indicator renders on the ground showing range, direction, or target area. The indicator deactivates on Commit or Cancel.

## Observable behavior

- Press Q (LocalAim ability) → ground indicator appears at caster position
- Move mouse → indicator updates direction/position in real-time
- Primary click → indicator hides, Commit command is sent
- Cancel/right-click → indicator hides, aim is cancelled
- Different AimKind values produce different indicator shapes

## In scope

1. `SkillIndicatorDriver` MonoBehaviour — owns indicator pool, lifecycle (Show/Hide/Update)
2. `DirectionIndicator` — arrow from caster toward cursor, length = cast range
3. `RangeCircleIndicator` — circle at max cast range
4. `GroundTargetIndicator` — cursor-position marker
5. Integration: `PlayerInputController` activates indicator on LocalAiming, updates on frame, deactivates on Commit/Cancel
6. Tests: EditMode + PlayMode indicator lifecycle

## Out of scope

- Custom per-hero indicator art — use simple quad GameObjects
- Target-filtering visual feedback (enemy-only highlighting)
- Multi-stage indicator interactions

## New files (~300 lines)

| File | Lines |
|---|---|
| `PlayerInput/SkillIndicatorDriver.cs` | ~140 |
| `PlayerInput/Tests/IndicatorTests.cs` | ~80 |
| `Bootstrap/Resources/IndicatorArrow.prefab` | ~40 (simple quad) |
| `Bootstrap/Resources/IndicatorCircle.prefab` | ~40 |

## Modified files (~120 lines)

| File | Change |
|---|---|
| `PlayerInput/PlayerInputController.cs` | +60: wire indicator activate/update/deactivate |
| `PlayerInput/PlayerCommandRequester.cs` | +20: expose aiming state query |
| `Bootstrap/GameBootstrap.cs` | +40: create SkillIndicatorDriver, inject |

## Public contracts

| Contract | Owner |
|---|---|
| `SkillIndicatorDriver.Show(aimKind, range, caster)` | FrameSyncMoba.PlayerInput |
| `SkillIndicatorDriver.Hide()` | FrameSyncMoba.PlayerInput |
| `SkillIndicatorDriver.UpdateCursor(fp2 worldPos, fp2 casterPos, fp2 facing)` | FrameSyncMoba.PlayerInput |

## Tests

- `Indicator_ShowDirection_RendersArrow` (PlayMode)
- `Indicator_ShowRangeCircle_RendersCircle` (PlayMode)
- `Indicator_Hide_DeactivatesAll` (PlayMode)
- `Indicator_Update_ChangesArrowDirection` (PlayMode)
