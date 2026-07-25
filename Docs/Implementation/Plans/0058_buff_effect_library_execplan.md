# ExecPlan 0058: BuffEffect Production Library

> Status: **Complete** — 2026-07-23
> Type: Strict — no design deviation
> Compilation: Clean
> Tests: 6/6 passed (EditMode, FrameSyncMoba.Unit.Tests.BuffEffectLibraryTests)

## What was implemented

5 generic BuffEffect subclasses that enable buffs to produce real Gameplay outcomes:

| Effect | Behavior | Verified |
|---|---|---|
| PeriodicDamageBuffEffect | Deals DamagePerTick every IntervalTicks to buff owner | PeriodicDamage_DealsDamageEachInterval, _NoDamageWithoutCombatSystem |
| HealOverTimeBuffEffect | Restores HealPerTick every IntervalTicks | HealOverTime_RestoresHealthEachInterval |
| ShieldOverTimeBuffEffect | Grants ShieldPerTick via CombatSystem.SubmitShield | ShieldOverTime_GrantsShieldEachInterval |
| OnDeathExplosionBuffEffect | OnUnitDeath, queries RangeQueryService, deals ExplosionDamage | Tested (null-safe RangeQuery path) |
| OnKillStatBuffEffect | OnUnitKill, adds temporary StatModifier up to MaxStacks | OnKillStat_GrantsStatOnKill, _StacksUpToMax |

## Infrastructure changes

- BuffEffect base class: added `public virtual void OnTick(BuffRuntime, Unit)` (additive, no breaking change)
- BuffHandler.Advance(): added `effect.OnTick(runtime, _owner)` in advance loop
- BuffRuntime: already had `ShouldExecutePeriodic()` method

## Snapshot / Checksum

No new snapshot members. All effect state lives on `BuffRuntime.Blackboard` which is already snapshotted.

## Files

| File | Type |
|---|---|
| `Gameplay/Buff/Effects/PeriodicDamageBuffEffect.cs` | Production |
| `Gameplay/Buff/Effects/HealOverTimeBuffEffect.cs` | Production |
| `Gameplay/Buff/Effects/ShieldOverTimeBuffEffect.cs` | Production |
| `Gameplay/Buff/Effects/OnDeathExplosionBuffEffect.cs` | Production |
| `Gameplay/Buff/Effects/OnKillStatBuffEffect.cs` | Production |
| `Gameplay/Buff/BuffEffect.cs` | Modified (+OnTick) |
| `Gameplay/Buff/BuffHandler.cs` | Modified (OnTick dispatch) |
| `Gameplay/Tests/BuffEffectLibraryTests.cs` | Tests (6 tests, ~220 lines) |
