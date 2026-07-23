# ExecPlan 0046 — Unit MonoBehaviour and prefab composition

> Status: Approved — executing.  
> Status override: Superseded; incorporated into `0046_full_design_conformance_recovery_execplan.md`. Do not execute independently.  
> Created: 2026-07-22.  
> Approval source: the owner explicitly selected the formal MonoBehaviour Unit/Handler architecture and requested immediate project correction before the next candidate round.

The owner subsequently clarified that this execution round must correct every previously audited P0/P1 design-conformance issue before candidates are prepared. This narrower document is retained as a historical component plan; its work and evidence are tracked by the formal recovery plan.

## 1. Purpose

Make Unit prefabs the authoring and runtime composition source. `Unit` and every current Unit Handler become Unity components with Inspector-visible references/configuration; `UnitWorld.SpawnUnit(in UnitSpawnRequest)` resolves a stable prefab ID, instantiates the configured prefab, binds deterministic runtime identity/state, registers Physics, and returns the new `UnitUid`. No parallel Unit-to-GameObject mapping is introduced.

## 2. Progress

- [x] Owner selected MonoBehaviour composition over the prior pure-C# variance.
- [x] Read the handoff protocol, current designs, current code, tests, asmdefs, and Unity baseline.
- [ ] Add the shared prefab-table assembly and formal minimum contracts.
- [ ] Convert Unit and the seven current Handlers to component-owned composition.
- [ ] Replace legacy constructor/spawn setup and add component test fixtures.
- [ ] Correct D-009 lifecycle and Unit Physics identity while touching composition.
- [ ] Compile, inspect Console, and run focused EditMode/PlayMode validation.
- [ ] Update status, repository map, handoff, and Results.
- [ ] Create exactly three next-number Candidate ExecPlans and stop.

## 3. Surprises and discoveries

- The current Unit assembly is `noEngineReferences=true`, and Unit/Handlers are constructed with `new`; this directly blocks prefab/Inspector composition.
- The formal design already freezes `UnitHandler : MonoBehaviour`, owner binding, component lifecycle methods, `UnitSpawnRequest`, `PrefabKind`, and GlobalPrefabTable lookup.
- Current tests construct many Units and Handlers directly. A test-only component factory is required; production must not retain a legacy constructor or test-only spawn protocol.
- No project GlobalPrefabTable or Unit prefab exists. This slice implements the minimum formal table contract and proves transient prefab instantiation; authoring/Bake tooling and project assets remain a separate candidate.
- Existing Unit/FrameSync test failures predate this migration. This slice fixes only failures caused by component composition, Tick setup, D-009, and Physics identity.

## 4. Decision log

- Follow the owner and Unit v27.3: `Unit`, `StatHandler`, `MovementHandler`, `AttackHandler`, `AbilityHandler`, `BuffHandler`, `CrowdControlHandler`, and `EquipmentHandler` are MonoBehaviours.
- Add a low-level `FrameSyncMoba.RuntimeConfig` assembly for the formal global prefab contract so Unit does not own all prefab semantics and no dependency cycle is created.
- Keep runtime collections, snapshots, deterministic math, EventBus, CombatModifierSet, AI, and locomotion services as plain C# where Inspector composition adds no value.
- Convert Inspector floats once in component initialization; authoritative runtime remains `fp`.
- Replace the legacy spawn overload rather than keeping two public spawn protocols.
- Do not create production content or a hero-specific prefab. A generic project authoring asset/prefab is deferred to a candidate after the runtime contract is verified.

## 5. Current repository context

Affected assemblies are Deterministic, Physics, Unit, FrameSync, the Unit/FrameSync tests, and a new lower-level RuntimeConfig assembly. Current dependency direction is Deterministic/Physics → Unit → FrameSync → PlayerInput. The new direction is RuntimeConfig → Unit; RuntimeConfig references UnityEngine only and no Gameplay module.

Primary files are `Unit.cs`, `UnitWorld.cs`, every current Handler, the Unit asmdef, `FrameSyncGameRuntime.cs`, Unit/FrameSync test fixtures, and new prefab-contract/component tests. `GameScene` remains empty; no existing production prefab or ScriptableObject is modified.

## 6. Design sources

- `Docs/Design/unit_behavior_framework_design_v27_3.md`: sections 1.2, 1.11, 4.1–4.2, 7.2–7.4, 7.10–7.11.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`: sections 17.4–17.6 (`PrefabKind`, `GlobalPrefabTable`, Inspector requirements).
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`: query mirror and synchronous Unit registration.
- `Docs/Architecture/DECISION_LOG.md`: D-008, D-009, D-022, D-023, D-024.
- `E:/EgdeDownLoad/Unity_MOBA_Compact_AI_Workflow.md`: approved execution/validation/candidate loop.

## 7. Scope

### In scope

- `UnitHandler : MonoBehaviour` with formal owner binding and lifecycle seams.
- MonoBehaviour conversion for Unit and all seven current Handler types.
- Serialized private Unit component references with Inspector grouping, automatic local resolution, and deterministic validation.
- Minimum formal `PrefabKind`, `PrefabEntry`, `PrefabGroup`, and `GlobalPrefabTable` lookup/validation contract in a new assembly.
- Formal `UnitSpawnRequest`/`UnitSpawnReason` and synchronous prefab-based UnitWorld spawn.
- Runtime initialization of existing Stat, movement, attack, ability, Buff, CC, equipment, XP, EventBus, CombatModifier, Physics, and registry state.
- Lossless UnitUid/team Physics query binding and D-009 lifecycle correction.
- Test-only GameObject/prefab builders; component, invalid-prefab, stable-identity, and PlayMode lifecycle tests.
- Required asmdef references and status/handoff documentation.

### Out of scope

- Full custom GlobalPrefabTable Inspector, folder import, ID allocation, Bake products, production table assets, or production Unit prefabs.
- HandlerLoadout, Unit pool, despawn, restore topology, or complete lifecycle service.
- Command/Aim/Input, aggregate Snapshot, Checksum, Combat ordering, Projectile UID, Ability/Buff/CC functional completion, pathfinding, presentation, UI, or content.
- New packages or changes to current formal design documents.

## 8. Implementation plan

1. Add RuntimeConfig asmdef and the smallest formal prefab table with stable kind/ID validation and required lookup.
2. Add `UnitHandler`; convert all Handler ownership from constructors/private owner fields to component binding and explicit runtime initialization.
3. Convert Unit into the prefab root component, serialize Handler/Physics references, validate duplicates/missing required components, and preserve deterministic identity as runtime-only state.
4. Add UnitSpawnRequest; refactor UnitWorld to resolve prototype and prefab tables, instantiate, initialize, set Physics pose/query metadata, register, then expose the Unit by UID.
5. Correct FrameSync's explicit binding path and normal-death modifier ownership.
6. Add a test-only factory/extension so old pure logic fixtures create real GameObjects without adding a production compatibility protocol.
7. Add focused EditMode and PlayMode component tests, compile, and repair only migration-scope failures.
8. Review public types/dependencies, update documents, then create 0047A/B/C candidates.

## 9. Public contracts

Added formal contracts: `UnitHandler`, `UnitSpawnRequest`, `UnitSpawnReason`, `PrefabKind`, `PrefabEntry`, `PrefabGroup`, and `GlobalPrefabTable`. `UnitWorld.SpawnUnit` changes to the design-owned `UnitUid SpawnUnit(in UnitSpawnRequest)` signature. Existing UnitUid, TeamId, FixedPoint, Command, Aim, Snapshot, AbilitySignal, and Checksum types are reused unchanged.

Unit and Handler inheritance changes are intentionally source-breaking: callers must use prefab/component composition and runtime initialization rather than `new`. No obsolete second public constructor/spawn path remains.

## 10. Validation

- Unity MCP AssetDatabase refresh; wait for compilation; inspect Error/Exception/Assert/Warning output.
- EditMode: RuntimeConfig lookup/duplicate/invalid entry; Unit component owner binding; formal spawn UID/context; missing component/prefab failure; D-009 ownership; Physics UID/team registration; affected existing Unit and FrameSync fixtures.
- PlayMode: instantiate a configured Unit prefab, verify Unity lifecycle/component ownership, deterministic identity, pose/query registration, and no reliance on Unity InstanceID.
- Determinism: identical Tick/request/table/prototype input produces equal UID/query data; stable table lookup is ID-based; runtime math remains fp.
- Run broader Unit/FrameSync suites only after focused migration tests compile; pre-existing out-of-scope functional failures are recorded honestly.

## 11. Failure and recovery

Stop only for a Current-design public-contract conflict, an unavoidable out-of-scope core protocol change, a new package requirement, or an external compile blocker. Apply changes in assembly/component/spawn/test order so a partial migration can resume from Progress. Do not keep a second pure-C# Unit/Handler implementation, weaken tests, or silently fall back to programmatically assembled production Units.

## 12. Results

In progress. Record exact production/test files, contract/dependency changes, Unity compilation, EditMode/PlayMode results, design invariants, remaining limits, and scope exceptions at completion.
