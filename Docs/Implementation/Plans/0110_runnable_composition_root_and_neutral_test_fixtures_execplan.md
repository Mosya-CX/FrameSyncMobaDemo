# ExecPlan 0110: Runnable composition root and neutral test fixtures

> Status: Implemented and MCP-validated; focused Test Runner pending dirty-scene resolution.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 1.
> Design conformance: Strict -- no deviation.
> Estimated production/test code change: 700-1200 lines.

## Purpose

Create the smallest runnable generic Unity composition proving existing Unit
contracts can be authored in the Inspector, Baked to deterministic runtime
values, spawned through authoritative prototype/prefab tables and advanced by
bounded Logic Ticks.

## Observable production behavior

A neutral smoke scene loads frozen configuration, resolves stable prototype and
prefab IDs, creates a prefab-authored Unit/Handler GameObject through
`UnitWorld`, and advances deterministic Logic Ticks. Primitive geometry is a
framework fixture only.

## Progress

- [x] Read current designs, decisions, parent plan and composition code.
- [x] Confirm Unity MCP compilation/Console baseline is clean.
- [x] Add Inspector-friendly Unit/stat authoring and deterministic Bake.
- [x] Compose the frozen catalog into `GameBootstrap`.
- [x] Add the bounded application-layer Logic Tick driver.
- [x] Create neutral prefab/config/smoke assets through Unity MCP.
- [x] Add focused EditMode and PlayMode behavior tests.
- [x] Compile, inspect Console and run direct MCP behavior validation.
- [ ] Run the focused Test Runner classes after `ClientBootstrap` is saved.

## Current repository context

- `GameBootstrap` constructs empty Unit prototype and stat tables.
- `GlobalPrefabTable.asset` has no entries.
- No production code calls `FrameSyncGameRuntime.ExecuteOneTick`.
- Unit and Handlers already are prefab-authored MonoBehaviours and remain so.
- RuntimeConfig cannot depend on Unit without reversing assembly direction;
  Unit-owned authoring therefore lives in Unit and is referenced by Bootstrap.
- Dirty `ClientBootstrap.unity` will not be overwritten.

## Exact design sources

- `Docs/Design/unit_behavior_framework_design_v27_3.md`: 1.6, 7, 8.2-8.3.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`: 8 and 14.
- `Docs/Architecture/DECISION_LOG.md`: D-019 through D-021.

## In scope

- Unit-owned ScriptableObject catalog with float authoring and strict Bake into
  existing stat/prototype/profile runtime contracts.
- Duplicate/invalid ID, fixed-point input and missing-reference failures.
- Bootstrap catalog/prefab composition.
- Thin application tick driver with a non-authoritative Unity-frame accumulator.
- Neutral primitive Unit prefab/config/smoke assets.
- Focused Bake, resolution, prefab composition and Tick advancement tests.

## Out of scope

- Snapshot/checksum repair (Gate 2), Command/gold/match flow (Gate 3), and later
  Attack, Projectile, Ability, movement, AI, Presentation or network work.
- Production heroes, abilities, Buffs, equipment, units or map data.
- New packages or parallel runtime/protocol types.

## Affected assemblies

- `FrameSyncMoba.Unit`: Unit/stat authoring and Bake.
- `FrameSyncMoba.Bootstrap`: scene composition and scheduling.
- Relevant EditMode/PlayMode test assemblies.

## Exact production types

- New `UnitRuntimeCatalogAsset` and serializable authoring records.
- Existing `GlobalUnitPrototypeTable`, `StatDefinitionTable`, `UnitPrototype`,
  `StatPreset`, `LocomotionProfile`, `PhysicsProfile2D`, `GlobalPrefabTable`,
  `GameBootstrap` and `FrameSyncGameRuntime`.

## Public contracts

No UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint or runtime
DTO is added. The catalog is static authoring, not a Gameplay protocol; it Bakes
to existing runtime table types.

## Ownership and dependency direction

```text
UnitRuntimeCatalogAsset -> existing Unit/stat runtime tables
GlobalGameplayData / GlobalPrefabTable -> frozen global configuration
GameBootstrap / tick driver -> Bake, compose, call existing ExecuteOneTick
```

Gameplay never reads ScriptableObjects, Unity frame time, Transform authority or
scene hierarchy order during a Logic Tick.

## Deterministic ordering

- Bake sorts definitions by `StatId` and prototypes by `UnitPrototypeId`.
- Duplicate stable IDs and duplicate prototype stat entries fail.
- Runtime Unit iteration remains ascending `UnitUid`.
- Unity accumulator selects a tick count only; Gameplay never consumes its time.

## Snapshot and serialization impact

No GameplaySnapshot change. Authored floats convert once to `fp` during Bake;
frozen static tables are not snapshot state. Gate 2 owns snapshot repair.

## Implementation steps

1. Add float authoring structures and validate finite/range constraints.
2. Bake in stable order to existing runtime types and make duplicates fail.
3. Add required catalog references to `GameBootstrap`.
4. Expose baked `MaxLogicTicksPerUnityFrame` for scheduling.
5. Add a bounded accumulator calling the existing Tick entry point.
6. Use Unity MCP to create neutral prefab/config/smoke assets and wire stable IDs.
7. Leave dirty `ClientBootstrap` untouched.

## EditMode tests

- Float authoring Bakes to expected `fp`.
- Output is insertion-order independent.
- Duplicate/zero IDs, duplicate preset stats and invalid values fail.
- Missing prefab/stat/prototype resolution fails visibly.

## PlayMode tests

- Neutral prefab has Unit and required Handler components.
- Spawn resolves prototype to prefab and registers stable Unit/Physics identity.
- Requested Logic Ticks advance `CurrentTick` exactly once.
- Render Transform is not read back as authoritative state.

## Unity MCP validation

Refresh and compile, inspect Console and assets, then run only the focused
EditMode and PlayMode smoke tests.

## Failure conditions

- A current-design public contract conflict is found.
- Composition needs a later-gate protocol change or a new package.
- The dirty scene cannot be preserved while creating an independent smoke scene.

## Completion criteria

- Neutral composition Bakes, spawns and advances deterministic Ticks.
- Invalid static configuration fails visibly.
- Unity compiles, focused tests pass and no duplicate protocol/map is introduced.
- Parent plan, status, repository map where structural, and Results are current.

## Production-content exclusion

All assets use neutral names and primitive geometry and contain no production content.

## Results

Implemented on 2026-07-26.

- Added `UnitRuntimeCatalogAsset` with float Inspector authoring, finite/range
  validation, stable sorting and Bake into existing Unit/stat runtime types.
- `StatDefinitionTable.Add` now rejects duplicates instead of overwriting them.
- Unit spawn now applies the prototype `HandlerLoadout`, fixed-point movement
  speed and logical Physics shape/forward.
- `GameBootstrap` consumes the frozen catalog, queues explicitly ordered Tick-0
  spawns and advances via a bounded unscaled-time accumulator.
- Added neutral prefab/config/smoke assets through Unity MCP. MCP readback caught
  and corrected sparse-enum serialization, then verified every formal StatId.
- Added five focused Unit catalog tests, one Bootstrap EditMode integration test
  and one PlayMode smoke-scene test.
- Unity MCP compilation is clean. Direct Editor validation executed the actual
  assets through four Logic Ticks and verified one stable UID, Circle radius
  `0.5`, move speed `3.5`, serialized scene references and Build Settings.
- `tests-run` was attempted and refused solely because the pre-existing open
  `ClientBootstrap` scene is dirty. It was not saved or overwritten.

Approximate production/test code added or changed: 1,086 lines, within the
planned 700-1200 range. Remaining non-blocking limitations: Point/Circle-only
scalar Physics authoring and current all-Handler root composition.
