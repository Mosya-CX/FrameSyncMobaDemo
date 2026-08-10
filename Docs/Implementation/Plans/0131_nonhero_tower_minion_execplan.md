# ExecPlan 0131: Tower mechanics + minion built-in buffs + two-config split

> Status: Completed on 2026-08-07.

## Purpose

Implement defensive-tower and lane-minion mechanics per the user's LoL-values
document plus `moba_non_hero_unit_modules_design_v5.md`, with data-driven
built-in buffs (no hard-coded per-unit branches) and a separate formal config.

## Changes

- BuffHandler: `SetInitialBuffConfigs` / `ApplyInitialBuffs`; UnitPrototype +
  authoring gain `InitialBuffConfigIds`; UnitWorld applies at spawn.
- CombatModifier: target-UnitKind filter + TargetCurrentHealth operand.
- Buffs (test config): MinionMuncher / MinionPincushion / TowerPillow.
- TowerAttackHandler reworked as an AttackHandler subclass: hero damage ramp
  (180 -> x1.5/hit, cap 600) via projectile on-hit override; in-flight
  projectile locking; AttackSnapshot ramp/lock members + checksum.
- TowerAIController keeps locked target while a shot is unresolved (v5 8.5).
- TowerTargetLinePresenter (presentation red line); test + formal tower
  prefabs migrated.
- Formal config split: Assets/Config/Formal/ FormalUnitRuntimeCatalog +
  FormalGlobalPrefabTable (real Resources/Prefab/Unit prefabs).

## Validation

- EditMode: MinionInitialBuffTests 4/4, TowerAttackHandlerTests 3/3,
  FrameSync 71/71, Bootstrap 58/58; compile clean.
- Remaining external gate: packaged/PlayMode full-match re-validation with the
  migrated tower prefabs.
