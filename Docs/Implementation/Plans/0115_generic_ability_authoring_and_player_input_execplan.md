# 0115 Generic Ability Authoring and PlayerInput

> Status: Complete. Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 6.
> Estimated code change: 900-1800 lines.

## Purpose

Make neutral Inspector-authored abilities Bake into the runtime registry, obey
formal start/aim/cost timing rules, expose read-only session state to local
input, and fail invalid stage configuration visibly.

## Sources

- `Docs/Design/moba_ability_system_design_v15_2.md`
- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md`
- `Docs/Design/unit_behavior_framework_design_v27_3.md`

## Scope and invariants

- Formal level values, resource/health cost and `CostTiming`.
- Basic deterministic aim/range checks and extensible cast conditions.
- Strict Stage/registry Bake; no placeholder success or swallowed exception.
- Runtime Ability catalog/loadout composition and `ILocalAbilityRuntimeView`.
- Input profiles/indicators derive from the active `AbilityDef`.

No production ability/content, no new Ability signal or Aim protocol, and no
input-local state in snapshots/checksums.

## Validation

Focused EditMode checks cover failed start/no cost, both cost timings,
health/resource level values, invalid stage/duplicate registration, hold-release
session state and runtime-view/profile derivation. Unity MCP compilation and
Console inspection are required; PlayMode is only needed if Input callbacks or
scene references are changed.

## Progress

- [x] Inspected current design, code, composition and compile baseline.
- [x] Implement and run focused validation.

## Result

Formal cost timing, strict authored stages/catalog/loadouts, hold-release state,
read-only input profiles and neutral fixtures are integrated. Unity compiled
without Console errors; eight focused behavior checks and serialized-reference
reload validation passed. No production ability content was added.
