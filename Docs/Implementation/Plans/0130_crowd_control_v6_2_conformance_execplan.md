# ExecPlan 0130: Crowd Control v6.2 design conformance

> Status: Slices 1-3 complete, Slice 4 (tests/docs) in progress (2026-08-06).

## Purpose

Rebuild the Crowd Control system to match
`Docs/Design/moba_crowd_control_system_design_v6_2.md`: replace the legacy
Kind-branch handler (`CrowdControlType` enum + `CrowdControlConstraint`) with
the module-executor architecture (Definition + Tags + Key Params + signals +
immunity/unstoppable/cleanse + forced behavior + unique forced move).

## Scope

In scope:

- Runtime core types: `CrowdControlId`, tags/query, signals, param keys/layout/
  block/writer, `CrowdControlInstance`, `CrowdControlDefinition` (SO with
  authoring + baked fields), definition registry, catalog asset, module
  executor table + standard modules, `ControlAccumulator`, module commands.
- `CrowdControlHandler` rewrite (Add/Remove/RemoveAll/Cleanse/AddImmunity/
  AddUnstoppable/Advance/OnDamageTaken/OnOwnerActionStarted/
  OnForcedMoveFinished/query/rollback; lifecycle ID semantics per v6.2).
- Unit-framework integration: `UnitEventBus` damage-taken route, capability/
  arbiter/planner reads of `CrowdControlStateView` and
  `TryGetBehaviorOverride`, movement gate reads.
- Caller migration: `StunStageDef`, `PullStageDef`, `ProjectileOnHitEffect`,
  `StatHandler` spell-shield immunity contract, `GameplayInputGate`.
- Snapshot/checksum: new `CrowdControlHandlerSnapshot` members (signals);
  GameplaySnapshot schema bump.
- Editor: `CrowdControlDefinitionBaker` + validation; standard definitions
  (Stun/Root/Slow/Silence/Disarm/KnockBack/Suppression/Sleep/Drowsy/Taunt/
  Charm/Fear) as framework authoring examples.
- Tests: rewrite movement/CC tests to the new API; add Definition/Bake/params/
  immunity/unstoppable/cleanse/signal/tenacity/forced-move/rollback coverage.

Out of scope: UI/icon/localization mapping for controls, production hero
content, VFX/audio, minimap, Balance values.

## Design-to-project mappings (recorded decisions)

- Global `GameplayConfig` singleton -> `UnitWorld.CrowdControlDefinitions`
  (`CrowdControlDefinitionRegistry`) populated from `CrowdControlCatalogAsset`
  at bootstrap (project catalog pattern, cf. BuffCatalogAsset).
- `StableStringId32` -> explicit stable `ControlParamKeys` constants plus an
  editor-side string->key registry with collision/uniqueness validation; no
  runtime string or hash lookup.
- `FixedBytes64` -> project-owned 64-byte block (8 x ulong, explicit byte
  offsets, little-endian typed read/write; `fp` stored via `fp.RawValue`,
  `UnitUid` via canonical fields).
- Suppression Intensity: `High` (not cleansable/immunable), matching the
  pre-refactor behavior; configurable per definition.

## Slices

1. Runtime core types + params + definition + registry + modules (additive
   rewrite of `CrowdControlTypes.cs`).
2. Handler rewrite + snapshot/checksum + caller migration + unit integration.
3. Editor baker + catalog asset + standard definitions + bootstrap wiring.
4. Tests + full EditMode/PlayMode regression + docs (DECISION_LOG D-036,
   MODULE_STATUS).
