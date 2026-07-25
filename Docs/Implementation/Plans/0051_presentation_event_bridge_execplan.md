# ExecPlan 0051 — Presentation Event Bridge Hardening: SFX, Hit Reactions, Death Events

> Parent: NEXT_CANDIDATES.md Candidate C
> Created: 2026-07-23
> Design authority: `moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`, `moba_attack_module_design_v6_2.md`, `moba_combat_system_design_v13_2.md`

## Purpose

Consume the deterministic `VisualEventOutput` SFX/VFX event streams at tick-end and drive Unity presentation: play attack/ability sound effects, show hit reactions, trigger death animations. Currently events are submitted but never consumed — the game is headless.

## Observable behavior

- Attack commit → attack SFX plays at attacker's position
- Unit takes damage → brief hit-flash visual
- Unit dies → death animation trigger + death SFX
- Events deduplicated during rollback (same tick+source skipped)

## In scope

1. `PresentationEventDispatcher` MonoBehaviour — reads `VisualEventOutput.ConsumeVfxEvents/ConsumeSfxEvents` each tick-end, dispatches to registered handlers
2. `AttackSfxHandler` — plays `AudioClip` for `AttackCommit` events
3. `HitReactionPresenter` — material flash + brief scale punch on damage
4. `DeathPresenter` — triggers `Animator.SetTrigger("Death")` + death SFX
5. Wire in `GameBootstrap`: register handlers, call dispatcher each tick-end
6. Tests: EditMode event dedup, PlayMode audio playback

## Out of scope

- Final production audio assets — use procedural AudioClip or project-existing clips
- Particle system VFX — use simple GameObject instantiation
- UI health bars
- Presentation rollback (events are fire-and-forget)

## New files (~300 lines)

| File | Lines |
|---|---|
| `Bootstrap/PresentationEventDispatcher.cs` | ~100 |
| `Bootstrap/AttackSfxHandler.cs` | ~50 |
| `Bootstrap/HitReactionPresenter.cs` | ~70 |
| `Bootstrap/DeathPresenter.cs` | ~40 |
| `Bootstrap/Tests/PresentationEventTests.cs` | ~40 |

## Modified files (~60 lines)

| File | Change |
|---|---|
| `Bootstrap/GameBootstrap.cs` | +40: create dispatcher + handlers, call Consume each tick |
| `FrameSync/SimulationTickPipeline.cs` | +20: call presentation dispatch after tick |

## Public contracts

| Contract | Owner |
|---|---|
| `PresentationEventDispatcher.DispatchCurrentFrame()` | FrameSyncMoba.Bootstrap |
| `IAttackSfxHandler.Play(UnitUid, fp2 position)` | FrameSyncMoba.Bootstrap |
| `IHitReactionPresenter.OnDamageTaken(UnitUid, fp damage)` | FrameSyncMoba.Bootstrap |
| `IDeathPresenter.OnUnitDeath(UnitUid, fp2 position)` | FrameSyncMoba.Bootstrap |

## Tests

- `Presentation_Dispatch_DeduplicatesSameTickSource` (EditMode)
- `Presentation_AttackSfx_PlaysOnCommit` (PlayMode)
- `Presentation_HitReaction_FlashOnDamage` (PlayMode)
