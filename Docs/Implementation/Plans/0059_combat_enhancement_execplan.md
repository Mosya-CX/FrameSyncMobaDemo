# ExecPlan 0059: Combat Enhancement — Critical Strike + Attack Speed

> Status: **Complete** — 2026-07-23
> Type: Strict — no design deviation
> Compilation: Clean
> Tests: 6/6 passed (EditMode, FrameSyncMoba.Unit.Tests.CombatEnhancementTests)

## What was implemented

### Critical Strike
- CombatSystem.ProcessDamage integrates crit roll via `DeterministicRandomService.Chance01`
- Two new StatId entries already existed: `CriticalStrikeChance` (14), `CriticalStrikeDamage` (15)
- `DamageEventData` gained `IsCritical` field
- Crit multiplier defaults to 2.0 when `CriticalStrikeDamage <= 0`
- Only triggers when `UnitWorld.RandomService != null`

### Attack Speed
- `AttackHandler.GetAttackSpeed()` reads `StatId.AttackSpeed` from StatHandler
- Attack cooldown is scaled: `BaseCooldown / AttackSpeed`
- StatId.AttackSpeed (9) already existed in the stat enum

### Infrastructure
- `DeterministicRandomService` wired in FrameSyncGameRuntime constructor
- `RandomSeed` added to `GameModeConfigAuthoring` and `BakedGlobalGameplayData`
- `UnitWorld.RandomService` and `SimulationTickPipeline.RandomService` both assigned
- Random state captured in `GameplaySnapshot`

## On-Hit Pipeline

Deferred. The on-hit effect pipeline (OnHitEventData, CombatEvents.RaiseOnHit, BuffEffect.OnHitDealt) was skipped per the candidate's note that on-hit was lower priority.

## Files

| File | Type |
|---|---|
| `Combat/CombatSystem.cs` | Modified (+crit logic, +IsCritical in DamageEventData) |
| `Combat/CombatEvents.cs` | Modified (+IsCritical field) |
| `FrameSync/FrameSyncGameRuntime.cs` | Modified (+RandomService creation/wiring) |
| `RuntimeConfig/GlobalGameplayData.cs` | Modified (+RandomSeed field) |
| `Gameplay/Presentation/SfxEvent.cs` | Modified (+WorldPosition field) |
| `Gameplay/Tests/CombatEnhancementTests.cs` | Tests (6 tests, ~180 lines) |
| `FrameSync/Tests/FrameSyncPipelineTests.cs` | Modified (+randomSeed parameter) |
